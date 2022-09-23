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
using UnityEditor.Presets;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{
	[CreateAssetMenu(fileName = "Asset", menuName = "Visual Pinball/Asset", order = 302)]
	public class Asset : ScriptableObject
	{
		public string Name => Object != null ? Object.name : "<invalid ref>";

		[SerializeReference]
		public AssetLibrary Library;

		[SerializeReference]
		public Object Object;

		[SerializeField]
		private string _addedAt;

		[SerializeField]
		public string Description;

		[SerializeField]
		public AssetScale Scale = AssetScale.World;

		[SerializeReference]
		public Preset ThumbCameraPreset;

		[SerializeField]
		private string _categoryId;

		[SerializeField]
		public List<LibraryKeyValue> Attributes;

		[SerializeField]
		public List<LibraryKeyValue> Links;

		[SerializeField]
		internal Tags Tags;

		[SerializeField]
		internal List<MaterialVariation> MaterialVariations;

		[NonSerialized]
		private LibraryCategory _category;

		public DateTime AddedAt {
			get => string.IsNullOrEmpty(_addedAt) ? DateTime.Now : Convert.ToDateTime(_addedAt);
			set => _addedAt = value.ToString("o");
		}

		public Asset SetCategory(LibraryDatabase lib)
		{
			_category = lib.GetCategory(_categoryId);
			return this;
		}

		public LibraryCategory Category {
			get => _category;
			set {
				_category = value;
				_categoryId = value.Id;
			}
		}

		public bool IsOfCategory(LibraryCategory category) => _categoryId == category.Id;

	}
}
