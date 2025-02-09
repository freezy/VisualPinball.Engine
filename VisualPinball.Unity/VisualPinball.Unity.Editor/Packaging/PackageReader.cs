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
using NLog;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Logger = NLog.Logger;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{
	public class PackageReader
	{
		private readonly string _vpePath;
		private IPackageFolder _tableFolder;
		private string _assetPath;
		private string _tableName;
		private GameObject _table;
		private PackagedRefs _refs;
		private PackagedFiles _files;
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public PackageReader(string vpePath)
		{
			_vpePath = vpePath;
		}

		public async Task ImportIntoScene(string tableName)
		{
			var sw = new Stopwatch();
			sw.Start();
			_tableName = tableName;

			using var storage = PackageApi.StorageManager.OpenStorage(_vpePath);
			try {
				Setup(storage);
				await ImportModels();

				_refs = new PackagedRefs(_table.transform);
				_files = new PackagedFiles(_tableFolder, _refs);

				await ReadAssets();

				// create components and update game objects
				ReadPackables(PackageApi.ItemFolder, (go, file) => ItemPackable.Unpack(file.GetData()).Apply(go), (item, type, stream, index) => {
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
				});

				// add references
				ReadPackables(PackageApi.ItemReferencesFolder, null, (item, type, stream, _) => {
					// add the component and unpack it.
					var comp = item.gameObject.GetComponent(type) as IPackable;
					comp?.UnpackReferences(stream.GetData(), _table.transform, _refs, _files);
				});

				ReadGlobals();

			} finally {
				storage.Close();
				Logger.Info($"Scene import took {sw.ElapsedMilliseconds}ms.");
			}
		}

		private void Setup(IPackageStorage storage)
		{
			// open storages
			_tableFolder = storage.GetFolder(PackageApi.TableFolder);
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
				var sceneFile = _tableFolder.GetFile(PackageApi.SceneFile);
				await using var glbFileFile = new FileStream(glbPath, FileMode.Create, FileAccess.Write);
				await sceneFile.AsStream().CopyToAsync(glbFileFile);

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

		private async Task ReadAssets()
		{
			_files.UnpackAssets(_assetPath);
			await _files.UnpackMeshes(_assetPath);
			_files.UnpackSounds(_assetPath);
		}

		/// <summary>
		/// Loops through all items and components in the package, and applies the given action.
		/// The item action is executed before the component action.
		/// </summary>
		/// <param name="rootFolder">Which root folder of the package to start with</param>
		/// <param name="itemAction">Action to execute on the item</param>
		/// <param name="componentAction">Action to execute on the component</param>
		/// <exception cref="Exception">When there are folder paths in the package that don't correspond to the instantiated scene.</exception>
		private void ReadPackables(string rootFolder, Action<GameObject, IPackageFile> itemAction, Action<Transform, Type, IPackageFile, int> componentAction) {

			var itemsFolder = _tableFolder.GetFolder(rootFolder);

			// -> rootFolder <- / 0.0.0 / CompType / 0
			itemsFolder.VisitFolders(itemFolder => {

				// rootFolder / -> 0.0.0 <- / CompType / 0
				var item = _table.transform.FindByPath(itemFolder.Name);
				if (item == null) {
					throw new Exception($"Cannot find item at path {itemFolder.Name} on node {_table.name}");
				}
				if (itemAction != null && itemFolder.TryGetFile(PackageApi.ItemFile, out var itemFile, PackageApi.Packer.FileExtension)) {
					itemAction(item.gameObject, itemFile);
				}
				itemFolder.VisitFolders(typeFolder => {

					// rootFolder / 0.0.0 / -> CompType <- / 0
					var t = _refs.GetType(typeFolder.Name);
					var index = 0;
					typeFolder.VisitFiles(compFile => {

						// rootFolder / 0.0.0 / CompType / -> 0 <- (there might be multiple components of the same type)
						componentAction(item, t, compFile, index++);
					});
				});
			});
		}

		private void ReadGlobals()
		{
			var tableComponent = _table.GetComponent<TableComponent>();
			if (!tableComponent) {
				throw new Exception("Cannot find table component on table object.");
			}
			var globalStorage = _tableFolder.GetFolder(PackageApi.GlobalFolder);
			tableComponent.MappingConfig = new MappingConfig {
				Switches = PackageApi.Packer.Unpack<List<SwitchMapping>>(globalStorage.GetFile(PackageApi.SwitchesFile, PackageApi.Packer.FileExtension).GetData()),
				Coils = PackageApi.Packer.Unpack<List<CoilMapping>>(globalStorage.GetFile(PackageApi.CoilsFile, PackageApi.Packer.FileExtension).GetData()),
				Lamps = PackageApi.Packer.Unpack<List<LampMapping>>(globalStorage.GetFile(PackageApi.LampsFile, PackageApi.Packer.FileExtension).GetData()),
				Wires = PackageApi.Packer.Unpack<List<WireMapping>>(globalStorage.GetFile(PackageApi.WiresFile, PackageApi.Packer.FileExtension).GetData()),
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
