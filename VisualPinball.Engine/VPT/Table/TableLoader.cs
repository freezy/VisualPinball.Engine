// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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
using System.IO;
using NLog;
using OpenMcdf;
using VisualPinball.Engine.IO;
using VisualPinball.Engine.VPT.Collection;

namespace VisualPinball.Engine.VPT.Table
{
	/// <summary>
	/// The entry point for loading and parsing the VPX file.
	/// </summary>
	public static class TableLoader
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public static FileTableContainer Load(string filename, bool loadGameItems = true)
		{
			var cf = new CompoundFile(filename);
			try {
				var gameStorage = cf.RootStorage.GetStorage("GameStg");
				var gameData = gameStorage.GetStream("GameData");

				var fileVersion = BitConverter.ToInt32(gameStorage.GetStream("Version").GetData(), 0);
				using (var stream = new MemoryStream(gameData.GetData()))
				using (var reader = new BinaryReader(stream)) {
					var tableHolder = new FileTableContainer(reader);

					LoadTableInfo(tableHolder, cf.RootStorage, gameStorage);
					if (loadGameItems) {
						LoadGameItems(tableHolder, gameStorage);
					}
					LoadTextures(tableHolder, gameStorage);
					LoadSounds(tableHolder, gameStorage, fileVersion);
					LoadCollections(tableHolder, gameStorage);
					LoadMappings(tableHolder, gameStorage);
					LoadTableMeta(tableHolder, gameStorage);

					tableHolder.Table.SetupPlayfieldMesh();
					return tableHolder;
				}

			} finally {
				cf.Close();
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

		public static void LoadGameItem(byte[] itemData, int storageIndex, out ItemType itemType, out object item)
		{
			item = null;
			var itemName = $"GameItem{storageIndex}";
			var reader = new BinaryReader(new MemoryStream(itemData));

			// parse to enum
			var iItemType = reader.ReadInt32();
			if (!Enum.IsDefined(typeof(ItemType), iItemType)) {
				Logger.Info("Invalid item type " + iItemType);
				itemType = ItemType.Invalid;
				return;
			}

			itemType = (ItemType) iItemType;
			switch (itemType) {
				case ItemType.Bumper: item = new Bumper.Bumper(reader, itemName); break;
				case ItemType.Decal: item = new Decal.Decal(reader, itemName); break;
				case ItemType.DispReel: item = new DispReel.DispReel(reader, itemName); break;
				case ItemType.Flasher: item = new Flasher.Flasher(reader, itemName); break;
				case ItemType.Flipper: item = new Flipper.Flipper(reader, itemName); break;
				case ItemType.Gate: item = new Gate.Gate(reader, itemName); break;
				case ItemType.HitTarget: item = new HitTarget.HitTarget(reader, itemName); break;
				case ItemType.Kicker: item = new Kicker.Kicker(reader, itemName); break;
				case ItemType.Light: item = new Light.Light(reader, itemName); break;
				case ItemType.LightSeq: item = new LightSeq.LightSeq(reader, itemName); break;
				case ItemType.Plunger: item = new Plunger.Plunger(reader, itemName); break;
				case ItemType.Primitive: item = new Primitive.Primitive(reader, itemName); break;
				case ItemType.Ramp: item = new Ramp.Ramp(reader, itemName); break;
				case ItemType.Rubber: item = new Rubber.Rubber(reader, itemName); break;
				case ItemType.Spinner: item = new Spinner.Spinner(reader, itemName); break;
				case ItemType.Surface: item = new Surface.Surface(reader, itemName); break;
				case ItemType.TextBox: item = new TextBox.TextBox(reader, itemName); break;
				case ItemType.Timer: item = new Timer.Timer(reader, itemName); break;
				case ItemType.Trigger: item = new Trigger.Trigger(reader, itemName); break;
				case ItemType.Trough: item = new Trough.Trough(reader, itemName); break;
				default:
					Logger.Info("Unhandled item type " + itemType);
					itemType = ItemType.Invalid; break;
			}
		}

		private static void LoadGameItems(FileTableContainer tableContainer, CFStorage storage)
		{
			for (var i = 0; i < tableContainer.NumGameItems; i++) {
				var itemName = $"GameItem{i}";
				storage.TryGetStream(itemName, out var itemStream);
				if (itemStream == null) {
					Logger.Warn("Could not find stream {0}, skipping.", itemName);
					continue;
				}
				var itemData = itemStream.GetData();
				if (itemData.Length < 4) {
					Logger.Warn("Skipping {itemName} because it has size of {itemDataLength}.", itemName, itemData.Length);
					continue;
				}

				var reader = new BinaryReader(new MemoryStream(itemData));

				// parse to enum
				var iItemType = reader.ReadInt32();
				if (!Enum.IsDefined(typeof(ItemType), iItemType)) {
					Logger.Info("Invalid item type " + iItemType);
					return;
				}

				var itemType = (ItemType) iItemType;
				switch (itemType) {
					case ItemType.Bumper: {
						var item = new VisualPinball.Engine.VPT.Bumper.Bumper(reader, itemName);
						tableContainer.Add(item);
						break;
					}
					case ItemType.Decal: {
						tableContainer.Add(new VisualPinball.Engine.VPT.Decal.Decal(reader, itemName));
						break;
					}
					case ItemType.DispReel: {
						var item = new VisualPinball.Engine.VPT.DispReel.DispReel(reader, itemName);
						tableContainer.Add(item);
						break;
					}
					case ItemType.Flasher: {
						var item = new VisualPinball.Engine.VPT.Flasher.Flasher(reader, itemName);
						tableContainer.Add(item);
						break;
					}
					case ItemType.Flipper: {
						var item = new VisualPinball.Engine.VPT.Flipper.Flipper(reader, itemName);
						tableContainer.Add(item);
						break;
					}
					case ItemType.Gate: {
						var item = new VisualPinball.Engine.VPT.Gate.Gate(reader, itemName);
						tableContainer.Add(item);
						break;
					}
					case ItemType.HitTarget: {
						var item = new VisualPinball.Engine.VPT.HitTarget.HitTarget(reader, itemName);
						tableContainer.Add(item);
						break;
					}
					case ItemType.Kicker: {
						var item = new VisualPinball.Engine.VPT.Kicker.Kicker(reader, itemName);
						tableContainer.Add(item);
						break;
					}
					case ItemType.Light: {
						var item = new VisualPinball.Engine.VPT.Light.Light(reader, itemName);
						tableContainer.Add(item);
						break;
					}
					case ItemType.LightSeq: {
						var item = new VisualPinball.Engine.VPT.LightSeq.LightSeq(reader, itemName);
						tableContainer.Add(item);
						break;
					}
					case ItemType.Plunger: {
						var item = new VisualPinball.Engine.VPT.Plunger.Plunger(reader, itemName);
						tableContainer.Add(item);
						break;
					}
					case ItemType.Primitive: {
						var item = new Primitive.Primitive(reader, itemName);
						tableContainer.Add(item);
						break;
					}
					case ItemType.Ramp: {
						var item = new Ramp.Ramp(reader, itemName);
						tableContainer.Add(item);
						break;
					}
					case ItemType.Rubber: {
						var item = new Rubber.Rubber(reader, itemName);
						tableContainer.Add(item);
						break;
					}
					case ItemType.Spinner: {
						var item = new Spinner.Spinner(reader, itemName);
						tableContainer.Add(item);
						break;
					}
					case ItemType.Surface: {
						var item = new Surface.Surface(reader, itemName);
						tableContainer.Add(item);
						break;
					}
					case ItemType.TextBox: {
						var item = new TextBox.TextBox(reader, itemName);
						tableContainer.Add(item);
						break;
					}
					case ItemType.Timer: {
						var item = new Timer.Timer(reader, itemName);
						tableContainer.Add(item);
						break;
					}
					case ItemType.Trigger: {
						var item = new Trigger.Trigger(reader, itemName);
						tableContainer.Add(item);
						break;
					}
					case ItemType.Trough: {
						var item = new Trough.Trough(reader, itemName);
						tableContainer.Add(item);
						break;
					}
				}
			}
		}

		private static void LoadTextures(FileTableContainer tableContainer, CFStorage storage)
		{
			for (var i = 0; i < tableContainer.NumTextures; i++) {
				var textureName = $"Image{i}";
				storage.TryGetStream(textureName, out var textureStream);
				if (textureStream == null) {
					Logger.Warn("Could not find stream {0}, skipping.", textureName);
					continue;
				}
				var textureData = textureStream.GetData();
				if (textureData.Length < 4) {
					Logger.Warn("Skipping {itemName} because it has size of {itemDataLength}.", textureName, textureData.Length);
					continue;
				}

				using (var stream = new MemoryStream(textureData))
				using (var reader = new BinaryReader(stream)) {
					var texture = new Texture(reader, textureName);
					tableContainer.Textures[texture.Name.ToLower()] = texture;
				}
			}
		}

		private static void LoadCollections(FileTableContainer tableContainer, CFStorage storage)
		{
			for (var i = 0; i < tableContainer.NumCollections; i++) {
				var collectionName = $"Collection{i}";
				storage.TryGetStream(collectionName, out var collectionStream);
				if (collectionStream == null) {
					Logger.Warn("Could not find stream {0}, skipping.", collectionName);
					continue;
				}
				using (var stream = new MemoryStream(collectionStream.GetData()))
				using (var reader = new BinaryReader(stream)) {
					tableContainer.Collections.Add(new CollectionData(reader, collectionName));
				}
			}
		}

		private static void LoadMappings(FileTableContainer tableContainer, CFStorage gameStorage)
		{
			var name = "Mappings0";
			gameStorage.TryGetStream(name, out var citStream);
			if (citStream != null)
			{
				using (var stream = new MemoryStream(citStream.GetData()))
				using (var reader = new BinaryReader(stream)) {
					tableContainer.SetMappings(new Mappings.Mappings(reader, name));
				}
			}
		}

		private static void LoadSounds(FileTableContainer tableContainer, CFStorage storage, int fileVersion)
		{
			for (var i = 0; i < tableContainer.NumSounds; i++) {
				var soundName = $"Sound{i}";
				storage.TryGetStream(soundName, out var soundStream);
				if (soundStream == null) {
					Logger.Warn("Could not find stream {0}, skipping.", soundName);
					continue;
				}
				var soundData = soundStream.GetData();
				using (var stream = new MemoryStream(soundData))
				using (var reader = new BinaryReader(stream)) {
					var sound = new Sound.Sound(reader, soundName, fileVersion);
					tableContainer.Sounds[sound.Name.ToLower()] = sound;
				}
			}
		}

		private static void LoadTableInfo(FileTableContainer tableContainer, CFStorage rootStorage, CFStorage gameStorage)
		{
			// first, although we can loop through entries, get them from the game storage, so we
			// know their order, which is important when writing back (because you know, hashing).
			gameStorage.TryGetStream("CustomInfoTags", out var citStream);
			if (citStream != null) {
				using (var stream = new MemoryStream(citStream.GetData()))
				using (var reader = new BinaryReader(stream)) {
					tableContainer.CustomInfoTags.Load(reader);
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
						tableContainer.TableInfo[item.Name] = BiffUtil.ParseWideString(itemStream.GetData());
					}
				}
			}, false);
		}

		private static void LoadTableMeta(FileTableContainer tableContainer, CFStorage gameStorage)
		{
			// version
			gameStorage.TryGetStream("Version", out var versionBytes);
			if (versionBytes != null) {
				tableContainer.FileVersion = BitConverter.ToInt32(versionBytes.GetData(), 0);
			} else {
				Logger.Info("No Version under GameStg found, skipping.");
			}


			// hash
			gameStorage.TryGetStream("Version", out var hashBytes);
			if (hashBytes != null) {
				tableContainer.FileHash = hashBytes.GetData();
			} else {
				Logger.Info("No MAC under GameStg found, skipping.");
			}
		}
	}
}
