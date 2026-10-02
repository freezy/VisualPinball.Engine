// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// The preset's fields, passed on to every material linked to it as they
	/// change, and the list of those materials.
	/// </summary>
	[CustomEditor(typeof(InsertColorPreset)), CanEditMultipleObjects]
	public class InsertColorPresetInspector : UnityEditor.Editor
	{
		/// <summary>Linked materials per preset, found once rather than on every change of a color being dragged.</summary>
		private readonly Dictionary<InsertColorPreset, List<Material>> _linked = new();

		private void OnEnable()
		{
			EditorApplication.projectChanged += Forget;
			Undo.undoRedoPerformed += Forget;
		}

		private void OnDisable()
		{
			EditorApplication.projectChanged -= Forget;
			Undo.undoRedoPerformed -= Forget;
		}

		private void Forget()
		{
			_linked.Clear();
			Repaint();
		}

		private List<Material> Linked(InsertColorPreset preset)
		{
			if (!_linked.TryGetValue(preset, out var materials)) {
				materials = InsertColorPresets.LinkedMaterials(preset);
				_linked[preset] = materials;
			}
			return materials;
		}

		public override void OnInspectorGUI()
		{
			if (DrawDefaultInspector()) {
				foreach (var preset in targets.OfType<InsertColorPreset>()) {
					InsertColorPresets.Sync(preset, Linked(preset), true);
				}
			}

			if (targets.Length > 1) {
				return;
			}
			var single = (InsertColorPreset)target;
			var linked = Linked(single).Where(material => material != null).ToArray();
			EditorGUILayout.Space();
			EditorGUILayout.LabelField($"Linked Materials ({linked.Length})", EditorStyles.boldLabel);
			if (linked.Length == 0) {
				EditorGUILayout.HelpBox("No material follows this preset yet. Link an insert's body and cover in its inspector, under Color.", MessageType.None);
				return;
			}
			EditorGUILayout.HelpBox("These materials take the preset's colors and Max Brightness whenever it changes.", MessageType.None);
			using (new EditorGUI.DisabledScope(true)) {
				foreach (var material in linked) {
					var readOnly = InsertBaker.IsReadOnly(AssetDatabase.GetAssetPath(material));
					EditorGUILayout.ObjectField(readOnly ? new GUIContent("Read-only", "This material's package can't be written to, so it doesn't follow the preset.") : GUIContent.none,
						material, typeof(Material), false);
				}
			}
		}
	}

	/// <summary>
	/// Passes a preset changed outside the inspector, by version control for
	/// example, on to the materials linked to it.
	/// </summary>
	internal class InsertColorPresetPostprocessor : AssetPostprocessor
	{
		private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
		{
			var paths = imported
				.Where(path => path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) && AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(InsertColorPreset))
				.ToArray();
			if (paths.Length == 0) {
				return;
			}
			// Not while importing: the materials may not be there yet.
			EditorApplication.delayCall += () => {
				foreach (var path in paths) {
					var preset = AssetDatabase.LoadAssetAtPath<InsertColorPreset>(path);
					if (preset != null) {
						InsertColorPresets.Sync(preset, InsertColorPresets.LinkedMaterials(preset), false);
					}
				}
			};
		}
	}
}
