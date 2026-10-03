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
	/// The look preset's values, passed on to every material linked to it as
	/// they change, and the list of those materials.
	/// </summary>
	[CustomEditor(typeof(InsertMaterialPreset)), CanEditMultipleObjects]
	public class InsertMaterialPresetInspector : UnityEditor.Editor
	{
		/// <summary>Linked materials per preset, found once rather than on every change of a value being dragged.</summary>
		private readonly Dictionary<InsertMaterialPreset, List<Material>> _linked = new();

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

		private List<Material> Linked(InsertMaterialPreset preset)
		{
			if (!_linked.TryGetValue(preset, out var materials)) {
				materials = InsertMaterialPresets.LinkedMaterials(preset);
				_linked[preset] = materials;
			}
			return materials;
		}

		public override void OnInspectorGUI()
		{
			EditorGUILayout.HelpBox("A look: how the insert is moulded and behaves optically, every numeric setting of the hybrid body. Not in it: the colours and Max Brightness, which are the Color preset's; the lamp positions, which are the shape's; the bulb, which is the Lamp preset's.", MessageType.None);
			if (DrawDefaultInspector()) {
				foreach (var preset in targets.OfType<InsertMaterialPreset>()) {
					InsertMaterialPresets.Sync(preset, Linked(preset), true);
				}
			}

			if (targets.Length > 1) {
				return;
			}
			var single = (InsertMaterialPreset)target;
			var linked = Linked(single).Where(material => material != null).ToArray();
			EditorGUILayout.Space();
			EditorGUILayout.LabelField($"Linked Materials ({linked.Length})", EditorStyles.boldLabel);
			if (linked.Length == 0) {
				EditorGUILayout.HelpBox("No material follows this look yet. Click it under Presets → Look on an insert, or pick it as the Look Preset in a body material.", MessageType.None);
				return;
			}
			EditorGUILayout.HelpBox("These materials take the look's values whenever it changes.", MessageType.None);
			using (new EditorGUI.DisabledScope(true)) {
				foreach (var material in linked) {
					var readOnly = InsertBaker.IsReadOnly(AssetDatabase.GetAssetPath(material));
					var edited = single.Edited(material);
					EditorGUILayout.ObjectField(readOnly
							? new GUIContent("Read-only", "This material's package can't be written to, so it doesn't follow the preset.")
							: edited > 0 ? new GUIContent($"{edited} edited", "Values changed in the material since the look was applied.") : GUIContent.none,
						material, typeof(Material), false);
				}
			}
		}
	}

	/// <summary>
	/// Passes a look changed outside the inspector, by version control for
	/// example, on to the materials linked to it.
	/// </summary>
	internal class InsertMaterialPresetPostprocessor : AssetPostprocessor
	{
		private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
		{
			var paths = imported
				.Where(path => path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) && AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(InsertMaterialPreset))
				.ToArray();
			if (paths.Length == 0) {
				return;
			}
			// Not while importing: the materials may not be there yet.
			EditorApplication.delayCall += () => {
				foreach (var path in paths) {
					var preset = AssetDatabase.LoadAssetAtPath<InsertMaterialPreset>(path);
					if (preset != null) {
						InsertMaterialPresets.Sync(preset, InsertMaterialPresets.LinkedMaterials(preset), false);
					}
				}
			};
		}
	}
}
