// Visual Pinball Engine
// Copyright (C) 2022 freezy and VPE Team
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
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	internal class AssetQuery
	{
		public event EventHandler<AssetQueryResult> OnQueryUpdated;

		private readonly List<AssetLibrary> _libraries;
		private string _query;
		private Dictionary<AssetLibrary, List<LibraryCategory>> _categories;

		public AssetQuery(List<AssetLibrary> libraries)
		{
			_libraries = libraries;
		}

		public void Search(string q)
		{
			_query = q;
			Run();
		}

		public void Filter(Dictionary<AssetLibrary, List<LibraryCategory>> categories)
		{
			_categories = categories;
			Run();
		}

		public void Toggle(AssetLibrary lib, bool enabled)
		{
			if (enabled && !_libraries.Contains(lib)) {
				_libraries.Add(lib);
			}
			if (!enabled && _libraries.Contains(lib)) {
				_libraries.Remove(lib);
			}
			Run();
		}

		public void Run()
		{
			var assets = _libraries
				.SelectMany(lib => {
					try {
						// if categories are set but none exist of this lib, skip entire lib.
						if (_categories != null && !_categories.ContainsKey(lib)) {
							return Array.Empty<AssetData>();
						}
						return lib.GetAssets(
							_query,
							_categories != null && _categories.ContainsKey(lib) ? _categories[lib] : null
						).Select(asset => new AssetData(lib, asset));

					} catch (Exception e) {
						Debug.LogError($"Error reading assets from {lib.Name}, maybe corruption? ({e.Message})");
						// old data or whatever, just don't crash here.
						return Array.Empty<AssetData>();
					}
				})
				.ToList();

			OnQueryUpdated?.Invoke(this, new AssetQueryResult(assets));
		}
	}

	public class AssetData
	{
		public readonly AssetLibrary Library;
		public readonly LibraryAsset Asset;

		public AssetData(AssetLibrary library, LibraryAsset asset)
		{
			Library = library;
			Asset = asset;
		}
	}
}
