// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
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
using UnityEditor.Search;

namespace VisualPinball.Unity.Editor
{
	internal sealed class AssetSearchIndex
	{
		private readonly List<IndexedAsset> _assets;

		public AssetSearchIndex(AssetLibrary library)
		{
			_assets = library.GetAllAssets().Select(asset => new IndexedAsset(asset)).ToList();
		}

		public IEnumerable<AssetResult> Search(LibraryQuery query)
		{
			IEnumerable<IndexedAsset> results = _assets;
			if (query.HasCategories) {
				var categoryIds = new HashSet<string>(query.Categories.Select(category => category.Id));
				results = results.Where(asset => categoryIds.Contains(asset.CategoryId));
			}

			if (query.HasAttributes) {
				foreach (var (key, values) in query.Attributes) {
					foreach (var value in values) {
						results = results.Where(asset => asset.HasAttribute(key, value));
					}
				}
			}

			if (query.HasTags) {
				foreach (var tag in query.Tags) {
					results = results.Where(asset => asset.Tags.Contains(tag));
				}
			}

			if (query.HasQuality && Enum.TryParse(query.Quality, true, out AssetQuality quality)) {
				results = results.Where(asset => asset.Quality == quality);
			}

			return query.HasKeywords
				? results.Select(asset => asset.Match(query.Keywords)).Where(result => result.Score > 0)
				: results.Select(asset => new AssetResult(asset.Asset, 0L, asset.Name));
		}

		public IEnumerable<string> AttributeNames => _assets
			.SelectMany(asset => asset.Attributes.Keys)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.OrderBy(value => value);

		public IEnumerable<string> TagNames => _assets
			.SelectMany(asset => asset.Tags)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.OrderBy(value => value);

		public IEnumerable<string> AttributeValues(string key) => _assets
			.Where(asset => asset.Attributes.ContainsKey(key))
			.SelectMany(asset => asset.Attributes[key])
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.OrderBy(value => value);

		private sealed class IndexedAsset
		{
			public readonly Asset Asset;
			public readonly string Name;
			public readonly string CategoryId;
			public readonly AssetQuality Quality;
			public readonly Dictionary<string, string[]> Attributes = new(StringComparer.OrdinalIgnoreCase);
			public readonly HashSet<string> Tags = new(StringComparer.OrdinalIgnoreCase);

			private readonly string[] _searchValues;

			public IndexedAsset(Asset asset)
			{
				Asset = asset;
				Name = asset.Name;
				CategoryId = asset.Category?.Id;
				Quality = asset.Quality;
				foreach (var attribute in (IEnumerable<AssetAttribute>)asset.Attributes ?? Enumerable.Empty<AssetAttribute>()) {
					if (string.IsNullOrEmpty(attribute.Key)) {
						continue;
					}
					var values = (attribute.Value ?? string.Empty)
						.Split(',')
						.Select(value => value.Trim())
						.Where(value => value.Length > 0)
						.ToArray();
					if (Attributes.TryGetValue(attribute.Key, out var existing)) {
						Attributes[attribute.Key] = existing.Concat(values).ToArray();
					} else {
						Attributes[attribute.Key] = values;
					}
				}
				foreach (var tag in (IEnumerable<AssetTag>)asset.Tags ?? Enumerable.Empty<AssetTag>()) {
					if (!string.IsNullOrEmpty(tag.TagName)) {
						Tags.Add(tag.TagName);
					}
				}
				_searchValues = Tags.Concat(Attributes.Values.SelectMany(values => values)).ToArray();
			}

			public bool HasAttribute(string key, string value)
			{
				if (!Attributes.TryGetValue(key, out var values)) {
					return false;
				}
				return value == null || values.Any(candidate => candidate.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0);
			}

			public AssetResult Match(string keywords)
			{
				var result = new AssetResult(Asset, 0L, Name);
				FuzzySearch.FuzzyMatch(keywords, Name, ref result.Score);
				foreach (var value in _searchValues) {
					var score = 0L;
					FuzzySearch.FuzzyMatch(keywords, value, ref score);
					result.AddScore(score);
				}
				return result;
			}
		}
	}
}
