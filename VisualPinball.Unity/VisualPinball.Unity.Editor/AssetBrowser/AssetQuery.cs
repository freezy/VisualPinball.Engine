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
using System.Diagnostics;
using System.Linq;
using System.Text;
using NLog;
using Unity.Mathematics;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// This class queries all loaded libraries and returns a merged result of assets.
	/// </summary>
	public class AssetQuery
	{
		public event EventHandler<AssetQueryResult> OnQueryUpdated;

		public bool HasTag(string tag) => _tags.Contains(tag);
		public bool HasAttribute(string attrKey, string attrValue) => _attributes.ContainsKey(attrKey) && _attributes[attrKey].Contains(attrValue);

		public bool HasQuality(AssetQuality quality) => string.Equals(_quality, quality.ToString(), StringComparison.OrdinalIgnoreCase);

		private readonly List<AssetLibrary> _libraries;
		private readonly Dictionary<AssetLibrary, AssetSearchIndex> _indexes = new();
		private string _keywords;
		private Dictionary<AssetLibrary, List<AssetCategory>> _categories;
		private Dictionary<string, HashSet<string>> _attributes = new(StringComparer.OrdinalIgnoreCase);
		private HashSet<string> _tags = new(StringComparer.OrdinalIgnoreCase);
		private string _quality = null;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private readonly Stopwatch _queryTime = new();

		public static string ValueToQuery(string value) => value
			.Replace("\\", "\\\\")
			.Replace("\"", "\\\"");

		public static string QueryToValue(string query)
		{
			var value = new StringBuilder(query.Length);
			for (var i = 0; i < query.Length; i++) {
				if (query[i] == '\\' && i + 1 < query.Length && (query[i + 1] == '\\' || query[i + 1] == '"')) {
					value.Append(query[++i]);
				} else {
					value.Append(query[i]);
				}
			}
			return value.ToString();
		}

		public AssetQuery(List<AssetLibrary> libraries)
		{
			_libraries = libraries;
			foreach (var library in libraries) {
				_indexes[library] = new AssetSearchIndex(library);
			}
		}

		public void Search(string q)
		{
			StartTimer();

			var parsed = AssetQueryTokenizer.Parse(q ?? string.Empty);
			_attributes = parsed.Attributes;
			_tags = parsed.Tags;
			_quality = parsed.Quality;
			_keywords = parsed.Keywords;

			Run();
		}

		public void Filter(Dictionary<AssetLibrary, List<AssetCategory>> categories)
		{
			StartTimer();
			_categories = categories;
			Run();
		}

		public void Toggle(AssetLibrary lib)
		{
			StartTimer();
			if (lib.IsActive && !_libraries.Contains(lib)) {
				_libraries.Add(lib);
				_indexes[lib] = new AssetSearchIndex(lib);
			}
			if (!lib.IsActive && _libraries.Contains(lib)) {
				_libraries.Remove(lib);
			}
			Run();
		}

		public void Reindex(AssetLibrary library)
		{
			if (_libraries.Contains(library)) {
				_indexes[library] = new AssetSearchIndex(library);
			}
		}

		public string[] AttributeNames => _libraries
			.SelectMany(lib => _indexes[lib].AttributeNames)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.OrderBy(x => x)
			.ToArray();

		public string[] TagNames => _libraries
			.SelectMany(lib => _indexes[lib].TagNames)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.OrderBy(x => x)
			.ToArray();

		public string[] AttributeValues(string attributeKey) => _libraries
			.SelectMany(lib => _indexes[lib].AttributeValues(attributeKey))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();

		private void StartTimer()
		{
			_queryTime.Restart();
		}

		private void Run()
		{
			var assets = _libraries
				.SelectMany(lib => {
					try {
						// if categories are set but none exist of this lib, skip entire lib.
						if (_categories is { Count: > 0 } && !_categories.ContainsKey(lib)) {
							return Array.Empty<AssetResult>();
						}
						return _indexes[lib].Search(new LibraryQuery {
							Keywords = _keywords,
							Categories = _categories != null && _categories.ContainsKey(lib) ? _categories[lib] : null,
							Attributes = _attributes,
							Tags = _tags,
							Quality = _quality
						});

					} catch (Exception e) {
						Logger.Error($"Error reading assets from {lib.Name}, maybe corruption? ({e.Message})\n{e.StackTrace}");
						// old data or whatever, just don't crash here.
						return Array.Empty<AssetResult>();
					}
				})
				.OrderBy(r => r.Score)
				.ThenBy(r => r.Name)
				.ToList();

			OnQueryUpdated?.Invoke(this, new AssetQueryResult(assets, _queryTime.ElapsedMilliseconds));
		}
	}

	public class AssetResult : IEquatable<AssetResult>
	{
		public readonly Asset Asset;
		public readonly string Name;
		public long Score;

		public AssetResult(Asset asset, long score, string name = null)
		{
			Asset = asset;
			Name = name ?? asset.Name;
			Score = score;
		}

		public void AddScore(long score)
		{
			Score = Score < 0
				? math.max(Score, score)
				: math.max(Score, Score + score);
		}

		public override string ToString()
		{
			return $"[{Asset.Library.Name}] {Name}";
		}

		#region IEquatable

		public bool Equals(AssetResult other)
		{
			if (ReferenceEquals(null, other))
				return false;
			if (ReferenceEquals(this, other))
				return true;
			return Equals(Asset, other.Asset);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj))
				return false;
			if (ReferenceEquals(this, obj))
				return true;
			return obj.GetType() == GetType() && Equals((AssetResult)obj);
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(Asset);
		}

		#endregion
	}
}
