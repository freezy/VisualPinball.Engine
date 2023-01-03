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
using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// This is the root node of the asset library.
	///
	/// The data itself is stored in a sub object, <see cref="_db"/>. This sub object contains
	/// references to the asset meta data, as well as the categories.
	/// </summary>
	[CreateAssetMenu(fileName = "Library", menuName = "Visual Pinball/Asset Library", order = 300)]
	public class AssetLibrary : ScriptableObject, ISerializationCallbackReceiver
	{
		[HideInInspector]
		public string Id;

		public string Name => name;

		public string LibraryRoot;

		public bool IsLocked;

		public Preset DefaultThumbCameraPreset;

		[SerializeField]
		private LibraryDatabase _db;

		public event EventHandler OnChange;

		[NonSerialized]
		public bool IsActive;

		#region Asset

		public bool AddAsset(Object obj, AssetCategory category)
		{
			if (IsLocked) {
				throw new InvalidOperationException($"Cannot add new asset because library {Name} is locked.");
			}

			RecordUndo("add asset to library");
			var wasAdded = _db.AddAsset(obj, category, this);
			SaveLibrary();

			return wasAdded;
		}

		public void SaveAsset(Asset asset)
		{
			if (IsLocked) {
				throw new InvalidOperationException($"Cannot update asset {asset.Name} because library {Name} is locked.");
			}
			SaveLibrary();
		}

		public IEnumerable<AssetResult> GetAssets(LibraryQuery q) => _db.GetAssets(this, q);

		public void RemoveAsset(Asset asset)
		{
			if (IsLocked) {
				throw new InvalidOperationException($"Cannot delete asset because library {Name} is locked.");
			}

			RecordUndo("remove asset from library");
			_db.RemoveAsset(asset);
			SaveLibrary();
		}

		public bool HasAsset(string guid) => _db.HasAsset(guid);
		public Asset GetAsset(string guid) => _db.GetAsset(guid);

		#endregion

		#region Category

		public AssetCategory AddCategory(string categoryName)
		{
			if (IsLocked) {
				throw new InvalidOperationException($"Cannot add category {categoryName} because library {Name} is locked.");
			}

			RecordUndo("add category to library");
			var category = _db.AddCategory(categoryName);
			SaveLibrary();

			return category;
		}

		public void RenameCategory(AssetCategory category, string newName)
		{
			if (IsLocked) {
				throw new InvalidOperationException($"Cannot rename category {category.Name} because library {Name} is locked.");
			}

			RecordUndo("rename category");
			_db.RenameCategory(category, newName);
			SaveLibrary();
		}

		public void SetCategory(Asset asset, AssetCategory category)
		{
			if (IsLocked) {
				throw new InvalidOperationException($"Cannot assign category {category.Name} because library {Name} is locked.");
			}

			RecordUndo("assign category");
			_db.SetCategory(asset, category);
			SaveLibrary();
		}

		public int NumAssetsWithCategory(AssetCategory category) => _db.NumAssetsWithCategory(category);

		public void DeleteCategory(AssetCategory category)
		{
			if (IsLocked) {
				throw new InvalidOperationException($"Cannot delete category {category.Name} because library {Name} is locked.");
			}

			RecordUndo("delete category");
			_db.DeleteCategory(category);
			SaveLibrary();
		}

		public IEnumerable<AssetCategory> GetCategories() => _db.GetCategories();

		#endregion

		#region Attribute

		public IEnumerable<string> GetAttributeKeys() => _db?.GetAttributeKeys() ?? Array.Empty<string>();
		public IEnumerable<string> GetAllTags() => _db?.GetAllTags() ?? Array.Empty<string>();
		public IEnumerable<string> GetLinkNames() => _db?.GetLinkNames() ?? Array.Empty<string>();

		public IEnumerable<string> GetAttributeValues(string key) => _db?.GetAttributeValues(key) ?? Array.Empty<string>();

		public AssetAttribute AddAttribute(Asset asset, string attributeName)
		{
			if (IsLocked) {
				throw new InvalidOperationException($"Cannot add new attribute to asset {asset.Name} because library {Name} is locked.");
			}

			RecordUndo("add attribute");
			var attr = _db.AddAttribute(asset, attributeName);
			SaveLibrary();

			return attr;
		}

		#endregion

		#region Links

		public AssetLink AddLink(Asset asset, string linkName)
		{
			if (IsLocked) {
				throw new InvalidOperationException($"Cannot add new link to asset {asset.Name} because library {Name} is locked.");
			}

			RecordUndo("add link");
			var link = _db.AddLink(asset, linkName);
			SaveLibrary();

			return link;
		}

		public string AddTag(Asset asset, string tagName)
		{
			if (IsLocked) {
				throw new InvalidOperationException($"Cannot add new tag to asset {asset.Name} because library {Name} is locked.");
			}

			RecordUndo("add tag");
			var tag = _db.AddTag(asset, tagName);
			SaveLibrary();

			return tag;
		}

		#endregion

		#region Lifecycle

		public void OnBeforeSerialize()
		{
			// set default path to asset's location
			if (string.IsNullOrEmpty(LibraryRoot)) {
				var path = AssetDatabase.GetAssetPath(this);
				if (!string.IsNullOrEmpty(path)) {
					LibraryRoot = Path.GetDirectoryName(path);
				}
			}

			// generate id
			if (string.IsNullOrEmpty(Id)) {
				Id =  $"{Guid.NewGuid().ToString()}";
			}

			// create database
			_db ??= new LibraryDatabase();
		}

		public void OnAfterDeserialize()
		{
		}

		// private void OnValidate()
		// {
		// 	OnChange?.Invoke(this, EventArgs.Empty);
		// }

		#endregion

		#region Persistance

		private void RecordUndo(string message)
		{
			Undo.RecordObject(this, message);
		}

		private void SaveLibrary()
		{
			EditorUtility.SetDirty(this);
			AssetDatabase.SaveAssetIfDirty(this);
		}

		#endregion

	}
}
