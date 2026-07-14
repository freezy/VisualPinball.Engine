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
			using (var cf = RootStorage.OpenRead(filename, StorageModeFlags.None)) {
				var gameStorage = cf.OpenStorage("GameStg");
				var gameData = gameStorage.OpenStream("GameData");

				var fileVersion = BitConverter.ToInt32(gameStorage.OpenStream("Version").ReadAll(), 0);
				using (var stream = new MemoryStream(gameData.ReadAll()))
				using (var reader = new BinaryReader(stream)) {
					var tableContainer = new FileTableContainer(reader);

					var tableInfoStorage = LoadTableInfo(tableContainer, cf, gameStorage);
					if (loadGameItems) {
						LoadGameItems(tableContainer, gameStorage, tableContainer.NumGameItems, "GameItem");
						LoadGameItems(tableContainer, gameStorage, tableContainer.NumVpeGameItems, "VpeGameItem");
					}
					LoadTextures(tableContainer, gameStorage, tableInfoStorage);
					LoadSounds(tableContainer, gameStorage, fileVersion);
					LoadCollections(tableContainer, gameStorage);
					LoadTableMeta(tableContainer, gameStorage);

					return tableContainer;
				}
			}
		}

		public static IEnumerable<byte[]> ReadGameItems(string fileName, int numGameItems, string storagePrefix)
		{
			var gameItemData = new byte[numGameItems][];
			using (var cf = RootStorage.OpenRead(fileName, StorageModeFlags.None)) {
				var storage = cf.OpenStorage("GameStg");
				for (var i = 0; i < numGameItems; i++) {
					var itemName = $"{storagePrefix}{i}";
					var itemStream = storage.OpenStream(itemName);
					gameItemData[i] = itemStream.ReadAll();
				}
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
				case ItemType.Trough: item = new Trough.Trough(reader, $"VpeGameItem{storageIndex}"); break;
				case ItemType.MetalWireGuide: item = new MetalWireGuide.MetalWireGuide(reader, itemName); break;
				default:
					Logger.Info("Unhandled item type " + itemType);
					itemType = ItemType.Invalid; break;
			}
		}

		private static void LoadGameItems(FileTableContainer tableContainer, Storage storage, int count, string storagePrefix)
		{
			for (var i = 0; i < count; i++) {
				var itemName = $"{storagePrefix}{i}";
				storage.TryOpenStream(itemName, out var itemStream);
				if (itemStream == null) {
					Logger.Warn("Could not find stream {0}, skipping.", itemName);
					continue;
				}
				var itemData = itemStream.ReadAll();
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
					case ItemType.MetalWireGuide:
					{
						var item = new MetalWireGuide.MetalWireGuide(reader, itemName);
						tableContainer.Add(item);
						break;
					}
				}
			}
		}

		private static void LoadTextures(FileTableContainer tableContainer, Storage storage, Storage tableInfoStorage)
		{
			for (var i = 0; i < tableContainer.NumTextures; i++) {
				var textureName = $"Image{i}";
				storage.TryOpenStream(textureName, out var textureStream);
				if (textureStream == null) {
					Logger.Warn("Could not find stream {0}, skipping.", textureName);
					continue;
				}
				var textureData = textureStream.ReadAll();
				if (textureData.Length < 4) {
					Logger.Warn("Skipping {itemName} because it has size of {itemDataLength}.", textureName, textureData.Length);
					continue;
				}

				using (var stream = new MemoryStream(textureData))
				using (var reader = new BinaryReader(stream)) {
					var texture = new Texture(reader, textureName, tableInfoStorage);
					tableContainer.AddTexture(texture);
				}
			}
		}

		private static void LoadCollections(FileTableContainer tableContainer, Storage storage)
		{
			for (var i = 0; i < tableContainer.NumCollections; i++) {
				var collectionName = $"Collection{i}";
				storage.TryOpenStream(collectionName, out var collectionStream);
				if (collectionStream == null) {
					Logger.Warn("Could not find stream {0}, skipping.", collectionName);
					continue;
				}
				using (var stream = new MemoryStream(collectionStream.ReadAll()))
				using (var reader = new BinaryReader(stream)) {
					tableContainer.Collections.Add(new CollectionData(reader, collectionName));
				}
			}
		}

		private static void LoadSounds(FileTableContainer tableContainer, Storage storage, int fileVersion)
		{
			for (var i = 0; i < tableContainer.NumSounds; i++) {
				var soundName = $"Sound{i}";
				storage.TryOpenStream(soundName, out var soundStream);
				if (soundStream == null) {
					Logger.Warn("Could not find stream {0}, skipping.", soundName);
					continue;
				}
				var soundData = soundStream.ReadAll();
				using (var stream = new MemoryStream(soundData))
				using (var reader = new BinaryReader(stream)) {
					var sound = new Sound.Sound(reader, soundName, fileVersion);
					tableContainer.AddSound(sound);
				}
			}
		}

		private static Storage LoadTableInfo(FileTableContainer tableContainer, Storage rootStorage, Storage gameStorage)
		{
			// first, although we can loop through entries, get them from the game storage, so we
			// know their order, which is important when writing back (because you know, hashing).
			gameStorage.TryOpenStream("CustomInfoTags", out var citStream);
			if (citStream != null) {
				using (var stream = new MemoryStream(citStream.ReadAll()))
				using (var reader = new BinaryReader(stream)) {
					tableContainer.CustomInfoTags.Load(reader);
				}
			}

			// now actually read them in
			rootStorage.TryOpenStorage("TableInfo", out var tableInfoStorage);
			if (tableInfoStorage == null) {
				Logger.Info("TableInfo storage not found, skipping.");
				return null;
			}
			foreach (var item in tableInfoStorage.EnumerateEntries()) {
				if (item.Name == "Screenshot") { // skip those
					continue;
				}
				if (item.Type == EntryType.Stream) {
					var itemStream = tableInfoStorage.OpenStream(item.Name);
					tableContainer.TableInfo[item.Name] = BiffUtil.ParseWideString(itemStream.ReadAll());
				}
			}

			return tableInfoStorage;
		}

		private static void LoadTableMeta(FileTableContainer tableContainer, Storage gameStorage)
		{
			// version
			gameStorage.TryOpenStream("Version", out var versionBytes);
			if (versionBytes != null) {
				tableContainer.FileVersion = BitConverter.ToInt32(versionBytes.ReadAll(), 0);
			} else {
				Logger.Info("No Version under GameStg found, skipping.");
			}


			// hash
			gameStorage.TryOpenStream("MAC", out var hashBytes);
			if (hashBytes != null) {
				tableContainer.FileHash = hashBytes.ReadAll();
			} else {
				Logger.Info("No MAC under GameStg found, skipping.");
			}
		}
	}
}
