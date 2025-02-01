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
using System.Text;
using GLTFast;
using GLTFast.Export;
using GLTFast.Logging;
using Newtonsoft.Json;
using OpenMcdf;
using OpenMcdf.Extensions;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace VisualPinball.Unity
{
	public class PackageWriter
	{
		private const string TableStorage = "table";
		private const string DataStorage = "items";
		private const string SceneStream = "scene.glb";

		private readonly GameObject _table;

		public PackageWriter(GameObject table)
		{
			_table = table;
		}

		public async void WritePackage(string path)
		{
			var now = DateTime.Now;
			var sw = new Stopwatch();
			sw.Start();
			using var cf = new CompoundFile();

			var tableStorage = cf.RootStorage.AddStorage(TableStorage);
			tableStorage.CreationDate = now;
			tableStorage.ModifyDate = now;

			var dataStorage = tableStorage.AddStorage(DataStorage);
			dataStorage.CreationDate = now;
			dataStorage.ModifyDate = now;

			var sceneStream = tableStorage.AddStream(SceneStream);

			var logger = new ConsoleLogger();
			var exportSettings = new ExportSettings {

				// Format = GltfFormat.Json,
				Format = GltfFormat.Binary,
				FileConflictResolution = FileConflictResolution.Overwrite,
				Deterministic = false,

				// Export everything except cameras or animation
				ComponentMask = ~(ComponentType.Camera | ComponentType.Animation),

				// Boost light intensities
				LightIntensityFactor = 100f,

				// Ensure mesh vertex attributes colors and texture coordinate (channels 1 through 8) are always
				// exported, even if they are not used/referenced.
				PreservedVertexAttributes = VertexAttributeUsage.AllTexCoords | VertexAttributeUsage.Color,

				// Enable Draco compression
				Compression = Compression.Draco,

				// Optional: Tweak the Draco compression settings
				DracoSettings = new DracoExportSettings
				{
					positionQuantization = 12
				}
			};
			var gameObjectExportSettings = new GameObjectExportSettings {

				// Include inactive GameObjects in export
				OnlyActiveInHierarchy = false,

				// Also export disabled components
				DisabledComponents = true,

				// Only export GameObjects on certain layers
				//LayerMask = LayerMask.GetMask("Default", "MyCustomLayer"),
			};

			var export = new GameObjectExport(exportSettings, gameObjectExportSettings, logger: logger);
			export.AddScene(new [] { _table }, _table.transform.worldToLocalMatrix, "VPE Table");

			await export.SaveToStreamAndDispose(sceneStream.AsIOStream());

			// walk the entire tree
			foreach (var t in _table.transform.GetComponentsInChildren<Transform>()) {

				// for each go, loop through all components
				var key = t.GetPath(_table.transform);
				var packageables = new List<PackagedItem>();
				foreach (var component in t.gameObject.GetComponents<Component>()) {

					switch (component) {
						case IPackageable packageable: {
							packageables.Add(new PackagedItem(packageable.GetType(), packageable.ToPackageData()));
							break;
						}

						// those are covered by the glTF export
						case Transform:
						case MeshFilter:
						case MeshRenderer:
							break;

						default:
							Debug.LogWarning($"Unknown component {component.GetType()} on {key} ({component.name})");
							break;
					}
				}

				if (packageables.Count > 0) {
					var gameItemStream = dataStorage.AddStream(key);
					gameItemStream.Append(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(packageables)));
				}
			}

			if (File.Exists(path)) {
				File.Delete(path);
			}
			cf.SaveAs(path);
			cf.Close();
			sw.Stop();
			Debug.Log($"File saved to {path} in {sw.ElapsedMilliseconds}ms.");
		}
	}
}
