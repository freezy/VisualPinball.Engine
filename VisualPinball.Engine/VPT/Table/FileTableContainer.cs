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
using System.IO;
using System.Linq;
using VisualPinball.Engine.VPT.Collection;

namespace VisualPinball.Engine.VPT.Table
{
	public class FileTableContainer : TableContainer
	{
		public override Table Table { get; }
		public override Dictionary<string, string> TableInfo { get; } = new Dictionary<string, string>();
		public override List<CollectionData> Collections { get; } = new List<CollectionData>();
		public override CustomInfoTags CustomInfoTags { get; } = new CustomInfoTags();
		public override IEnumerable<Texture> Textures => _textures.Values;
		public override IEnumerable<Sound.Sound> Sounds => _sounds.Values;

		private readonly Dictionary<string, Texture> _textures = new Dictionary<string, Texture>();
		private readonly Dictionary<string, Sound.Sound> _sounds = new Dictionary<string, Sound.Sound>();


		public FileTableContainer(string name = "Table1")
		{
			Table = new Table(this, new TableData { Name = name });
		}

		public FileTableContainer(BinaryReader reader)
		{
			Table = new Table(this, new TableData(reader));
		}

		/// <summary>
		/// Adds a game item to the table.
		/// </summary>
		/// <param name="item">Game item instance</param>
		/// <param name="updateStorageIndices">If set, re-computes the storage indices. Only needed when adding game items via the editor.</param>
		/// <typeparam name="T">Game item type</typeparam>
		/// <exception cref="ArgumentException">Whe type of game item is unknown</exception>
		public void Add<T>(T item, bool updateStorageIndices = false) where T : IItem
		{
			var dict = GetItemDictionary<T>();
			if (dict != null) {
				AddItem(item.Name, item, dict, updateStorageIndices);

			} else {
				var list = GetItemList<T>();
				if (list != null) {
					AddItem(item, list, updateStorageIndices);

				} else {
					throw new ArgumentException("Unknown item type " + typeof(T) + ".");
				}
			}
		}

		private void AddItem<TItem>(string name, TItem item, IDictionary<string, TItem> d, bool updateStorageIndices) where TItem : IItem
		{
			if (updateStorageIndices) {
				item.StorageIndex = ItemDatas.Count();
				Table.Data.NumGameItems = item.StorageIndex + 1;
			}
			d[name.ToLower()] = item;
		}

		private void AddItem<TItem>(TItem item, ICollection<TItem> d, bool updateStorageIndices) where TItem : IItem
		{
			if (updateStorageIndices) {
				item.StorageIndex = ItemDatas.Count();
			}
			d.Add(item);
		}

		/// <summary>
		/// The API to load the table from a file.
		/// </summary>
		/// <param name="filename">Path to the VPX file</param>
		/// <param name="loadGameItems">If false, game items are not loaded. Useful when loading them on multiple threads.</param>
		/// <returns>The parsed table</returns>
		public static FileTableContainer Load(string filename, bool loadGameItems = true)
		{
			return TableLoader.Load(filename, loadGameItems);
		}

		public override Material GetMaterial(string name)
		{
			if (Table.Data.Materials == null || name == null) {
				return null;
			}

			// ReSharper disable once LoopCanBeConvertedToQuery
			foreach (var t in Table.Data.Materials) {
				if (t.Name == name) {
					return t;
				}
			}

			return null;
		}

		public override Texture GetTexture(string name)
		{
			var tex = name == null || !_textures.ContainsKey(name.ToLower())
				? null
				: _textures[name.ToLower()];
			return tex;
		}

		public int AddTexture(Texture texture)
		{
			_textures[texture.Name.ToLower()] = texture;
			return _textures.Count;
		}

		public Sound.Sound GetSound(string name)
		{
			var snd = name == null || !_sounds.ContainsKey(name.ToLower())
				? null
				: _sounds[name.ToLower()];
			return snd;
		}

		public void AddSound(Sound.Sound sound)
		{
			_sounds[sound.Name.ToLower()] = sound;
		}
	}
}

