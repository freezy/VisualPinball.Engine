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
