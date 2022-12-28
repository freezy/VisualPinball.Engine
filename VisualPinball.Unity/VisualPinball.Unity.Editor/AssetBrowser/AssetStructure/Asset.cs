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
using UnityEngine;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// This class describes the meta data of a library asset. It also references the actual
	/// library asset. It's the entity you'll see in the asset browser, so anything library
	/// related goes through that class.
	/// </summary>
	public class Asset : ScriptableObject
	{
		public string Name => Object != null ? Object.name : "<invalid ref>";

		[SerializeReference]
		public AssetLibrary Library;

		[SerializeReference]
		public Object Object;

		[SerializeField]
		private string _categoryId;

		[SerializeField]
		private string _addedAt = DateTime.Now.ToString("o");

		[SerializeField]
		public string Description;

		[SerializeField]
		public List<AssetAttribute> Attributes;

		[SerializeField]
		public List<AssetTag> Tags;

		[SerializeField]
		public List<AssetLink> Links;

		[SerializeField]
		[NonReorderable] // see https://answers.unity.com/questions/1828499/nested-class-lists-inspector-overlapping-bug.html
		public List<AssetMaterialVariation> MaterialVariations;

		[SerializeField]
		public string ThumbBackgroundObjectName;
		
		[SerializeReference]
		public Preset ThumbCameraPreset;

		[SerializeField]
		public float ThumbCameraHeight;

		[SerializeField]
		public bool ThumbTopLight;
		
		[SerializeField]
		public bool UnpackPrefab;

		[SerializeReference]
		public AssetQuality Quality = AssetQuality.Measured;

		[NonSerialized]
		private AssetCategory _category;

		public DateTime AddedAt {
			get => string.IsNullOrEmpty(_addedAt) ? DateTime.Now : Convert.ToDateTime(_addedAt);
			set => _addedAt = value.ToString("o");
		}

		public string GUID {
			get {
				if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(Object, out var guid, out long _)) {
					return guid;
				}
				throw new Exception($"Could not get GUID from {Object.name}");
			}
		}

		public Asset SetCategory(LibraryDatabase lib)
		{
			_category = lib.GetCategory(_categoryId);
			return this;
		}

		public AssetCategory Category {
			get => _category;
			set {
				_category = value;
				_categoryId = value.Id;
			}
		}

		public bool IsOfCategory(AssetCategory category) => _categoryId == category.Id;

		public void Save()
		{
			EditorUtility.SetDirty(this);
			AssetDatabase.SaveAssetIfDirty(this);
			Library.SaveAsset(this);
		}

		public void AddAttribute(string key, string value)
		{
			var attr = Attributes.FirstOrDefault(a => a.Key == key);
			if (attr == null) {
				Attributes.Add(new AssetAttribute(key, value));
			} else {
				AddValuesToAttribute(attr, value);
			}
		}

		public void ReplaceAttribute(string key, string value)
		{
			var attr = Attributes.FirstOrDefault(a => a.Key == key);
			if (attr == null) {
				Attributes.Add(new AssetAttribute(key, value));
			} else {
				attr.Value = value;
			}
		}

		public void AddTag(string tagName)
		{
			var tag = Tags.FirstOrDefault(t => t.TagName == tagName);
			if (tag == null) {
				Tags.Add(new AssetTag(tagName));
			}
		}

		public void RemoveTag(string tagName)
		{
			var tag = Tags.FirstOrDefault(t => t.TagName == tagName);
			if (tag != null) {
				Tags.Remove(tag);
			}
		}

		public IEnumerable<Asset> GetNestedAssets() => EditorUtility.CollectDependencies(new[] { Object })
			.Where(o => o is GameObject)
			.Select(g => AssetDatabase.TryGetGUIDAndLocalFileIdentifier(g, out var guid, out long _) ? guid : null)
			.Where(guid => guid != null && guid != GUID && Library.HasAsset(guid))
			.Distinct()
			.Select(guid => Library.GetAsset(guid));

		private static void AddValuesToAttribute(AssetAttribute attr, string values)
		{
			var destValues = new HashSet<string>(attr.Value.Split(",").Select(v => v.Trim().ToLowerInvariant()));
			var srcValues = values.Split(",").Select(v => v.Trim());
			foreach (var srcValue in srcValues) {
				if (!destValues.Contains(srcValue.ToLowerInvariant())) {
					attr.Value += $",{srcValue}";
				}
			}
		}
	}
}
