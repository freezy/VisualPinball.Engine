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

using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Material = UnityEngine.Material;

namespace VisualPinball.Unity.Editor
{
	[Serializable]
	public static class VpxImportWizardSettings
	{
		public static bool ApplyPatch
		{
			get => EditorPrefs.GetBool("ApplyPatch", true);
			set => EditorPrefs.SetBool("ApplyPatch", value);
		}

		public static string VpxPath
		{
			get => EditorPrefs.GetString("VpxPath", "");
			set => EditorPrefs.SetString("VpxPath", value);
		}

		public static string TableName
		{
			get => EditorPrefs.GetString("TableName", "%TABLENAME%");
			set => EditorPrefs.SetString("TableName", value);
		}

		public static VpxObjectImportFilter ObjectImportFilter
		{
			get => (VpxObjectImportFilter)EditorPrefs.GetInt("ObjectImportFilter", (int)VpxObjectImportFilter.All);
			set => EditorPrefs.SetInt("ObjectImportFilter", (int)value);
		}

		public static bool ImportTextures
		{
			get => EditorPrefs.GetBool("ImportTextures", true);
			set => EditorPrefs.SetBool("ImportTextures", value);
		}

		public static bool ImportSounds
		{
			get => EditorPrefs.GetBool("ImportSounds", true);
			set => EditorPrefs.SetBool("ImportSounds", value);
		}

		public static bool ForceAllObjectsVisible
		{
			get => EditorPrefs.GetBool("ForceAllObjectsVisible", false);
			set => EditorPrefs.SetBool("ForceAllObjectsVisible", value);
		}

		public static Material OverrideVisualMaterial
		{
			get {
				var materialPath = EditorPrefs.GetString("OverrideVisualMaterialPath", "");
				return string.IsNullOrEmpty(materialPath)
					? null
					: AssetDatabase.LoadAssetAtPath<Material>(materialPath);
			}
			set {
				var materialPath = value != null
					? AssetDatabase.GetAssetPath(value)
					: string.Empty;
				EditorPrefs.SetString("OverrideVisualMaterialPath", materialPath);
			}
		}

		public static ConvertOptions BuildConvertOptions()
		{
			var options = new ConvertOptions {
				ObjectImportFilter = ObjectImportFilter,
				ImportTextures = ImportTextures,
				ImportSounds = ImportSounds,
				ForceAllObjectsVisible = ForceAllObjectsVisible,
				OverrideVisualMaterial = OverrideVisualMaterial
			};

			if (options.ObjectImportFilter == VpxObjectImportFilter.CollidableOnly) {
				options.SkipExistingMeshes = false;
				options.ImportTextures = false;
				options.ImportSounds = false;
			}

			return options;
		}

		public static bool IsPathValid()
		{
			return !string.IsNullOrEmpty(VpxPath) && File.Exists(VpxPath);
		}

		public static void Reset()
		{
			VpxPath = "";
			ApplyPatch = true;
			TableName = "%TABLENAME%";
			ObjectImportFilter = VpxObjectImportFilter.All;
			ImportTextures = true;
			ImportSounds = true;
			ForceAllObjectsVisible = false;
			OverrideVisualMaterial = null;
		}
	}
}