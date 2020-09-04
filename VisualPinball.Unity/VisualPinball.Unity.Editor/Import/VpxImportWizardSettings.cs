// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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

namespace VisualPinball.Unity.Editor
{
	[Serializable]
	public class VpxImportWizardSettings : MonoBehaviour
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

		public static bool IsPathValid()
		{
			return !string.IsNullOrEmpty(VpxPath) && File.Exists(VpxPath);
		}

		public static void Reset()
		{
			VpxPath = "";
			ApplyPatch = true;
			TableName = "%TABLENAME%";
		}
	}
}
