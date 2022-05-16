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
using System.Text.RegularExpressions;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// This class queries all loaded libraries and returns a merged result of assets.
	/// </summary>
	public class AssetQuery
	{
		public event EventHandler<AssetQueryResult> OnQueryUpdated;

		private readonly List<AssetLibrary> _libraries;
		private string _query;
		private Dictionary<AssetLibrary, List<LibraryCategory>> _categories;
		private readonly Dictionary<string, string> _attributes = new();
		private readonly HashSet<string> _tags = new();

		public AssetQuery(List<AssetLibrary> libraries)
		{
			_libraries = libraries;
		}

		public void Search(string q)
		{
			// parse attributes
			_attributes.Clear();
			foreach (var regex in new []{ new Regex(@"(\w+):(\w+)"), new Regex("\"([\\w\\s]+)\":(\\w+)"), new Regex("(\\w+):\"([\\w\\s]+)\""), new Regex("\"([\\w\\s]+)\":\"([\\w\\s]+)\"") }) {
				foreach (Match match in regex.Matches(q)) {
					_attributes[match.Groups[1].Value] = match.Groups[2].Value;
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

			// clean white spaces
			_query = Regex.Replace(q, @"\s+", " ").Trim();

			Run();
		}

		public void Filter(Dictionary<AssetLibrary, List<LibraryCategory>> categories)
		{
			_categories = categories;
			Run();
		}

		public void Toggle(AssetLibrary lib)
		{
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

		private void Run()
		{
			var assets = _libraries
				.SelectMany(lib => {
					try {
						// if categories are set but none exist of this lib, skip entire lib.
						if (_categories is { Count: > 0 } && !_categories.ContainsKey(lib)) {
							return Array.Empty<AssetResult>();
						}
						return lib.GetAssets(
							_query,
							_categories != null && _categories.ContainsKey(lib) ? _categories[lib] : null,
							_attributes,
							_tags
						);

					} catch (Exception e) {
						Debug.LogError($"Error reading assets from {lib.Name}, maybe corruption? ({e.Message})\n{e.StackTrace}");
						// old data or whatever, just don't crash here.
						return Array.Empty<AssetResult>();
					}
				})
				.OrderBy(r => r.Score)
				.ToList();

			OnQueryUpdated?.Invoke(this, new AssetQueryResult(assets));
		}
	}

	public class AssetResult : IEquatable<AssetResult>
	{
		public readonly AssetLibrary Library;
		public readonly LibraryAsset Asset;
		public long Score;

		public AssetResult(AssetLibrary library, LibraryAsset asset, long score)
		{
			Library = library;
			Asset = asset;
			Score = score;
		}

		public void Save()
		{
			Library.SaveAsset(Asset);
		}

		#region IEquatable

		public bool Equals(AssetResult other)
		{
			if (ReferenceEquals(null, other))
				return false;
			if (ReferenceEquals(this, other))
				return true;
			return Equals(Library, other.Library) && Equals(Asset, other.Asset);
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
			return HashCode.Combine(Library, Asset);
		}

		#endregion

	}
}
