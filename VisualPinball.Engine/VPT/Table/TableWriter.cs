﻿using System;
using System.Linq;
using OpenMcdf;
using VisualPinball.Engine.IO;

namespace VisualPinball.Engine.VPT.Table
{
	public class TableWriter
	{
		private const int VpFileFormatVersion = 1060;

		private readonly Table _table;

		private CompoundFile _cf;
		private CFStorage _gameStorage;

		public TableWriter(Table table)
		{
			_table = table;
		}

		public void WriteTable(string fileName)
		{
			using (var hashWriter = new HashWriter()) {

				_cf = new CompoundFile();
				_gameStorage = _cf.RootStorage.AddStorage("GameStg");

				// 1. version
				WriteStream(_gameStorage, "Version", BitConverter.GetBytes(VpFileFormatVersion), hashWriter);

				// 2. table info
				WriteTableInfo(hashWriter);

				// 3. game items
				WriteGameItems(hashWriter);

				// 4. the rest, which isn't hashed.
				WriteImages();
				WriteSounds();

				// finally write hash
				WriteStream(_gameStorage, "MAC", hashWriter.Hash());

				_cf.Save(fileName);
				_cf.Close();
			}
		}

		private void WriteTableInfo(HashWriter hashWriter)
		{
			var tableInfo = _cf.RootStorage.AddStorage("TableInfo");

			// order for the hashing is important here.
			var knownTags = new[] {
				"TableName", "AuthorName", "TableVersion", "ReleaseDate", "AuthorEmail",
				"AuthorWebSite", "TableBlurb", "TableDescription", "TableRules", "Screenshot"
			};

			// 1. write known tags
			foreach (var tag in knownTags) {
				WriteInfoTag(tableInfo, tag, hashWriter);
			}

			// 2. write custom tag names
			_table.CustomInfoTags?.WriteData(_gameStorage, hashWriter);

			// 3. write custom tags
			foreach (var tag in _table.CustomInfoTags?.TagNames ?? Array.Empty<string>()) {
				WriteInfoTag(tableInfo, tag, hashWriter);
			}
		}

		private void WriteInfoTag(CFStorage tableInfo, string tag, HashWriter hashWriter)
		{
			if (!_table.TableInfo.ContainsKey(tag)) {
				return;
			}
			WriteStream(tableInfo, tag, BiffUtil.GetWideString(_table.TableInfo[tag]), hashWriter);
		}

		private void WriteGameItems(HashWriter hashWriter)
		{
			// again, the order is important, because we're hashing at the same time.

			// 1. game data
			_table.Data.WriteData(_gameStorage, hashWriter);

			// 2. game items
			foreach (var writeable in _table.GameItems.OrderBy(gi => gi.StorageIndex)) {
				writeable.WriteData(_gameStorage);
			}

			// 3. Collections
			var collections = _table.Collections.Values.ToArray();
			foreach (var collection in collections.Select(c => c.Data).OrderBy(c => c.StorageIndex)) {
				collection.WriteData(_gameStorage, hashWriter);
			}
		}

		private void WriteImages()
		{
			int i = 0;
			foreach (var texture in _table.Textures.Values) {
				texture.Data.StorageName = $"Image{i++}";
				texture.Data.WriteData(_gameStorage);
			}
		}

		private void WriteSounds()
		{
			foreach (var sound in _table.Sounds.Values) {
				sound.Data.WriteData(_gameStorage);
			}
		}

		private static void WriteStream(CFStorage storage, string streamName, byte[] data, HashWriter hashWriter = null)
		{
			storage.AddStream(streamName).SetData(data);
			hashWriter?.Write(data);
		}
	}
}
