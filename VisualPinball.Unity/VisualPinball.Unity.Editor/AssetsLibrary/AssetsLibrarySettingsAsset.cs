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
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// An asset containing somme prefab library settings.
	/// Prefabs are populated to the <see cref="AssetsLibraryEditor"/> using these settings.
	/// </summary>
	/// <remarks>
	/// You can have several PrefabLibrarySettingsAssets, the editor will find all of them into the AssetDatabase.
	/// </remarks>
	[CreateAssetMenu(fileName = "Assets Library Settings", menuName = "Visual Pinball/Assets Library Settings", order = 101)]
	public class AssetsLibrarySettingsAsset : ScriptableObject
	{
		[Serializable]
		public class FolderSettings
		{
			public ProjectFolderReference FolderReference = new ProjectFolderReference();

			[Tooltip("Assets will be searched recursively into this path.")]
			public bool Recursive = true;
		}

		public string Name = "Assets Library";

		public bool Locked = false;

		[Tooltip("List of the asset's types to search for.")]
		public List<string> AssetTypes = new List<string>();

		public List<FolderSettings> Folders = new List<FolderSettings>();
	}
}
