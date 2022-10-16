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
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
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

		public bool HasQuality(AssetQuality quality) => _quality == quality.ToString();

		private readonly List<AssetLibrary> _libraries;
		private string _keywords;
		private Dictionary<AssetLibrary, List<AssetCategory>> _categories;
		private readonly Dictionary<string, HashSet<string>> _attributes = new();
		private readonly HashSet<string> _tags = new();
		private string _quality = null;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private readonly Stopwatch _queryTime = new();

		public static string ValueToQuery(string value) => value.Contains(" ") ? value.Replace("\"", "in") : value;

		public static string QueryToValue(string query) => query.Contains(" ") ? query.Replace("in", "\"") : query;

		public AssetQuery(List<AssetLibrary> libraries)
		{
			_libraries = libraries;
		}

		public void Search(string q)
		{
			StartWatch();

			// parse attributes
			_attributes.Clear();
			const string quoted = "\"([\\w\\d\\s_/-]+)\"";
			const string nonQuoted = "([\\w\\d_/\"-]+)";
			var regexes = new[] {
				new Regex($"{quoted}:{quoted}"),
				new Regex($"{quoted}:{nonQuoted}"),
				new Regex($"{nonQuoted}:{quoted}"),
				new Regex($"{nonQuoted}:{nonQuoted}"),
			};
			foreach (var regex in regexes) {
				foreach (Match match in regex.Matches(q)) {
					var key = match.Groups[1].Value;
					if (!_attributes.ContainsKey(key)) {
						_attributes[key] = new HashSet<string>();
					}
					_attributes[key].Add(QueryToValue(match.Groups[2].Value));
					q = q.Replace(match.Value, "");
				}
			}

			_tags.Clear();
			var tagRegex = new Regex(@"\[([^\]]+)\]");
			foreach (Match match in tagRegex.Matches(q)) {
				if (!_tags.Contains(match.Groups[1].Value)) {
					_tags.Add(match.Groups[1].Value);
				}
				q = q.Replace(match.Value, "");
			}

			_quality = null;
			var qualityRegex = new Regex(@"\(([^\]]+)\)");
			foreach (Match match in qualityRegex.Matches(q)) {
				_quality = match.Groups[1].Value;
				q = q.Replace(match.Value, "");
				break;
			}

			// clean white spaces
			_keywords = Regex.Replace(q, @"\s+", " ").Trim();

			Run();
		}

		public void Filter(Dictionary<AssetLibrary, List<AssetCategory>> categories)
		{
			StartWatch();
			_categories = categories;
			Run();
		}

		public void Toggle(AssetLibrary lib)
		{
			StartWatch();
			if (lib.IsActive && !_libraries.Contains(lib)) {
				_libraries.Add(lib);
			}
			if (!lib.IsActive && _libraries.Contains(lib)) {
				_libraries.Remove(lib);
			}
			Run();
		}

		public string[] AttributeNames => _libraries
			.SelectMany(lib => lib.GetAttributeKeys())
			.Distinct()
			.OrderBy(x => x)
			.ToArray();

		public string[] TagNames => _libraries
			.SelectMany(lib => lib.GetAllTags())
			.Distinct()
			.OrderBy(x => x)
			.ToArray();

		public string[] AttributeValues(string attributeKey) => _libraries
			.SelectMany(lib => lib.GetAttributeValues(attributeKey))
			.Distinct()
			.ToArray();

		private void StartWatch()
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
						return lib.GetAssets(new LibraryQuery {
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
				.ToList();

			OnQueryUpdated?.Invoke(this, new AssetQueryResult(assets, _queryTime.ElapsedMilliseconds));
		}
	}

	public class AssetResult : IEquatable<AssetResult>
	{
		public readonly Asset Asset;
		public long Score;

		public AssetResult(Asset asset, long score)
		{
			Asset = asset;
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
			return $"[{Asset.Library.Name}] {Asset.Name}";
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
