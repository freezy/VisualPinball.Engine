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

// ReSharper disable InconsistentNaming

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{
	[Serializable]
	public class LibraryDatabase
	{
		public Dictionary<string, LibraryAsset2> Assets = new();
		public Dictionary<string, LibraryCategory2> Categories = new();

		public IEnumerable<(long, LibraryAsset2)> GetAssets(AssetQuery2 query)
		{
			var result = Assets.Values.Select(asset => (0L, asset));
			if (query.HasKeywords) {
				result = result.Select(hit => {
					var (score, asset) = hit;
					FuzzySearch.FuzzyMatch(query.Keywords, asset.Asset.name, ref score);
					return (score, asset);
				});
			}

			if (query.HasCategories) {
				result = result.Where(hit => query.HasCategory(hit.asset.Category));
			}

			return result.Where(hit => hit.Item1 > 0);
		}
	}

	public class AssetQuery2
	{
		public string Keywords;
		public bool HasKeywords => string.IsNullOrEmpty(Keywords);

		public LibraryCategory2[] Categories
		{
			get => _categories;
			set {
				_categories = value;
				_categoryIds = new HashSet<string>(value.Select(c => c.Id));
			}
		}

		private LibraryCategory2[] _categories;
		private HashSet<string> _categoryIds;
		public bool HasCategories => Categories is { Length: > 0 };
		public bool HasCategory(LibraryCategory2 category) => _categoryIds.Contains(category.Id);

		public Dictionary<string, string> Attributes = new();
	}

	[Serializable]
	public class LibraryAsset2
	{
		[SerializeReference]
		public Object Asset;

		public string Guid;
		public string Type;
		public string Path;
		public DateTime AddedAt;
		public string Description;
		private string _categoryId;

		[NonSerialized]
		public LibraryCategory2 Category;

		public List<LibraryAttribute2> Attributes;

		public Object LoadAsset() => AssetDatabase.LoadAssetAtPath(Path, AssetLibrary.TypeByName(Type));
	}

	[Serializable]
	public class LibraryCategory2
	{
		public string Id;
		public string Name;
	}

	[Serializable]
	public class LibraryAttribute2
	{
		public string Key;
		public string Value;
	}
}
