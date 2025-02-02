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
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
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
		private GameObject _tableGo;
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public VpeImporter(string vpePath)
		{
			_vpePath = vpePath;
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
				ImportComponents();

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
			_tableGo = PrefabUtility.InstantiatePrefab(glbPrefab) as GameObject;
			if (_tableGo == null) {
				_tableGo = Object.Instantiate(glbPrefab); // fallback instantiation in case the above method fails.
			}
			_tableGo.transform.SetParent(null);
			PrefabUtility.UnpackPrefabInstance(_tableGo, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
			EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); // mark the active scene as dirty so that the changes are saved.
		}


		private void ImportComponents()
		{
			var typeLookup = new PackNameLookup();
			var itemsStorage = _tableStorage.GetStorage(PackageWriter.DataStorage);
			// -> items <- / 0.0.0 / CompType / 0
			itemsStorage.VisitEntries(itemEntry => {
				// items / -> 0.0.0 <- / CompType / 0
				if (itemEntry is CFStorage itemStorage) {
					var item = _tableGo.transform.FindByPath(itemEntry.Name);
					itemStorage.VisitEntries(typeEntry => {
						// items / 0.0.0 / -> CompType <- / 0
						if (typeEntry is CFStorage typeStorage) {
							var t = typeLookup.GetType(typeEntry.Name);
							typeStorage.VisitEntries(typedEntry => {

								// items / 0.0.0 / CompType / -> 0 <- (there might be multiple components of the same type)
								if (typedEntry is CFStream stream) {

									// now we can add the component and unpack it.
									var comp = item.gameObject.AddComponent(t) as IPackageable;
									comp?.Unpack(stream.GetData(), _tableGo.transform);
								} else {
									throw new Exception("Component entry must be of type stream.");
								}
							}, false);
						} else {
							throw new Exception("Type entry must be of type storage.");
						}
					}, false);
				} else {
					throw new Exception("Path entry must be of type storage.");
				}
			}, false);
		}
	}
}
