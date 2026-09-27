// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// Finds, saves and applies insert material templates: numeric settings
	/// for the hybrid body, without changing colors, maps, or the cover.
	/// </summary>
	public static class InsertMaterialTemplates
	{
		private const string LuminosityProperty = "_InsertLuminosity";
		private static InsertMaterialTemplate[] _templates;

		static InsertMaterialTemplates()
		{
			EditorApplication.projectChanged += () => _templates = null;
		}

		/// <summary>Every template in the project and its packages, by name.</summary>
		public static InsertMaterialTemplate[] Find()
		{
			return _templates ??= AssetDatabase.FindAssets("t:" + nameof(InsertMaterialTemplate))
				.Select(guid => AssetDatabase.LoadAssetAtPath<InsertMaterialTemplate>(AssetDatabase.GUIDToAssetPath(guid)))
				.Where(template => template != null)
				.OrderBy(template => template.name)
				.ToArray();
		}

		/// <summary>Whether the insert wears a material a template can be put on.</summary>
		public static bool HasStyledMaterial(InsertComponent insert)
		{
			var material = insert.GetComponent<MeshRenderer>().sharedMaterial;
			return material != null && material.HasProperty(LuminosityProperty);
		}

		/// <summary>
		/// Applies the template to the insert's body material, undoably, and
		/// returns how many values were set.
		/// </summary>
		public static int Apply(InsertMaterialTemplate template, InsertComponent insert)
		{
			var body = insert.GetComponent<MeshRenderer>().sharedMaterial;
			if (body == null || !body.HasProperty(LuminosityProperty)) {
				return 0;
			}
			Undo.RecordObject(body, "Apply Insert Material Template");
			var count = template.Apply(body, null);
			EditorUtility.SetDirty(body);
			return count;
		}

		/// <summary>
		/// Saves the insert's current body settings as a new template, asking where, and
		/// returns it, or null when the user cancelled.
		/// </summary>
		public static InsertMaterialTemplate SaveAs(InsertComponent insert)
		{
			var body = insert.GetComponent<MeshRenderer>().sharedMaterial;
			if (body == null) {
				return null;
			}
			var path = EditorUtility.SaveFilePanelInProject("Save Insert Material Template", body.name + " Template", "asset",
				"Where to save this insert's numeric body settings.");
			if (string.IsNullOrEmpty(path)) {
				return null;
			}
			return Save(body, null, path);
		}

		/// <summary>Saves the body's numeric settings as a template asset at that path.</summary>
		public static InsertMaterialTemplate Save(Material body, Material cover, string path)
		{
			var template = ScriptableObject.CreateInstance<InsertMaterialTemplate>();
			template.Capture(body, cover);
			var folder = Path.GetDirectoryName(path)?.Replace('\\', '/');
			if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder)) {
				Directory.CreateDirectory(folder);
				AssetDatabase.Refresh();
			}
			AssetDatabase.CreateAsset(template, path);
			_templates = null;
			AssetDatabase.SaveAssets();
			return AssetDatabase.LoadAssetAtPath<InsertMaterialTemplate>(path);
		}

		/// <summary>The names a popup shows for the templates, with a line for none.</summary>
		public static string[] Labels(IReadOnlyList<InsertMaterialTemplate> templates)
		{
			var labels = new string[templates.Count + 1];
			labels[0] = "Choose a template";
			for (var i = 0; i < templates.Count; i++) {
				labels[i + 1] = templates[i].name;
			}
			return labels;
		}
	}
}
