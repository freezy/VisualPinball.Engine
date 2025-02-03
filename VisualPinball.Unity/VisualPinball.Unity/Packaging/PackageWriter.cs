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
		public const string TableStorage = "table";
		public const string ItemStorage = "items";
		public const string ItemReferenceStorage = "refs";
		public const string SceneStream = "scene.glb";

		private readonly GameObject _table;
		private readonly PackNameLookup _typeLookup;
		private readonly DateTime _now;
		private CFStorage _tableStorage;

		public PackageWriter(GameObject table)
		{
			_table = table;
			_typeLookup = new PackNameLookup();
			_now = DateTime.Now;
		}

		public async void WritePackage(string path)
		{
			var sw = new Stopwatch();
			sw.Start();
			using var cf = new CompoundFile();

			_tableStorage = cf.RootStorage.AddStorage(TableStorage);
			_tableStorage.CreationDate = _now;
			_tableStorage.ModifyDate = _now;

			var sceneStream = _tableStorage.AddStream(SceneStream);

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

			WritePackables(ItemStorage, packageable => packageable.Pack(_table.transform));
			WritePackables(ItemReferenceStorage, packageable => packageable.PackReferences(_table.transform, _typeLookup));

			if (File.Exists(path)) {
				File.Delete(path);
			}
			cf.SaveAs(path);
			cf.Close();
			sw.Stop();
			Debug.Log($"File saved to {path} in {sw.ElapsedMilliseconds}ms.");
		}

		/// <summary>
		/// Walks through the entire game object tree and creates the same structure for
		/// a given storage name, for each IPackageable component.
		/// </summary>
		/// <param name="rootName">Name of the storage within table storage</param>
		/// <param name="action">Action to perform with the data.</param>
		private void WritePackables(string rootName, Func<IPackable, byte[]> action)
		{

			// -> rootName <- / 0.0.0 / CompType / 0
			var storage = _tableStorage.AddStorage(rootName);
			storage.CreationDate = _now;
			storage.ModifyDate = _now;

			// walk the entire tree
			foreach (var t in _table.transform.GetComponentsInChildren<Transform>()) {

				// for each go, loop through all components
				var key = t.GetPath(_table.transform);
				var counters = new Dictionary<string, int>();

				CFStorage itemStorage = null;
				foreach (var component in t.gameObject.GetComponents<Component>()) {
					switch (component) {
						case IPackable packageable: {

							var packName = _typeLookup.GetName(packageable.GetType());
							counters.TryAdd(packName, 0);

							var bytes = action(packageable);
							if (bytes.Length > 0) {

								// rootName / -> 0.0.0 <- / CompType / 0
								if (itemStorage == null) {
									itemStorage = storage.AddStorage(key);
									itemStorage.CreationDate = _now;
									itemStorage.ModifyDate = _now;
								}

								// rootName / 0.0.0 / -> CompType <- / 0
								if (!itemStorage.TryGetStorage(packName, out var typeStorage)) {
									typeStorage = itemStorage.AddStorage(packName);
									typeStorage.CreationDate = _now;
									typeStorage.ModifyDate = _now;
								}

								// rootName / 0.0.0 / CompType / -> 0 <-
								var dataStream = typeStorage.AddStream($"{counters[packName]++}");
								dataStream.Append(bytes);

							}
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
			}
		}
	}
}
