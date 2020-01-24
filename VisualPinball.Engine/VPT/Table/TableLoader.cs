using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using NLog;
using OpenMcdf;
using VisualPinball.Engine.IO;

namespace VisualPinball.Engine.VPT.Table
{
	/// <summary>
	/// The entry point for loading and parsing the VPX file.
	/// </summary>
	public static class TableLoader
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public static Table Load(string filename, bool loadGameItems = true)
		{
			Profiler.Start("TableLoader.Load()");
			var cf = new CompoundFile(filename);
			try {
				Profiler.Start("I/O");
				var gameStorage = cf.RootStorage.GetStorage("GameStg");
				var gameData = gameStorage.GetStream("GameData");
				var bytes = gameData.GetData();
				Profiler.Stop("I/O");

				using (var stream = new MemoryStream(bytes))
				using (var reader = new BinaryReader(stream)) {
					var table = new Table(reader);

					LoadTableInfo(table, cf.RootStorage);
					if (loadGameItems) {
						LoadGameItems(table, gameStorage);
					}
					LoadTextures(table, gameStorage);

					table.SetupPlayfieldMesh();
					return table;
				}

			} finally {
				cf.Close();
				Profiler.Stop("TableLoader.Load()");
			}
		}

		public static void LoadGameItem(byte[] itemData, int storageIndex, out int itemType, out object item)
		{
			item = null;
			var itemName = $"GameItem{storageIndex}";
			var reader = new BinaryReader(new MemoryStream(itemData));
			itemType = reader.ReadInt32();
			switch (itemType) {
				case ItemType.Primitive: {
					item = new Primitive.Primitive(reader, itemName);
					break;
				}
				default:
					itemType = -1;
					break;
			}
		}

		private static void LoadGameItems(Table table, CFStorage storage)
		{
			Profiler.Start("LoadGameItems");
			for (var i = 0; i < table.Data.NumGameItems; i++) {
				Profiler.Start("LoadGameItems (I/O)");
				var itemName = $"GameItem{i}";
				var itemStream = storage.GetStream(itemName);
				var itemData = itemStream.GetData();
				Profiler.Stop("LoadGameItems (I/O)");
				if (itemData.Length < 4) {
					Logger.Warn("Skipping {itemName} because it has size of {itemDataLength}.", itemName, itemData.Length);
					continue;
				}

				var reader = new BinaryReader(new MemoryStream(itemData));
				var itemType = reader.ReadInt32();
				Profiler.Start("LoadGameItems (parse)");
				switch (itemType) {
					case ItemType.Bumper: {
						var item = new VisualPinball.Engine.VPT.Bumper.Bumper(reader, itemName);
						table.Bumpers[item.Name] = item;
						break;
					}
					case ItemType.Flasher: {
						var item = new VisualPinball.Engine.VPT.Flasher.Flasher(reader, itemName);
						table.Flashers[item.Name] = item;
						break;
					}
					case ItemType.Flipper: {
						var item = new VisualPinball.Engine.VPT.Flipper.Flipper(reader, itemName);
						table.Flippers[item.Name] = item;
						break;
					}
					case ItemType.Gate: {
						var item = new VisualPinball.Engine.VPT.Gate.Gate(reader, itemName);
						table.Gates[item.Name] = item;
						break;
					}
					case ItemType.HitTarget: {
						var item = new VisualPinball.Engine.VPT.HitTarget.HitTarget(reader, itemName);
						table.HitTargets[item.Name] = item;
						break;
					}
					case ItemType.Kicker: {
						var item = new VisualPinball.Engine.VPT.Kicker.Kicker(reader, itemName);
						table.Kickers[item.Name] = item;
						break;
					}
					case ItemType.Light: {
						var item = new VisualPinball.Engine.VPT.Light.Light(reader, itemName);
						table.Lights[item.Name] = item;
						break;
					}
					case ItemType.Primitive: {
						var item = new Primitive.Primitive(reader, itemName);
						table.Primitives[item.Name] = item;
						break;
					}
					case ItemType.Ramp: {
						var item = new Ramp.Ramp(reader, itemName);
						table.Ramps[item.Name] = item;
						break;
					}
					case ItemType.Rubber: {
						var item = new Rubber.Rubber(reader, itemName);
						table.Rubbers[item.Name] = item;
						break;
					}
					case ItemType.Spinner: {
						var item = new Spinner.Spinner(reader, itemName);
						table.Spinners[item.Name] = item;
						break;
					}
					case ItemType.Surface: {
						var item = new Surface.Surface(reader, itemName);
						table.Surfaces[item.Name] = item;
						break;
					}
					case ItemType.Trigger: {
						var item = new Trigger.Trigger(reader, itemName);
						table.Triggers[item.Name] = item;
						break;
					}
				}
				Profiler.Stop("LoadGameItems (parse)");
			}
			Profiler.Stop("LoadGameItems");
		}

		private static void LoadTextures(Table table, CFStorage storage)
		{
			Profiler.Start("LoadTextures");
			for (var i = 0; i < table.Data.NumTextures; i++) {
				var textureName = $"Image{i}";
				Profiler.Start("LoadTextures (I/O)");
				var textureStream = storage.GetStream(textureName);
				var textureData = textureStream.GetData();
				Profiler.Stop("LoadTextures (I/O)");
				if (textureData.Length < 4) {
					Logger.Warn("Skipping {itemName} because it has size of {itemDataLength}.", textureName, textureData.Length);
					continue;
				}

				Profiler.Start("LoadTextures (parse)");
				var reader = new BinaryReader(new MemoryStream(textureData));
				var texture = new Texture(reader, textureName);
				table.Textures[texture.Name.ToLower()] = texture;
				Profiler.Stop("LoadTextures (parse)");
			}
			Profiler.Stop("LoadTextures");
		}

		private static void LoadTableInfo(Table table, CFStorage storage)
		{
			var tableInfoStorage = storage.GetStorage("TableInfo");
			tableInfoStorage.VisitEntries(item => {
				if (item.IsStream) {
					var itemStream = item as CFStream;
					if (itemStream != null) {
						table.TableInfo[item.Name] = BiffUtil.ParseWideString(itemStream.GetData());
					}
				}
			}, false);

		}
	}
}
