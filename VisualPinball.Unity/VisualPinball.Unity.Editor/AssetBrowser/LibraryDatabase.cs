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
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// This class acts as the data layer for the asset library.
	///
	/// It's a separate class from <see cref="AssetLibrary"/>, because here we store the actual data.
	/// This is also the layer that can be replaced with a different storage method, while keeping the same
	/// API.
	///
	/// When we say "data", we mean the *meta data* that is associated with an actual asset (prefabs, materials,
	/// etc). This data is stored in a ScriptableObject per asset, the <see cref="Asset"/> class. These "meta
	/// assets" are stored in a separate "_database" folder, under the library root.
	///
	/// This class also contains the category definitions. Both categories and assets are stored as dictionaries,
	/// so we can more quickly check if a given asset is already added, quickly delete them, etc. Basically
	/// anything that accesses an asset or category by its ID.
	/// </summary>
	[Serializable]
	public class LibraryDatabase
	{
		private const string DatabaseFolder = "_database";

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
						foreach (var tag in result.Asset.Tags.Select(t => t.TagName)) {
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
					results = results.Where(result => result.Asset.Tags != null && result.Asset.Tags.Any(t => t.TagName == tag));
				}
			}

			return results;
		}

		public bool AddAsset(Object obj, AssetCategory category, AssetLibrary lib)
		{
			if (Assets.Contains(obj)) {
				var existingAsset = Assets.Get(obj);
				existingAsset.Category = category;
				return false;
			}

			var asset = ScriptableObject.CreateInstance<Asset>();
			asset.name = obj.name;
			asset.Object = obj;
			asset.Category = category;
			asset.Attributes = new List<AssetAttribute>();
			asset.Tags = new List<AssetTag>();
			asset.Links = new List<AssetLink>();
			asset.MaterialVariations = new List<AssetMaterialVariation>();
			asset.AddedAt = DateTime.Now;

			var assetMetaPath = AssetMetaPath(asset, lib);
			var assetMetaFolder = Path.GetDirectoryName(assetMetaPath);
			if (!Directory.Exists(assetMetaFolder)) {
				Directory.CreateDirectory(assetMetaFolder!);
			}
			AssetDatabase.CreateAsset(asset, assetMetaPath);
			Assets.Add(asset);

			return true;
		}

		public void RemoveAsset(Asset asset, AssetLibrary lib)
		{
			if (Assets.Contains(asset)) {
				Assets.Remove(asset);
				File.Delete(AssetMetaPath(asset, lib));
			}
		}

		private string AssetMetaPath(Asset asset, AssetLibrary lib) => $"{lib.LibraryRoot}/{DatabaseFolder}/{asset.GUID}.asset";

		#endregion

		#region Category

		public AssetCategory AddCategory(string categoryName)
		{
			var category = new AssetCategory {
				Id = Guid.NewGuid().ToString(),
				Name = categoryName
			};
			Categories.Add(category);

			return category;
		}

		public void RenameCategory(AssetCategory category, string newName)
		{
			category.Name = newName;
		}

		public AssetCategory GetCategory(string id) => Categories[id];

		public IEnumerable<AssetCategory> GetCategories() => Categories.Values.OrderBy(c => c.Name).ToList();

		public void SetCategory(Asset asset, AssetCategory category)
		{
			asset.Category = category;
		}

		public int NumAssetsWithCategory(AssetCategory category) => Assets.Values.Count(a => a.IsOfCategory(category));

		public void DeleteCategory(AssetCategory category)
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

		public AssetAttribute AddAttribute(Asset asset, string attributeName)
		{
			var attribute = new AssetAttribute {
				Key = attributeName,
				Value = string.Empty,
			};
			asset.Attributes.Add(attribute);
			return attribute;
		}

		#endregion

		#region Tags

		public IEnumerable<string> GetAllTags() => Assets.Values
			.SelectMany(a => a.Tags ?? new List<AssetTag>())
			.Select(at => at.TagName)
			.Distinct()
			.OrderBy(a => a);

		public IEnumerable<string> GetLinkNames() => Assets.Values
			.SelectMany(a => a.Links ?? new List<AssetLink>())
			.Select(al => al.Name)
			.Distinct()
			.OrderBy(a => a);

		public string AddTag(Asset asset, string tag)
		{
			var existingTag = asset.Tags.FirstOrDefault(t => t.TagName == tag);
			if (existingTag == null) {
				asset.Tags.Add(new AssetTag(tag));
			}
			return tag;
		}


		#endregion

		#region Links

		public AssetLink AddLink(Asset asset, string linkName)
		{
			var link = new AssetLink {
				Name = linkName,
				Url = "https://",
			};
			asset.Links ??= new List<AssetLink>();
			asset.Links.Add(link);
			return link;
		}

		#endregion
	}

	public enum AssetScale
	{
		World, Table
	}


	[Serializable]
	internal class Assets : SerializableDictionary<string, Asset>
	{
		public IEnumerable<Asset> All(LibraryDatabase lib) => Values.Select(v => v.SetCategory(lib));
		public void Add(Asset asset) => this[asset.GUID] = asset;
		public bool Contains(Asset asset) => ContainsKey(asset.GUID);
		public bool Contains(string guid) => ContainsKey(guid);
		public bool Contains(Object obj) => AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out var guid, out long _) && Contains(guid);
		public bool Remove(Asset asset) => Remove(asset.GUID);
		public Asset Get(Object obj) => AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out var guid, out long _) ? this[guid] : null;
	}

	[Serializable]
	internal class Categories : SerializableDictionary<string, AssetCategory>
	{
		public void Add(AssetCategory category) => this[category.Id] = category;
		public bool Remove(AssetCategory category) => Remove(category.Id);
	}

	[Serializable]
	internal class Tags : SerializableHashSet<string>
	{
	}
}
