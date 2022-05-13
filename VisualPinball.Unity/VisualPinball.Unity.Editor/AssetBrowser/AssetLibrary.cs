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
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// This class handles the data layer of one asset library.
	/// </summary>
	[CreateAssetMenu(fileName = "Library", menuName = "Visual Pinball/Asset Library", order = 300)]
	public class AssetLibrary : ScriptableObject, ISerializationCallbackReceiver
	{
		[HideInInspector]
		public string Id;

		public string Name => name;

		public string LibraryRoot;

		public bool IsLocked;

		[SerializeField]
		private LibraryDatabase _db;

		public event EventHandler OnChange;

		[NonSerialized]
		public bool IsActive;

		#region Asset

		public void AddAssets((string guid, LibraryCategory category)[] assets)
		{
			if (IsLocked) {
				throw new InvalidOperationException($"Cannot add new asset because library {Name} is locked.");
			}

			RecordUndo("add assets to library");
			foreach (var (guid, category) in assets) {
				_db.AddAsset(guid, category);
			}
			SaveLibrary();
		}

		public bool AddAsset(string guid, LibraryCategory category)
		{
			if (IsLocked) {
				throw new InvalidOperationException($"Cannot add new asset because library {Name} is locked.");
			}

			RecordUndo("add asset to library");
			var wasAdded = _db.AddAsset(guid, category);
			SaveLibrary();

			return wasAdded;
		}

		public void SaveAsset(LibraryAsset asset)
		{
			if (IsLocked) {
				throw new InvalidOperationException($"Cannot update asset {asset.Guid} because library {Name} is locked.");
			}
			SaveLibrary();
		}

		public IEnumerable<AssetResult> GetAssets(string query, List<LibraryCategory> categories, Dictionary<string, string> attributes)
		{
			var q = new AssetQuery2 {
				Keywords = query,
				Categories = categories,
				Attributes = attributes
			};

			return _db.GetAssets(this, q);
		}

		public void RemoveAsset(LibraryAsset asset)
		{
			if (IsLocked) {
				throw new InvalidOperationException($"Cannot delete asset because library {Name} is locked.");
			}

			RecordUndo("remove asset from library");
			_db.RemoveAsset(asset);
			SaveLibrary();
		}

		#endregion

		#region Category

		public LibraryCategory AddCategory(string categoryName)
		{
			if (IsLocked) {
				throw new InvalidOperationException($"Cannot add category {categoryName} because library {Name} is locked.");
			}

			RecordUndo("add category to library");
			var category = _db.AddCategory(categoryName);
			SaveLibrary();

			return category;
		}

		public void RenameCategory(LibraryCategory category, string newName)
		{
			if (IsLocked) {
				throw new InvalidOperationException($"Cannot rename category {category.Name} because library {Name} is locked.");
			}

			RecordUndo("rename category");
			_db.RenameCategory(category, newName);
			SaveLibrary();
		}

		public void SetCategory(LibraryAsset asset, LibraryCategory category)
		{
			if (IsLocked) {
				throw new InvalidOperationException($"Cannot assign category {category.Name} because library {Name} is locked.");
			}

			RecordUndo("assign category");
			_db.SetCategory(asset, category);
			SaveLibrary();
		}

		public int NumAssetsWithCategory(LibraryCategory category) => _db.NumAssetsWithCategory(category);

		public void DeleteCategory(LibraryCategory category)
		{
			if (IsLocked) {
				throw new InvalidOperationException($"Cannot delete category {category.Name} because library {Name} is locked.");
			}

			RecordUndo("delete category");
			_db.DeleteCategory(category);
			SaveLibrary();
		}

		public IEnumerable<LibraryCategory> GetCategories() => _db.GetCategories();

		#endregion

		#region Attribute

		public IEnumerable<string> GetAttributeKeys() => _db.GetAttributeKeys();

		public IEnumerable<string> GetAttributeValues(string key) => _db.GetAttributeValues(key);

		public LibraryAttribute AddAttribute(LibraryAsset asset, string attributeName)
		{
			if (IsLocked) {
				throw new InvalidOperationException($"Cannot add new attribute to asset {asset.Guid} because library {Name} is locked.");
			}

			RecordUndo("add attribute");
			var attr = _db.AddAttribute(asset, attributeName);
			SaveLibrary();

			return attr;
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
