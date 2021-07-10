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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OpenMcdf;
using VisualPinball.Engine.IO;

namespace VisualPinball.Engine.VPT.Table
{
	public class TableWriter
	{
		private const int VpFileFormatVersion = 1060;

		private readonly TableContainer _tableContainer;

		private CompoundFile _cf;
		private CFStorage _gameStorage;

		public TableWriter(TableContainer tableContainer)
		{
			_tableContainer = tableContainer;
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
				WriteTextures();
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
			_tableContainer.CustomInfoTags?.WriteData(_gameStorage, hashWriter);

			// 3. write custom tags
			foreach (var tag in _tableContainer.CustomInfoTags?.TagNames ?? Array.Empty<string>()) {
				WriteInfoTag(tableInfo, tag, hashWriter);
			}
		}

		private void WriteInfoTag(CFStorage tableInfo, string tag, HashWriter hashWriter)
		{
			if (!_tableContainer.TableInfo.ContainsKey(tag)) {
				return;
			}
			WriteStream(tableInfo, tag, BiffUtil.GetWideString(_tableContainer.TableInfo[tag]), hashWriter);
		}

		private void WriteGameItems(HashWriter hashWriter)
		{
			// again, the order is important, because we're hashing at the same time.

			// 1. game data
			_tableContainer.Table.Data.WriteData(_gameStorage, hashWriter);

			// 2. game items
			foreach (var gameItem in _tableContainer.ItemDatas.OrderBy(gi => gi.StorageIndex)) {

				#if !WRITE_VP106 && !WRITE_VP107
				gameItem.WriteData(_gameStorage);
				#else
					if (gameItem.IsVpCompatible) {
						gameItem.WriteData(_gameStorage);
					}
				#endif
			}

			// 3. Collections
			var collections = _tableContainer.Collections;
			foreach (var collection in collections.OrderBy(c => c.StorageIndex)) {
				collection.WriteData(_gameStorage, hashWriter);
			}

			// 5. Mappings
			#if !WRITE_VP106 && !WRITE_VP107
			_tableContainer.Mappings.Data.WriteData(_gameStorage);
			#endif
		}

		private void WriteTextures()
		{
			int i = 0;
			foreach (var texture in _tableContainer.Textures) {
				texture.Data.StorageIndex = i++;
				texture.Data.WriteData(_gameStorage);
			}
		}

		private void WriteSounds()
		{
			int i = 0;
			foreach (var sound in _tableContainer.Sounds) {
				sound.Data.StorageIndex = i++;
				sound.Data.WriteData(_gameStorage);
			}
		}

		private static void WriteStream(CFStorage storage, string streamName, byte[] data, HashWriter hashWriter = null)
		{
			storage.AddStream(streamName).SetData(data);
			hashWriter?.Write(data);
		}

		private static IEnumerable<MemberInfo> GetMembersWithAttribute<TAttr>(ItemData data) where TAttr: Attribute
		{
			return data.GetType()
				.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
				.Where(member => member.GetCustomAttribute<TAttr>() != null);
		}

		private static T GetValue<T>(MemberInfo memberInfo, ItemData data)
		{
			switch (memberInfo.MemberType) {
				case MemberTypes.Field:
					return (T)((FieldInfo)memberInfo).GetValue(data);
				case MemberTypes.Property:
					return (T)((PropertyInfo)memberInfo).GetValue(data);
				default:
					throw new NotImplementedException();
			}
		}

		private static void SetValue<T>(MemberInfo memberInfo, ItemData data, T value)
		{
			switch (memberInfo.MemberType) {
				case MemberTypes.Field:
					((FieldInfo)memberInfo).SetValue(data, value);
					break;
				case MemberTypes.Property:
					((PropertyInfo)memberInfo).SetValue(data, value);
					break;
				default:
					throw new NotImplementedException();
			}
		}
	}
}
