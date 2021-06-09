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
using NLog;
using VisualPinball.Engine.VPT.Collection;

namespace VisualPinball.Engine.VPT.Table
{
	public class FileTableContainer : TableContainer
	{
		public override Table Table { get; }
		public override Dictionary<string, string> TableInfo { get; } = new Dictionary<string, string>();
		public override List<CollectionData> Collections { get; } = new List<CollectionData>();
		public override Mappings.Mappings Mappings => _mappings;
		public override CustomInfoTags CustomInfoTags { get; } = new CustomInfoTags();

		private Mappings.Mappings _mappings = new Mappings.Mappings();

		public FileTableContainer(string name = "Table1")
		{
			Table = new Table(this, new TableData { Name = name });
		}

		public FileTableContainer(BinaryReader reader)
		{
			Table = new Table(this, new TableData(reader));
		}

		public void SetMappings(Mappings.Mappings mappings)
		{
			_mappings = mappings;
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

