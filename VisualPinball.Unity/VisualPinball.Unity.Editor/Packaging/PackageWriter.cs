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
using GLTFast;
using GLTFast.Export;
using GLTFast.Logging;
using MemoryPack;
using UnityEngine;
using VisualPinball.Unity.Editor.Packaging;
using Debug = UnityEngine.Debug;

namespace VisualPinball.Unity
{
	public class PackageWriter
	{
		public const string TableStorage = "table";
		public const string ItemStorage = "items";
		public const string ItemStream = "item";
		public const string ItemReferenceStorage = "refs";
		public const string SceneStream = "scene.glb";
		public const string GlobalStorage = "global";
		public const string SwitchesStream = "switches";
		public const string CoilsStream = "coils";
		public const string WiresStream = "wires";
		public const string LampsStream = "lamps";

		private readonly GameObject _table;
		private readonly PackNameLookup _typeLookup;
		private readonly DateTime _now;
		private IPackageFolder _tableStorage;

		public PackageWriter(GameObject table)
		{
			_table = table;
			_typeLookup = new PackNameLookup();
			_now = DateTime.Now;
		}

		public async Task WritePackage(string path)
		{
			var sw = new Stopwatch();
			sw.Start();
			if (File.Exists(path)) {
				File.Delete(path);
			}

			using var storage = PackageApi.StorageManager.CreateStorage(path);

			_tableStorage = storage.AddFolder(TableStorage);

			await WriteScene();
			WritePackables(ItemStorage, packageable => packageable.Pack(), go => new ItemPackable(go).Pack());
			WritePackables(ItemReferenceStorage, packageable => packageable.PackReferences(_table.transform, _typeLookup));
			WriteGlobals();

			storage.Close();
			sw.Stop();
			Debug.Log($"File saved to {path} in {sw.ElapsedMilliseconds}ms.");
		}

		private async Task WriteScene()
		{
			var glbFile = _tableStorage.AddFile(SceneStream);

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

			await export.SaveToStreamAndDispose(glbFile.AsStream());
		}

		/// <summary>
		/// Walks through the entire game object tree and creates the same structure for
		/// a given storage name, for each IPackageable component.
		/// </summary>
		/// <param name="folderName">Name of the storage within table storage</param>
		/// <param name="getPackableData">Retrieves component-specific data.</param>
		/// <param name="getItemData">Retrieves item-specific data.</param>
		private void WritePackables(string folderName, Func<IPackable, byte[]> getPackableData, Func<GameObject, byte[]> getItemData = null)
		{
			// -> rootName <- / 0.0.0 / CompType / 0
			var folder = _tableStorage.AddFolder(folderName);

			// walk the entire tree
			foreach (var t in _table.transform.GetComponentsInChildren<Transform>()) {

				// for each go, loop through all components
				var key = t.GetPath(_table.transform);
				var counters = new Dictionary<string, int>();
				var itemData = getItemData?.Invoke(t.gameObject);

				// rootName / -> 0.0.0 <- / CompType / 0
				IPackageFolder itemStorage = null;
				if (itemData?.Length > 0) {
					itemStorage = folder.AddFolder(key);

					var itemStream = itemStorage.AddFile(ItemStream);
					itemStream.SetData(itemData);
				}

				foreach (var component in t.gameObject.GetComponents<Component>()) {
					switch (component) {
						case IPackable packageable: {

							var packName = _typeLookup.GetName(packageable.GetType());
							counters.TryAdd(packName, 0);



							var packableData = getPackableData(packageable);
							if (packableData.Length > 0) {

								// rootName / -> 0.0.0 <- / CompType / 0
								if (itemStorage == null) {
									itemStorage = folder.AddFolder(key);
								}

								// rootName / 0.0.0 / -> CompType <- / 0
								if (!itemStorage.TryGetFolder(packName, out var typeStorage)) {
									typeStorage = itemStorage.AddFolder(packName);
								}

								// rootName / 0.0.0 / CompType / -> 0 <-
								var dataStream = typeStorage.AddFile($"{counters[packName]++}");
								dataStream.SetData(packableData);
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

		private void WriteGlobals()
		{
			var tableComponent = _table.GetComponent<TableComponent>();
			if (!tableComponent) {
				throw new Exception("Cannot find table component on table object.");
			}

			var globalStorage = _tableStorage.AddFolder(GlobalStorage);

			foreach (var sw in tableComponent.MappingConfig.Switches) {
				sw.SaveReference(_table.transform);
			}
			foreach (var coil in tableComponent.MappingConfig.Coils) {
				coil.SaveReference(_table.transform);
			}
			foreach (var wire in tableComponent.MappingConfig.Wires) {
				wire.SaveReferences(_table.transform);
			}
			foreach (var lp in tableComponent.MappingConfig.Lamps) {
				lp.SaveReference(_table.transform);
			}

			globalStorage.AddFile(SwitchesStream).SetData(MemoryPackSerializer.Serialize(tableComponent.MappingConfig.Switches));
			globalStorage.AddFile(CoilsStream).SetData(MemoryPackSerializer.Serialize(tableComponent.MappingConfig.Coils));
			globalStorage.AddFile(WiresStream).SetData(MemoryPackSerializer.Serialize(tableComponent.MappingConfig.Wires));
			globalStorage.AddFile(LampsStream).SetData(MemoryPackSerializer.Serialize(tableComponent.MappingConfig.Lamps));
		}
	}
}
