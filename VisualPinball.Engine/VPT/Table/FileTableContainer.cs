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
using NLog;

namespace VisualPinball.Engine.VPT.Table
{
	public class FileTableContainer : TableContainer
	{
		public FileTableContainer(string name = "Table1")
		{
			Table = new Table(this, new TableData { Name = name });
		}

		public FileTableContainer(BinaryReader reader)
		{
			Table = new Table(this, new TableData(reader));
		}

		private void AddItem<TItem>(string name, TItem item, IDictionary<string, TItem> d, bool updateStorageIndices) where TItem : IItem
		{
			if (updateStorageIndices) {
				item.StorageIndex = ItemDatas.Count();
				Table.Data.NumGameItems = item.StorageIndex + 1;
			}
			d[name] = item;
		}

		private void AddItem<TItem>(TItem item, ICollection<TItem> d, bool updateStorageIndices) where TItem : IItem
		{
			if (updateStorageIndices) {
				item.StorageIndex = ItemDatas.Count();
			}
			d.Add(item);
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

		/// <summary>
		/// Replaces all game items of a list with new game items.
		/// </summary>
		///
		/// <remarks>
		/// This only applied to Decals, because they are the only game items
		/// that don't have a name.
		/// </remarks>
		/// <param name="items">New list of game items</param>
		/// <typeparam name="T">Game item type (only Decals)</typeparam>
		/// <exception cref="ArgumentException">If not decals</exception>
		public void ReplaceAll<T>(IEnumerable<T> items) where T : IItem
		{
			var list = GetItemList<T>();
			if (list == null) {
				throw new ArgumentException("Cannot set all " + typeof(T) + "s (only Decals so far).");
			}
			list.Clear();
			list.AddRange(items);
		}

		public TData[] GetAllData<TItem, TData>() where TItem : Item<TData> where TData : ItemData
		{
			var dict = GetItemDictionary<TItem>();
			if (dict != null) {
				return dict.Values.Select(d => d.Data).ToArray();
			}
			var list = GetItemList<TItem>();
			if (list != null) {
				return list.Select(d => d.Data).ToArray();
			}
			throw new ArgumentException("Unknown item type " + typeof(TItem) + ".");
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

		public void SetTextureContainer(ITableResourceContainer<Texture> container)
		{
			Textures = container;
		}

		public override Texture GetTexture(string name)
		{
			var tex = name == null
				? null
				: Textures[name.ToLower()];
			return tex;
		}

		public void SetSoundContainer(ITableResourceContainer<Sound.Sound> container)
		{
			Sounds = container;
		}

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

	}
}

