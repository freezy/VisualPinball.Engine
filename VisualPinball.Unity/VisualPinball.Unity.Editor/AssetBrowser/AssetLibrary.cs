﻿// Visual Pinball Engine
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
using LiteDB;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// This class handles the data layer of one asset library.
	/// </summary>
	[CreateAssetMenu(fileName = "Library", menuName = "Visual Pinball/Asset Library", order = 300)]
	public class AssetLibrary : ScriptableObject, ISerializationCallbackReceiver, IDisposable
	{
		public string Name;

		public string LibraryRoot;

		private const string CollectionAssets = "assets";
		public const string CollectionCategories = "categories";

		private LiteDatabase _db {
			get {
				if (_dbInstance != null) {
					return _dbInstance;
				}
				_dbInstance = new LiteDatabase(_dbPath);
				return _dbInstance;
			}
		}

		private void OnDestroy()
		{
			Dispose();
		}

		public void Dispose()
		{
			_dbInstance?.Dispose();
			_dbInstance = null;
		}

		private string _dbPath {
			get {
				var thisPath = AssetDatabase.GetAssetPath(this);
				return Path.GetDirectoryName(thisPath) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(thisPath) + ".db";
			}
		}

		private LiteDatabase _dbInstance;

		public bool AddAsset(string guid, Type type, string path, LibraryCategory category = null, List<LibraryAttribute> attrs = null)
		{
			var collection = _db.GetCollection<LibraryAsset>(CollectionAssets);

			var existingAsset = collection.FindOne(x => x.Guid == guid);
			if (existingAsset != null) {
				existingAsset.Type = type.ToString();
				existingAsset.Path = path;
				existingAsset.Category = category;
				collection.Update(existingAsset);
				return false;
			}

			var asset = new LibraryAsset {
				Guid = guid,
				Type = type.ToString(),
				Path = path,
				Category = category,
				Attributes = attrs ?? new List<LibraryAttribute>(),
				AddedAt = DateTime.Now,
			};

			collection.EnsureIndex(x => x.Guid, true);
			collection.EnsureIndex(x => x.Type);
			collection.EnsureIndex(x => x.Category);
			collection.Insert(asset);

			return true;
		}

		#region Asset

		public void SaveAsset(LibraryAsset asset)
		{
			_db.GetCollection<LibraryAsset>(CollectionAssets).Update(asset);
		}

		public IEnumerable<LibraryAsset> GetAssets(string query = null, List<LibraryCategory> categories = null)
		{
			var assets = _db.GetCollection<LibraryAsset>(CollectionAssets);
			var q = assets.Query();
			if (!string.IsNullOrWhiteSpace(query)) {
				q = q.Where(a => a.Path.Contains(query, StringComparison.OrdinalIgnoreCase));
			}
			if (categories != null) {
				// SELECT $ FROM assets WHERE Category.$id IN [{"$guid": "7292885c-c6e5-4b6b-9fa1-fd2916784fed"}, {"$guid": "94a58b38-96ab-4b0c-8955-7d787b64333a"}];
				var categoryIds = categories.Select(c => c.Id);
				q.Where(a => categoryIds.Contains(a.Category.Id));
			}
			return q
				.Include(a => a.Category)
				.ToList();
		}

		#endregion

		#region Category

		public LibraryCategory AddCategory(string categoryName)
		{
			var categories = _db.GetCollection<LibraryCategory>(CollectionCategories);
			var category = new LibraryCategory {
				Name = categoryName
			};
			categories.Insert(category);
			return category;
		}

		public void RenameCategory(LibraryCategory category, string newName)
		{
			var categories = _db.GetCollection<LibraryCategory>(CollectionCategories);
			category.Name = newName;
			categories.Update(category);
		}

		public void SetCategory(LibraryAsset asset, LibraryCategory category)
		{
			var assets = _db.GetCollection<LibraryAsset>(CollectionAssets);
			asset.Category.Id = category.Id;
			assets.Upsert(asset);
		}

		public int NumAssetsWithCategory(LibraryCategory category) => _db.GetCollection<LibraryAsset>(CollectionAssets)
			.Query()
			.Where(a => a.Category.Id == category.Id)
			.Count();

		public void DeleteCategory(LibraryCategory category)
		{
			if (NumAssetsWithCategory(category) > 0) {
				throw new InvalidOperationException("Cannot delete category when there are assigned assets.");
			}
			_db.GetCollection<LibraryCategory>(CollectionCategories).Delete(category.Id);
		}

		public IEnumerable<LibraryCategory> GetCategories()
		{
			var collection = _db.GetCollection<LibraryCategory>(CollectionCategories);
			return collection.Query().OrderBy(c => c.Name).ToList();
		}

		#endregion

		#region Attribute

		public IEnumerable<string> GetAttributeKeys()
		{
			var assets = _db.GetCollection<LibraryAsset>(CollectionAssets);
			return assets.Query().ToList()
				.SelectMany(a => a.Attributes)
				.Select(a => a.Key)
				.Distinct();
		}

		public IEnumerable<string> GetAttributeValues(string key)
		{
			var assets = _db.GetCollection<LibraryAsset>(CollectionAssets);
			return assets.Query().ToList()
				.SelectMany(a => a.Attributes)
				.Where(a => a.Key == key && !string.IsNullOrEmpty(a.Value))
				.SelectMany(a => a.Value.Split(','))
				.Select(v => v.Trim())
				.Distinct();
		}

		public LibraryAttribute AddAttribute(LibraryAsset asset, string attributeName)
		{
			var assets = _db.GetCollection<LibraryAsset>(CollectionAssets);
			var attribute = new LibraryAttribute {
				Key = attributeName,
				Value = string.Empty,
			};
			asset.Attributes.Add(attribute);
			assets.Upsert(asset);
			return attribute;
		}

		#endregion

		public void OnBeforeSerialize()
		{
			if (string.IsNullOrEmpty(LibraryRoot)) {
				LibraryRoot = Path.GetDirectoryName(AssetDatabase.GetAssetPath(this));
			}
		}

		public void OnAfterDeserialize()
		{
		}

		private static readonly Dictionary<string, Type> Types = new();

		public static Type TypeByName(string name)
		{
			if (Types.ContainsKey(name)) {
				return Types[name];
			}
			Types[name] = AppDomain.CurrentDomain.GetAssemblies().Reverse().Select(assembly => assembly.GetType(name)).FirstOrDefault(tt => tt != null);
			return Types[name];
		}
	}

	public class LibraryAsset
	{
		[BsonId]
		public Guid Id { get; set; }
		public string Guid { get; set; }
		public string Type { get; set; }
		public string Path { get; set; }
		public DateTime AddedAt { get; set; }
		[BsonRef(AssetLibrary.CollectionCategories)]

		public string Description { get; set; }
		public LibraryCategory Category { get; set; }
		public List<LibraryAttribute> Attributes { get; set; }

		public UnityEngine.Object LoadAsset() => AssetDatabase.LoadAssetAtPath(Path, AssetLibrary.TypeByName(Type));
	}

	public class LibraryCategory
	{
		[BsonId]
		public Guid Id { get; set; }
		public string Name { get; set; }
	}

	public class LibraryAttribute
	{
		public string Key { get; set; }
		public string Value { get; set; }
	}
}
