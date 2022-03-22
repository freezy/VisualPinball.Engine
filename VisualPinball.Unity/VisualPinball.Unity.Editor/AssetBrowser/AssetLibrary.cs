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
using LiteDB;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	[CreateAssetMenu(fileName = "Library", menuName = "Visual Pinball/Asset Library", order = 300)]
	public class AssetLibrary : ScriptableObject, ISerializationCallbackReceiver, IDisposable
	{
		public string Name;

		public string LibraryRoot;

		private const string CollectionAssets = "assets";
		private const string CollectionCategories = "categories";

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

		public IEnumerable<LibraryAsset> GetAssets(string query = null)
		{
			var collection = _db.GetCollection<LibraryAsset>(CollectionAssets);
			var q = collection.Query();
			if (!string.IsNullOrWhiteSpace(query)) {
				q = q.Where(a => a.Path.Contains(query, StringComparison.OrdinalIgnoreCase));
			}
			return q.ToList();
		}

		public IEnumerable<LibraryCategory> GetCategories()
		{
			var collection = _db.GetCollection<LibraryCategory>(CollectionCategories);
			return collection.Query().ToList();
		}

		public void OnBeforeSerialize()
		{
			if (string.IsNullOrEmpty(LibraryRoot)) {
				LibraryRoot = Path.GetDirectoryName(AssetDatabase.GetAssetPath(this));
			}
		}

		public void OnAfterDeserialize()
		{
		}
	}

	public class LibraryAsset
	{
		public string Guid { get; set; }
		public string Type { get; set; }
		public string Path { get; set; }
		public DateTime AddedAt { get; set; }
		public LibraryCategory Category { get; set; }
		public List<LibraryAttribute> Attributes { get; set; }
	}

	public class LibraryCategory
	{
		[BsonId]
		public ObjectId Id;
		public string Name { get; set; }
	}

	public class LibraryAttribute
	{
		[BsonId]
		public ObjectId Id;
		public string Key { get; set; }
		public string Value { get; set; }
	}
}
