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
		[SerializeField] private Assets Assets = new();
		[SerializeField] private Categories Categories = new();

		#region Assets

		public IEnumerable<AssetResult> GetAssets(LibraryQuery query)
		{
			var results = Assets.All(this).Select(asset => new AssetResult(asset, 0L));
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

			if (query.HasQuality && Enum.TryParse(query.Quality, out AssetQuality quality)) {
				results = results.Where(result => result.Asset.Quality == quality);
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
			asset.Library = lib;
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

		public bool AddAsset(Asset asset, AssetLibrary lib)
		{
			if (Assets.Contains(asset.Object)) {
				return false;
			}
			var assetMetaPath = AssetMetaPath(asset, lib);
			var assetMetaFolder = Path.GetDirectoryName(assetMetaPath);
			if (!Directory.Exists(assetMetaFolder)) {
				Directory.CreateDirectory(assetMetaFolder!);
			}
			Assets.Add(asset);

			return true;
		}

		public void RemoveAsset(Asset asset)
		{
			if (Assets.Contains(asset)) {
				Assets.Remove(asset);
				foreach (var materialCombination in AssetMaterialCombination.GetCombinations(asset)) { // includes the original
					if (File.Exists(materialCombination.ThumbPath)) {
						File.Delete(materialCombination.ThumbPath);
					}
				}
				File.Delete(AssetMetaPath(asset, asset.Library));
				File.Delete(AssetMetaPath(asset, asset.Library) + ".meta");
			}
		}

		public bool MoveAsset(Asset asset, AssetLibrary destLibrary)
		{
			if (Assets.Contains(asset)) {

				// first, move material combination thumbs before we switch the reference to the library.
				foreach (var materialCombination in AssetMaterialCombination.GetCombinations(asset)) { // includes the original
					materialCombination.MoveThumb(destLibrary);
				}
				// move the .asset file to the new library.
				var assetSrc = AssetMetaPath(asset, asset.Library);
				var assetDest = AssetMetaPath(asset, destLibrary);

				Debug.Log($"Moving database asset {assetSrc} to {assetDest}");
				var error = AssetDatabase.MoveAsset(assetSrc, assetDest);
				if (!string.IsNullOrEmpty(error)) {
					Debug.LogError($"Could not move asset {assetSrc} to {assetDest}: {error}");
				}

				// move the reference in the database.
				destLibrary.AddAsset(asset);

				// finally, remove the reference from the old library.
				Assets.Remove(asset);
				return true;
			}

			if (destLibrary.HasAsset(asset.GUID)) {
				if (asset.Library != destLibrary) {
					Debug.Log($"Updated asset's library reference from {asset.Library.Name} to {destLibrary.Name}.");
					asset.Library = destLibrary;

				} else {
					Debug.LogWarning($"Asset {asset.Name} ({asset.GUID}) already exists in {destLibrary.Name}. Cannot move.");
				}
				return false;
			}

			Debug.LogWarning($"Database does not contain asset {asset.Name} ({asset.GUID}). Cannot move to {destLibrary.Name}.");
			return false;
		}

		public bool HasAsset(string guid) => Assets.Contains(guid);
		public Asset GetAsset(string guid) => Assets[guid];
		private string AssetMetaPath(Asset asset, AssetLibrary lib) => $"{lib.DatabaseRoot}/{asset.GUID}.asset";

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

		public AssetCategory GetCategory(string id) => Categories.ContainsKey(id) ? Categories[id] : null;

		public AssetCategory GetCategoryByName(string name) => Categories.Values.First(c=> c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

		public IEnumerable<AssetCategory> GetCategories() => Categories.Values.OrderBy(c => c.Name).ToList();

		public bool HasCategory(string name) => Categories.Contains(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

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

	[Serializable]
	internal class Assets : SerializableDictionary<string, Asset>
	{
		public IEnumerable<Asset> All(LibraryDatabase lib) => Keys.Where(k => {
			if (this[k] == null) {
				Debug.LogWarning($"Asset with ID {k} is null.");
				return false;
			}
			return true;
		}).Select(k => this[k].SetCategory(lib));
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

		public bool Contains(Func<AssetCategory, bool> predicate)
		{
			return Values.Any(predicate);
		}
	}

	[Serializable]
	internal class Tags : SerializableHashSet<string>
	{
	}
}
