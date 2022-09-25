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
using UnityEditor.Presets;
using UnityEditor.Search;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{
	[Serializable]
	public class LibraryDatabase
	{
		[SerializeField] private Assets Assets = new();
		[SerializeField] private Categories Categories = new();

		#region Assets

		public IEnumerable<AssetResult> GetAssets(AssetLibrary lib, LibraryQuery query)
		{
			var results = Assets.All(this).Select(asset => new AssetResult(lib, asset, 0L));
			if (query.HasKeywords) {
				results = results
					.Select(result => {
						FuzzySearch.FuzzyMatch(query.Keywords, result.Asset.Object.name, ref result.Score);
						foreach (var tag in result.Asset.Tags) {
							var score = 0L;
							FuzzySearch.FuzzyMatch(query.Keywords, tag, ref score);
							result.AddScore(score);
						}
						foreach (var value in result.Asset.Attributes.SelectMany(values => values.Value.Split(","))) {
							var score = 0L;
							FuzzySearch.FuzzyMatch(query.Keywords, value, ref score);
							result.AddScore(score);
						}
						return result;
					})
					.Where(result => result.Score > 0);
			}

			if (query.HasCategories) {
				results = results.Where(result => query.HasCategory(result.Asset.Category));
			}

			// do the attribute search after the query.
			if (query.HasAttributes) {
				foreach (var (attrKey, attrValues) in query.Attributes) {
					foreach (var attrValue in attrValues) {
						results = results.Where(result => result.Asset.Attributes != null && result.Asset.Attributes.Any(at => {
							var keyMatches = string.Equals(at.Key, attrKey, StringComparison.CurrentCultureIgnoreCase);
							var valueMatches = attrValue != null && at.Value != null && at.Value.ToLower().Contains(attrValue.ToLower());
							return attrValue != null ? keyMatches && valueMatches : keyMatches;
						}));
					}
				}
			}

			// tag filter
			if (query.HasTags) {
				foreach (var tag in query.Tags) {
					results = results.Where(result => result.Asset.Tags != null && result.Asset.Tags.Contains(tag));
				}
			}

			return results;
		}

		public bool AddAsset(Object obj, LibraryCategory category)
		{
			if (Assets.Contains(obj)) {
				var existingAsset = Assets.Get(obj);
				existingAsset.Category = category;
				return false;
			}

			var asset = new LibraryAsset {
				Object = obj,
				Category = category,
				Attributes = new List<LibraryKeyValue>(),
				AddedAt = DateTime.Now,
			};
			Assets.Add(asset);

			return true;
		}

		public void RemoveAsset(LibraryAsset asset)
		{
			if (Assets.Contains(asset)) {
				Assets.Remove(asset);
			}
		}

		#endregion

		#region Category

		public LibraryCategory AddCategory(string categoryName)
		{
			var category = new LibraryCategory {
				Id = Guid.NewGuid().ToString(),
				Name = categoryName
			};
			Categories.Add(category);

			return category;
		}

		public void RenameCategory(LibraryCategory category, string newName)
		{
			category.Name = newName;
		}

		public LibraryCategory GetCategory(string id) => Categories[id];

		public IEnumerable<LibraryCategory> GetCategories() => Categories.Values.OrderBy(c => c.Name).ToList();

		public void SetCategory(LibraryAsset asset, LibraryCategory category)
		{
			asset.Category = category;
		}

		public int NumAssetsWithCategory(LibraryCategory category) => Assets.Values.Count(a => a.IsOfCategory(category));

		public void DeleteCategory(LibraryCategory category)
		{
			if (NumAssetsWithCategory(category) > 0) {
				throw new InvalidOperationException("Cannot delete category when there are assigned assets.");
			}

			Categories.Remove(category);
		}

		#endregion

		#region Attribute

		public IEnumerable<string> GetAttributeKeys() => Assets.Values
			.SelectMany(a => a.Attributes)
			.Select(a => a.Key)
			.Distinct()
			.OrderBy(a => a);

		public IEnumerable<string> GetAttributeValues(string key) => Assets.Values
			.SelectMany(a => a.Attributes)
			.Where(a => a.Key == key && !string.IsNullOrEmpty(a.Value))
			.SelectMany(a => a.Value.Split(','))
			.Select(v => v.Trim())
			.Distinct()
			.OrderBy(a => a);

		public LibraryKeyValue AddAttribute(LibraryAsset asset, string attributeName)
		{
			var attribute = new LibraryKeyValue {
				Key = attributeName,
				Value = string.Empty,
			};
			asset.Attributes.Add(attribute);
			return attribute;
		}

		#endregion

		#region Tags

		public IEnumerable<string> GetAllTags() => Assets.Values
			.SelectMany(a => a.Tags?.Keys ?? Array.Empty<string>())
			.Distinct()
			.OrderBy(a => a);

		public IEnumerable<string> GetLinkNames() => Assets.Values
			.SelectMany(a => a.Links != null ? a.Links.Select(l => l.Key) : Array.Empty<string>())
			.Distinct()
			.OrderBy(a => a);

		public string AddTag(LibraryAsset asset, string tag)
		{
			if (!asset.Tags.Contains(tag)) {
				asset.Tags.Add(tag);
			}
			return tag;
		}


		#endregion

		#region Links

		public LibraryKeyValue AddLink(LibraryAsset asset, string linkName)
		{
			var link = new LibraryKeyValue {
				Key = linkName,
				Value = "https://",
			};
			asset.Links ??= new List<LibraryKeyValue>();
			asset.Links.Add(link);
			return link;
		}

		#endregion
	}

	public class LibraryQuery
	{
		#region Keywords

		public string Keywords;
		public bool HasKeywords => !string.IsNullOrEmpty(Keywords);

		#endregion

		#region Categories

		public List<LibraryCategory> Categories
		{
			get => _categories;
			set {
				_categories = value;
				_categoryIds = value == null
					? new HashSet<string>()
					: new HashSet<string>(value.Select(c => c.Id));
			}
		}

		private List<LibraryCategory> _categories;
		private HashSet<string> _categoryIds;
		public bool HasCategories => Categories is { Count: > 0 };
		public bool HasCategory(LibraryCategory category) => _categoryIds.Contains(category.Id);

		#endregion

		#region Attributes

		public Dictionary<string, HashSet<string>> Attributes = new();

		public bool HasAttributes => Attributes.Count > 0;

		#endregion

		#region Tags

		public HashSet<string> Tags = new();

		public bool HasTags => Tags.Count > 0;

		#endregion
	}

	public enum AssetScale
	{
		World, Table
	}

	[Serializable]
	public class LibraryAsset
	{
		public string Name => Object != null ? Object.name : "<invalid ref>";

		[SerializeReference]
		public Object Object;

		public DateTime AddedAt {
			get => Convert.ToDateTime(_addedAt);
			set => _addedAt = value.ToString("o");
		}
		[SerializeField]
		private string _addedAt;

		public string Description;

		[SerializeField]
		public AssetScale Scale = AssetScale.World;

		[SerializeReference]
		public Preset ThumbCameraPreset;

		[SerializeField]
		private string _categoryId;

		public LibraryCategory Category {
			get => _category;
			set {
				_category = value;
				_categoryId = value.Id;
			}
		}
		[NonSerialized]
		private LibraryCategory _category;

		public List<LibraryKeyValue> Attributes;
		public LibraryAsset SetCategory(LibraryDatabase lib)
		{
			_category = lib.GetCategory(_categoryId);
			return this;
		}

		public bool IsOfCategory(LibraryCategory category) => _categoryId == category.Id;

		public List<LibraryKeyValue> Links;

		[SerializeField]
		internal Tags Tags;
	}

	[Serializable]
	public class LibraryCategory
	{
		public string Id;
		public string Name;

		public override string ToString() => $"LibraryCategory: {Name} ({Id})";
	}

	[Serializable]
	public class LibraryKeyValue
	{
		public string Key;
		public string Value;
	}

	[Serializable]
	internal class Assets : SerializableDictionary<string, LibraryAsset>
	{
		public IEnumerable<LibraryAsset> All(LibraryDatabase lib) => Values.Select(v => v.SetCategory(lib));
		public void Add(LibraryAsset asset)
		{
			if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset.Object, out var guid, out long _)) {
				this[guid] = asset;
			}
		}
		public bool Contains(LibraryAsset asset)
		{
			return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset.Object, out var guid, out long _) && ContainsKey(guid);
		}

		public bool Contains(string guid) => ContainsKey(guid);
		public bool Remove(LibraryAsset asset)
		{
			return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset.Object, out var guid, out long _) && Remove(guid);
		}
		public LibraryAsset Get(Object obj)
		{
			return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out var guid, out long _) ? this[guid] : null;
		}
	}

	[Serializable]
	internal class Categories : SerializableDictionary<string, LibraryCategory>
	{
		public void Add(LibraryCategory category) => this[category.Id] = category;
		public bool Remove(LibraryCategory category) => Remove(category.Id);
	}

	[Serializable]
	internal class Tags : SerializableHashSet<string>
	{
	}
}
