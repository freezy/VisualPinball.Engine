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
	/// Finds, saves and applies insert material templates: the look of a
	/// clear or a coloured insert, put on another insert's materials without
	/// touching their bake.
	/// </summary>
	public static class InsertMaterialTemplates
	{
		private const string LuminosityProperty = "_InsertLuminosity";

		/// <summary>Every template in the project and its packages, by name.</summary>
		public static InsertMaterialTemplate[] Find()
		{
			return AssetDatabase.FindAssets("t:" + nameof(InsertMaterialTemplate))
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
		/// Puts the template on the insert's body material and, where it has
		/// one, its cover material, undoably, and returns how many values were
		/// set.
		/// </summary>
		public static int Apply(InsertMaterialTemplate template, InsertComponent insert)
		{
			var body = insert.GetComponent<MeshRenderer>().sharedMaterial;
			if (body == null || !body.HasProperty(LuminosityProperty)) {
				return 0;
			}
			var cover = insert.CoverMaterial;
			var touched = cover != null ? new Object[] { body, cover } : new Object[] { body };
			Undo.RecordObjects(touched, "Apply Insert Material Template");
			var count = template.Apply(body, cover);
			foreach (var material in touched) {
				EditorUtility.SetDirty(material);
			}
			return count;
		}

		/// <summary>
		/// Saves the insert's current look as a new template, asking where, and
		/// returns it, or null when the user cancelled.
		/// </summary>
		public static InsertMaterialTemplate SaveAs(InsertComponent insert)
		{
			var body = insert.GetComponent<MeshRenderer>().sharedMaterial;
			if (body == null) {
				return null;
			}
			var path = EditorUtility.SaveFilePanelInProject("Save Insert Material Template", body.name + " Template", "asset",
				"Where to save the look of this insert's materials.");
			if (string.IsNullOrEmpty(path)) {
				return null;
			}
			return Save(body, insert.CoverMaterial, path);
		}

		/// <summary>Saves the look of the two materials as a template asset at that path.</summary>
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
