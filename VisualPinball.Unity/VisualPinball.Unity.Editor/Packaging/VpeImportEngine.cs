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
using System.IO;
using System.Text;
using Newtonsoft.Json;
using NLog;
using OpenMcdf;
using OpenMcdf.Extensions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
using Logger = NLog.Logger;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{
	public static class VpeImportEngine
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public static async void ImportIntoScene(string path, string tableName)
		{
			using var cf = new CompoundFile(path);
			try {
				var tableStorage = cf.RootStorage.GetStorage(PackageWriter.TableStorage);

				var assetPath = Path.Combine(Application.dataPath, "Resources", tableName);
				var n = 0;
				while (Directory.Exists(assetPath)) {
					assetPath = Path.Combine(Application.dataPath, "Resources", $"{tableName} ({++n})");
				}
				Directory.CreateDirectory(assetPath);
				var glbPath = Path.Combine(assetPath, $"{tableName}.glb");

				try {
					AssetDatabase.StartAssetEditing();

					// dump glb
					var sceneStream = tableStorage.GetStream(PackageWriter.SceneStream);
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
					throw new Exception($"Could not load {tableName}.glb at path: {glbRelativePath}");
				}
				var tableGo = PrefabUtility.InstantiatePrefab(glbPrefab) as GameObject;
				if (tableGo == null) {
					tableGo = Object.Instantiate(glbPrefab); // fallback instantiation in case the above method fails.
				}
				tableGo.transform.SetParent(null);
				PrefabUtility.UnpackPrefabInstance(tableGo, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
				EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); // mark the active scene as dirty so that the changes are saved.

				var dataStorage = tableStorage.GetStorage(PackageWriter.DataStorage);
				dataStorage.VisitEntries(entry => {
					if (entry is CFStream stream) {
						var item = entry.Name == "0"
							? tableGo.transform
							: tableGo.transform.FindByPath(entry.Name[2..]);
						var data = JsonConvert.DeserializeObject<List<PackagedItem>>(Encoding.UTF8.GetString(stream.GetData()));
						foreach (var packagedItem in data) {
							switch (packagedItem.Type) {
								case "VisualPinball.Unity.KickerComponent": RestoreComponent<KickerComponent>(item, packagedItem, tableGo.transform); break;
								case "VisualPinball.Unity.KickerColliderComponent": RestoreComponent<KickerColliderComponent>(item, packagedItem, tableGo.transform); break;
								case "VisualPinball.Unity.TroughComponent": RestoreComponent<TroughComponent>(item, packagedItem, tableGo.transform); break;
								default:
									Logger.Warn($"Unknown packaged item type: {packagedItem.Type}");
									break;
							}
						}
					}
				}, false);


				Debug.Log($"Data path = {Application.dataPath}");


			} finally {
				cf.Close();
			}
		}

		private static void RestoreComponent<T>(Transform item,  PackagedItem packagedItem, Transform root) where T : MonoBehaviour, IPackageable
		{
			var comp = item.gameObject.AddComponent<T>();
			comp.FromPackageData(packagedItem.Data, root);
		}
	}
}
