// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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
using System.ComponentModel;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// An asset containing somme prefab library settings.
	/// Prefabs are populated to the <see cref="PrefabLibraryEditor"/> using these settings.
	/// </summary>
	/// <remarks>
	/// You can have several PrefabLibrarySettingsAssets, the editor will find all of them into the AssetDatabase.
	/// </remarks>
	[CreateAssetMenu(fileName = "Prefab Library Settings", menuName = "Visual Pinball/Prefab Library Settings", order = 101)]
	public class PrefabLibrarySettingsAsset : ScriptableObject
	{
		public string Name = "Prefab Library";

		[Serializable]
		public class FolderSettings
		{
			public ProjectFolderReference FolderReference = new ProjectFolderReference();
			[Tooltip("Prefabs will be searched recursively into this path.")]
			public bool Recursive = true;
		}

		public bool Locked = false;

		public List<string> AvailableTags = new List<string>();

		public List<string> Categories
		{
			get {
				var categories = new List<string>();
				foreach(var tag in AvailableTags) {
					var subtags = tag.Split('.');
					if (subtags.Length > 1 && !categories.Contains(subtags[0])) {
						categories.Add(subtags[0]);
					}
				}
				return categories;
			}
		}

		public List<FolderSettings> Folders = new List<FolderSettings>();

		private Dictionary<string, List<string>> AssetTags = new Dictionary<string, List<string>>();
	}
}
