using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using NLog;
using OpenMcdf;
using VisualPinball.Engine.Common;
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
				Profiler.Start("TableLoader.Load (I/O)");
				var gameStorage = cf.RootStorage.GetStorage("GameStg");
				var gameData = gameStorage.GetStream("GameData");
				var bytes = gameData.GetData();
				Profiler.Stop("TableLoader.Load (I/O)");

				using (var stream = new MemoryStream(bytes))
				using (var reader = new BinaryReader(stream)) {
					var table = new Table(reader);

					LoadTableInfo(table, cf.RootStorage, gameStorage);
					if (loadGameItems) {
						LoadGameItems(table, gameStorage);
					}
					LoadTextures(table, gameStorage);

					// todo sounds

					table.SetupPlayfieldMesh();
					return table;
				}

			} finally {
				cf.Close();
				Profiler.Stop("TableLoader.Load()");
			}
		}

		public static byte[][] ReadGameItems(string fileName, int numGameItems)
		{
			var gameItemData = new byte[numGameItems][];
			var cf = new CompoundFile(fileName);
			try {
				var storage = cf.RootStorage.GetStorage("GameStg");
				for (var i = 0; i < numGameItems; i++) {
					var itemName = $"GameItem{i}";
					var itemStream = storage.GetStream(itemName);
					gameItemData[i] = itemStream.GetData();
				}
			} finally {
				cf.Close();
			}
			return gameItemData;
		}

		public static void LoadGameItem(byte[] itemData, int storageIndex, out int itemType, out object item)
		{
			item = null;
			var itemName = $"GameItem{storageIndex}";
			var reader = new BinaryReader(new MemoryStream(itemData));
			itemType = reader.ReadInt32();
			switch (itemType) {
				case ItemType.Bumper: item = new Bumper.Bumper(reader, itemName); break;
				case ItemType.Flasher: item = new Flasher.Flasher(reader, itemName); break;
				case ItemType.Flipper: item = new Flipper.Flipper(reader, itemName); break;
				case ItemType.Gate: item = new Gate.Gate(reader, itemName); break;
				case ItemType.HitTarget: item = new HitTarget.HitTarget(reader, itemName); break;
				case ItemType.Kicker: item = new Kicker.Kicker(reader, itemName); break;
				case ItemType.Light: item = new Light.Light(reader, itemName); break;
				case ItemType.Primitive: item = new Primitive.Primitive(reader, itemName); break;
				case ItemType.Ramp: item = new Ramp.Ramp(reader, itemName); break;
				case ItemType.Rubber: item = new Rubber.Rubber(reader, itemName); break;
				case ItemType.Spinner: item = new Spinner.Spinner(reader, itemName); break;
				case ItemType.Surface: item = new Surface.Surface(reader, itemName); break;
				case ItemType.Trigger: item = new Trigger.Trigger(reader, itemName); break;
				default: itemType = -1; break;
			}
		}

		private static void LoadGameItems(Table table, CFStorage storage)
		{
			Profiler.Start("LoadGameItems");
			for (var i = 0; i < table.Data.NumGameItems; i++) {
				Profiler.Start("LoadGameItems (I/O)");
				var itemName = $"GameItem{i}";
				storage.TryGetStream(itemName, out var itemStream);
				if (itemStream == null) {
					Logger.Warn("Could not find stream {0}, skipping.", itemName);
					continue;
				}
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
					case ItemType.Collection: {
						var item = new VisualPinball.Engine.VPT.Collection.Collection(reader, itemName);
						table.Collections[item.Name] = item;
						break;
					}
					case ItemType.Decal: {
						var item = new VisualPinball.Engine.VPT.Decal.Decal(reader, itemName);
						table.Decals[item.Name] = item;
						break;
					}
					case ItemType.DispReel: {
						var item = new VisualPinball.Engine.VPT.DispReel.DispReel(reader, itemName);
						table.DispReels[item.Name] = item;
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
					case ItemType.LightSeq: {
						var item = new VisualPinball.Engine.VPT.LightSeq.LightSeq(reader, itemName);
						table.LightSeqs[item.Name] = item;
						break;
					}
					case ItemType.Plunger: {
						var item = new VisualPinball.Engine.VPT.Plunger.Plunger(reader, itemName);
						table.Plungers[item.Name] = item;
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
					case ItemType.Textbox: {
						var item = new TextBox.TextBox(reader, itemName);
						table.TextBoxes[item.Name] = item;
						break;
					}
					case ItemType.Timer: {
						var item = new Timer.Timer(reader, itemName);
						table.Timers[item.Name] = item;
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
				storage.TryGetStream(textureName, out var textureStream);
				if (textureStream == null) {
					Logger.Warn("Could not find stream {0}, skipping.", textureName);
					continue;
				}
				var textureData = textureStream.GetData();
				Profiler.Stop("LoadTextures (I/O)");
				if (textureData.Length < 4) {
					Logger.Warn("Skipping {itemName} because it has size of {itemDataLength}.", textureName, textureData.Length);
					continue;
				}

				Profiler.Start("LoadTextures (parse)");
				using (var stream = new MemoryStream(textureData))
				using (var reader = new BinaryReader(stream)) {
					var texture = new Texture(reader, textureName);
					table.Textures[texture.Name.ToLower()] = texture;
				}
				Profiler.Stop("LoadTextures (parse)");
			}
			Profiler.Stop("LoadTextures");
		}

		private static void LoadTableInfo(Table table, CFStorage rootStorage, CFStorage gameStorage)
		{
			// first, although we can loop through entries, get them from the game storage, so we
			// know their order, which is important when writing back (because you know, hashing).
			gameStorage.TryGetStream("CustomInfoTags", out var citStream);
			if (citStream != null) {
				using (var stream = new MemoryStream(citStream.GetData()))
				using (var reader = new BinaryReader(stream)) {
					table.CustomInfoTags = new CustomInfoTags(reader);
				}
			}

			// now actually read them in
			rootStorage.TryGetStorage("TableInfo", out var tableInfoStorage);
			if (tableInfoStorage == null) {
				Logger.Info("TableInfo storage not found, skipping.");
				return;
			}
			tableInfoStorage.VisitEntries(item => {
				if (item.IsStream) {
					var itemStream = item as CFStream;
					if (itemStream != null) {
						table.TableInfo[item.Name] = BiffUtil.ParseWideString(itemStream.GetData());
					}
				}
			}, false);
		}

		private static void LoadTableMeta(Table table, CFStorage gameStorage)
		{
			// version
			gameStorage.TryGetStream("Version", out var versionBytes);
			if (versionBytes != null) {
				table.FileVersion = BitConverter.ToInt32(versionBytes.GetData(), 0);
			} else {
				Logger.Info("No Version under GameStg found, skipping.");
			}


			// hash
			gameStorage.TryGetStream("Version", out var hashBytes);
			if (hashBytes != null) {
				table.FileHash = hashBytes.GetData();
			} else {
				Logger.Info("No MAC under GameStg found, skipping.");
			}
		}
	}
}
