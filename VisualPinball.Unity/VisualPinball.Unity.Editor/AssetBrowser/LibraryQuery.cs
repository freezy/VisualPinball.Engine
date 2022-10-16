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

using System.Collections.Generic;
using System.Linq;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// This class defines a subset of assets from the asset library. Use it whenever
	/// you need to retrieve assets from the asset library.
	/// </summary>
	public class LibraryQuery
	{
		#region Keywords

		public string Keywords;
		public bool HasKeywords => !string.IsNullOrEmpty(Keywords);

		#endregion

		#region Categories

		public List<AssetCategory> Categories
		{
			get => _categories;
			set {
				_categories = value;
				_categoryIds = value == null
					? new HashSet<string>()
					: new HashSet<string>(value.Select(c => c.Id));
			}
		}

		private List<AssetCategory> _categories;
		private HashSet<string> _categoryIds;
		public bool HasCategories => Categories is { Count: > 0 };
		public bool HasCategory(AssetCategory category) => _categoryIds.Contains(category.Id);

		#endregion

		#region Attributes

		public Dictionary<string, HashSet<string>> Attributes = new();

		public bool HasAttributes => Attributes.Count > 0;

		#endregion

		#region Tags

		public HashSet<string> Tags = new();

		public bool HasTags => Tags.Count > 0;

		#endregion

		#region Quality

		public string Quality;

		public bool HasQuality => !string.IsNullOrEmpty(Quality);

		#endregion
	}
}
