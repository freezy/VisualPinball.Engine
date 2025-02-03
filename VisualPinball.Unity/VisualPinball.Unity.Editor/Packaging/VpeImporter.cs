// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using MemoryPack;
using NLog;
using OpenMcdf;
using OpenMcdf.Extensions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Logger = NLog.Logger;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{
	public class VpeImporter
	{
		private readonly string _vpePath;
		private CFStorage _tableStorage;
		private string _assetPath;
		private string _tableName;
		private GameObject _table;
		private readonly PackNameLookup _typeLookup;
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public VpeImporter(string vpePath)
		{
			_vpePath = vpePath;
			_typeLookup = new PackNameLookup();
		}

		public async Task ImportIntoScene(string tableName)
		{
			var sw = new Stopwatch();
			sw.Start();
			_tableName = tableName;
			using var cf = new CompoundFile(_vpePath);
			try {
				Setup(cf);
				await ImportModels();

				// create components and update game objects
				ReadPackables(PackageWriter.ItemStorage, (item, type, stream, index) => {
					// add or update component
					var comps = item.gameObject.GetComponents(type);
					var comp = comps.Length > index
						? comps[index]
						: item.gameObject.AddComponent(type);
					if (comp is IPackable packable) {
						packable.Unpack(stream.GetData());

					} else {
						throw new Exception($"Got component of type {type.FullName} that does not implement IPackable.");
					}

				}, (go, stream) => ItemPackable.Unpack(stream.GetData()).Apply(go));

				// add references
				ReadPackables(PackageWriter.ItemReferenceStorage, (item, type, stream, _) => {
					// add the component and unpack it.
					var comp = item.gameObject.GetComponent(type) as IPackable;
					comp?.UnpackReferences(stream.GetData(), _table.transform, _typeLookup);
				});

				ReadGlobals();

			} finally {
				cf.Close();
				Logger.Info($"Scene import took {sw.ElapsedMilliseconds}ms.");
			}
		}

		private void Setup(CompoundFile cf)
		{
			// open storages
			_tableStorage = cf.RootStorage.GetStorage(PackageWriter.TableStorage);
			_assetPath = Path.Combine(Application.dataPath, "Resources", _tableName);
			var n = 0;
			while (Directory.Exists(_assetPath)) {
				_assetPath = Path.Combine(Application.dataPath, "Resources", $"{_tableName} ({++n})");
			}
			Directory.CreateDirectory(_assetPath);
		}

		private async Task ImportModels()
		{
			var glbPath = Path.Combine(_assetPath, $"{_tableName}.glb");
			try {
				AssetDatabase.StartAssetEditing();

				// dump glb
				var sceneStream = _tableStorage.GetStream(PackageWriter.SceneStream);
				await using var glbFileStream = new FileStream(glbPath, FileMode.Create, FileAccess.Write);
				await sceneStream.AsIOStream().CopyToAsync(glbFileStream);

			} finally {
				// resume asset database refreshing
				AssetDatabase.StopAssetEditing();
				AssetDatabase.Refresh();
			}

			// add glb to scene
			var glbRelativePath = Path.GetRelativePath(Path.Combine(Application.dataPath, ".."), glbPath);
			var glbPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(glbRelativePath);
			if (glbPrefab == null) {
				throw new Exception($"Could not load {_tableName}.glb at path: {glbRelativePath}");
			}
			_table = PrefabUtility.InstantiatePrefab(glbPrefab) as GameObject;
			if (_table == null) {
				_table = Object.Instantiate(glbPrefab); // fallback instantiation in case the above method fails.
			}
			_table.transform.SetParent(null);
			PrefabUtility.UnpackPrefabInstance(_table, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
			EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); // mark the active scene as dirty so that the changes are saved.
		}

		private void ReadPackables(string storageRoot, Action<Transform, Type, CFStream, int> componentAction, Action<GameObject, CFStream> itemAction = null)
		{
			var itemsStorage = _tableStorage.GetStorage(storageRoot);
			// -> storageRoot <- / 0.0.0 / CompType / 0
			itemsStorage.VisitEntries(itemEntry => {
				// storageRoot / -> 0.0.0 <- / CompType / 0
				if (itemEntry is CFStorage itemStorage) {
					var item = _table.transform.FindByPath(itemEntry.Name);
					if (item == null) {
						throw new Exception($"Cannot find item at path {itemEntry.Name} on node {_table.name}");;
					}
					if (itemAction != null && itemStorage.TryGetStream(PackageWriter.ItemStream, out var itemStream)) {
						itemAction(item.gameObject, itemStream);
					}
					itemStorage.VisitEntries(typeEntry => {
						// storageRoot / 0.0.0 / -> CompType <- / 0
						if (typeEntry is CFStorage typeStorage) {
							var t = _typeLookup.GetType(typeEntry.Name);
							var index = 0;
							typeStorage.VisitEntries(typedEntry => {

								// storageRoot / 0.0.0 / CompType / -> 0 <- (there might be multiple components of the same type)
								if (typedEntry is CFStream stream) {
									componentAction(item, t, stream, index++);

								} else {
									throw new Exception("Component entry must be of type stream.");
								}
							}, false);

						} else if (typeEntry.Name != PackageWriter.ItemStream) {
							throw new Exception("Type entry must be of type storage.");
						}
					}, false);
				} else {
					throw new Exception("Path entry must be of type storage.");
				}
			}, false);
		}

		private void ReadGlobals()
		{
			var tableComponent = _table.GetComponent<TableComponent>();
			if (!tableComponent) {
				throw new Exception("Cannot find table component on table object.");
			}
			var globalStorage = _tableStorage.GetStorage(PackageWriter.GlobalStorage);
			tableComponent.MappingConfig = new MappingConfig {
				Switches = MemoryPackSerializer.Deserialize<List<SwitchMapping>>(globalStorage.GetStream(PackageWriter.SwitchesStream).GetData()),
				Coils = MemoryPackSerializer.Deserialize<List<CoilMapping>>(globalStorage.GetStream(PackageWriter.CoilsStream).GetData()),
				Lamps = MemoryPackSerializer.Deserialize<List<LampMapping>>(globalStorage.GetStream(PackageWriter.LampsStream).GetData()),
				Wires = MemoryPackSerializer.Deserialize<List<WireMapping>>(globalStorage.GetStream(PackageWriter.WiresStream).GetData()),
			};
			foreach (var sw in tableComponent.MappingConfig.Switches) {
				sw.RestoreReference(_table.transform);
			}
			foreach (var coil in tableComponent.MappingConfig.Coils) {
				coil.RestoreReference(_table.transform);
			}
			foreach (var lamp in tableComponent.MappingConfig.Lamps) {
				lamp.RestoreReference(_table.transform);
			}
			foreach (var wire in tableComponent.MappingConfig.Wires) {
				wire.RestoreReferences(_table.transform);
			}
		}
	}
}
