// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// The buttons the presets are picked with, wherever they are picked: one
	/// per preset, wrapped onto as many rows as they need, a colour's with its
	/// swatch in front of its name. And what tells a shipped preset from one
	/// of the project's.
	/// </summary>
	public static class InsertPresetGui
	{
		private static GUIStyle _button;
		private static GUIStyle Button => _button ??= new GUIStyle(EditorStyles.miniButton) { richText = true, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(8, 8, 2, 2), fixedHeight = 22f };

		/// <summary>Whether the preset ships with a package, and is edited by duplicating it into the project.</summary>
		public static bool IsPackaged(Object preset)
		{
			var path = preset != null ? AssetDatabase.GetAssetPath(preset) : null;
			return !string.IsNullOrEmpty(path) && path.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>A colour preset's label: its swatch, then its name.</summary>
		public static GUIContent Label(InsertColorPreset preset, string tooltip = null)
			=> new($"<color=#{ColorUtility.ToHtmlStringRGB(preset.Swatch)}>●</color> {preset.ColorName}", tooltip ?? Where(preset));

		public static GUIContent Label(InsertMaterialPreset preset, string tooltip = null) => new(preset.name, tooltip ?? Where(preset));

		public static GUIContent Label(InsertLampPreset preset, string tooltip = null) => new(preset.LampName, tooltip ?? Where(preset));

		/// <summary>Where the preset lives, for a tooltip.</summary>
		public static string Where(Object preset)
			=> IsPackaged(preset) ? $"Ships with VPE: {AssetDatabase.GetAssetPath(preset)}. Edit… duplicates it into the project to change it." : $"In this project: {AssetDatabase.GetAssetPath(preset)}.";

		/// <summary>
		/// Draws the buttons on as many rows as the inspector's width asks for.
		/// Returns the index of the button clicked this frame, or -1. A button
		/// whose index is in <paramref name="on"/> is drawn pressed.
		/// </summary>
		public static int Buttons(IReadOnlyList<GUIContent> contents, ISet<int> on = null, Action trailing = null, float trailingWidth = 0f)
		{
			var clicked = -1;
			// Room for the inspector's scrollbar and margins.
			var available = EditorGUIUtility.currentViewWidth - 52f;
			var used = 0f;
			var open = false;
			for (var i = 0; i < contents.Count; i++) {
				var width = Button.CalcSize(contents[i]).x + 2f;
				if (!open || used + width > available) {
					if (open) {
						GUILayout.FlexibleSpace();
						EditorGUILayout.EndHorizontal();
					}
					EditorGUILayout.BeginHorizontal();
					open = true;
					used = 0f;
				}
				var pressed = on != null && on.Contains(i);
				if (GUILayout.Toggle(pressed, contents[i], Button, GUILayout.Width(width)) && !pressed) {
					clicked = i;
				}
				used += width;
			}
			if (trailing != null) {
				if (!open || used + trailingWidth > available) {
					if (open) {
						GUILayout.FlexibleSpace();
						EditorGUILayout.EndHorizontal();
					}
					EditorGUILayout.BeginHorizontal();
					open = true;
				}
				GUILayout.FlexibleSpace();
				trailing();
			}
			if (open) {
				if (trailing == null) {
					GUILayout.FlexibleSpace();
				}
				EditorGUILayout.EndHorizontal();
			}
			return clicked;
		}

		/// <summary>The button that opens the preset window on a kind of preset, with one selected.</summary>
		public static void EditButton(InsertPresetWindow.Kind kind, Object select = null)
		{
			if (GUILayout.Button(new GUIContent("Manage", "Opens the Insert Presets window: every preset of this kind, where it lives, and the way to add, copy and change the project's own."), EditorStyles.miniButton, GUILayout.Width(60f))) {
				InsertPresetWindow.Open(kind, select);
			}
		}
	}

	/// <summary>
	/// Every insert preset in one place: the lamps, the colours and the looks,
	/// those shipped with VPE and the project's own, with the way to add,
	/// duplicate, change and remove the project's. Shipped presets are read
	/// here and duplicated into the project to be changed.
	/// </summary>
	public class InsertPresetWindow : EditorWindow
	{
		public enum Kind
		{
			Lamp,
			Color,
			Look,
		}

		private static readonly string[] Tabs = { "Lamp", "Color", "Look" };
		private const string FolderKey = "VisualPinball.InsertPresets.Folder";

		[SerializeField] private Kind _kind = Kind.Color;
		[SerializeField] private Object _selected;
		private UnityEditor.Editor _editor;
		private Vector2 _list;
		private Vector2 _details;

		[MenuItem("Pinball/Insert Presets", false, 305)]
		public static void Open() => Open(Kind.Color, null);

		public static void Open(Kind kind, Object select)
		{
			var window = GetWindow<InsertPresetWindow>("Insert Presets");
			window.minSize = new Vector2(640f, 360f);
			window._kind = kind;
			if (select != null) {
				window._selected = select;
			}
			window.Show();
		}

		private void OnEnable()
		{
			EditorApplication.projectChanged += Repaint;
			Undo.undoRedoPerformed += Repaint;
		}

		private void OnDisable()
		{
			EditorApplication.projectChanged -= Repaint;
			Undo.undoRedoPerformed -= Repaint;
			if (_editor != null) {
				DestroyImmediate(_editor);
			}
		}

		private static Type TypeOf(Kind kind) => kind switch {
			Kind.Lamp => typeof(InsertLampPreset),
			Kind.Color => typeof(InsertColorPreset),
			_ => typeof(InsertMaterialPreset),
		};

		private static Object[] Presets(Kind kind) => kind switch {
			Kind.Lamp => InsertLampPresets.Find().Cast<Object>().ToArray(),
			Kind.Color => InsertColorPresets.Find().Cast<Object>().ToArray(),
			_ => InsertMaterialPresets.Find().Cast<Object>().ToArray(),
		};

		private static string NameOf(Object preset) => preset switch {
			InsertLampPreset lamp => lamp.LampName,
			InsertColorPreset color => color.ColorName,
			_ => preset != null ? preset.name : "",
		};

		private static string What(Kind kind) => kind switch {
			Kind.Lamp => "A lamp is the bulb under the inserts: the colour of its light, its output and how it fades. The whole table runs on one, from Lamp Manager; a single Insert Light can run another.",
			Kind.Color => "A colour is the plastic: tint, cavity colour, lamp emission, max brightness, and the cover's haze and tint. The body and cover materials made for it follow it when it changes.",
			_ => "A look is how the plastic is moulded and handles light: every numeric setting of the body material but max brightness, which is the colour's, and the lamp positions, which are the shape's. A body material follows the look it is linked to.",
		};

		private void OnGUI()
		{
			EditorGUI.BeginChangeCheck();
			_kind = (Kind)GUILayout.Toolbar((int)_kind, Tabs, GUILayout.Height(24f));
			if (EditorGUI.EndChangeCheck()) {
				_selected = null;
			}
			EditorGUILayout.HelpBox(What(_kind) + "\n\nPresets are assets. VPE lists every " + TypeOf(_kind).Name + " it finds in the project and its packages, here and on the inserts. The ones that ship with VPE live in a package and are not changed here: duplicate one into the project and change the copy. The project's own can live anywhere under Assets.", MessageType.None);

			var presets = Presets(_kind);
			if (_selected != null && Array.IndexOf(presets, _selected) < 0) {
				_selected = null;
			}
			if (_selected == null && presets.Length > 0) {
				_selected = presets[0];
			}
			using (new EditorGUILayout.HorizontalScope()) {
				using (new EditorGUILayout.VerticalScope(GUILayout.Width(240f))) {
					DrawList(presets);
				}
				using (new EditorGUILayout.VerticalScope()) {
					DrawDetails();
				}
			}
		}

		private void DrawList(Object[] presets)
		{
			using (var scroll = new EditorGUILayout.ScrollViewScope(_list)) {
				_list = scroll.scrollPosition;
				foreach (var preset in presets) {
					var packaged = InsertPresetGui.IsPackaged(preset);
					using (new EditorGUILayout.HorizontalScope()) {
						var label = preset switch {
							InsertColorPreset color => InsertPresetGui.Label(color, AssetDatabase.GetAssetPath(preset)),
							InsertLampPreset lamp => InsertPresetGui.Label(lamp, AssetDatabase.GetAssetPath(preset)),
							InsertMaterialPreset look => InsertPresetGui.Label(look, AssetDatabase.GetAssetPath(preset)),
							_ => new GUIContent(NameOf(preset)),
						};
						var style = new GUIStyle(preset == _selected ? "TV Selection" : "Label") { richText = true, alignment = TextAnchor.MiddleLeft };
						if (GUILayout.Button(label, style, GUILayout.ExpandWidth(true), GUILayout.Height(20f))) {
							_selected = preset;
							GUI.FocusControl(null);
						}
						GUILayout.Label(new GUIContent(packaged ? "VPE" : "project", packaged ? "Ships with VPE; read-only here." : "This project's own."), EditorStyles.miniLabel, GUILayout.Width(44f));
					}
				}
				if (presets.Length == 0) {
					EditorGUILayout.HelpBox("None found.", MessageType.None);
				}
			}
			using (new EditorGUILayout.HorizontalScope()) {
				if (GUILayout.Button(new GUIContent("New", "A new preset of this kind in the project, with default values."), EditorStyles.miniButtonLeft)) {
					Create(null);
				}
				using (new EditorGUI.DisabledScope(_selected == null)) {
					if (GUILayout.Button(new GUIContent("Duplicate", "A copy of the selected preset in the project: the way to change one that ships with VPE."), EditorStyles.miniButtonMid)) {
						Create(_selected);
					}
				}
				using (new EditorGUI.DisabledScope(_selected == null || InsertPresetGui.IsPackaged(_selected))) {
					if (GUILayout.Button(new GUIContent("Delete", "Removes the selected project preset. Materials and lights that followed it keep their values."), EditorStyles.miniButtonRight)) {
						Delete(_selected);
					}
				}
			}
		}

		private void DrawDetails()
		{
			if (_selected == null) {
				EditorGUILayout.HelpBox("Select a preset, or press New.", MessageType.None);
				return;
			}
			var packaged = InsertPresetGui.IsPackaged(_selected);
			using (new EditorGUILayout.HorizontalScope()) {
				EditorGUILayout.LabelField(NameOf(_selected), EditorStyles.boldLabel);
				if (GUILayout.Button(new GUIContent("Show in Project", AssetDatabase.GetAssetPath(_selected)), EditorStyles.miniButton, GUILayout.Width(110f))) {
					EditorGUIUtility.PingObject(_selected);
				}
			}
			EditorGUILayout.LabelField(AssetDatabase.GetAssetPath(_selected), EditorStyles.miniLabel);
			if (packaged) {
				EditorGUILayout.HelpBox("This preset ships with VPE. Duplicate it to get one of your own to change.", MessageType.Info);
			}
			if (_editor == null || _editor.target != _selected) {
				if (_editor != null) {
					DestroyImmediate(_editor);
				}
				_editor = UnityEditor.Editor.CreateEditor(_selected);
			}
			using (var scroll = new EditorGUILayout.ScrollViewScope(_details)) {
				_details = scroll.scrollPosition;
				using (new EditorGUI.DisabledScope(packaged)) {
					_editor.OnInspectorGUI();
				}
			}
		}

		/// <summary>A new preset in the project, from scratch or as a copy of another, saved where the user says.</summary>
		private void Create(Object source)
		{
			var type = TypeOf(_kind);
			var baseName = source != null ? NameOf(source) + " Copy" : "New " + Tabs[(int)_kind];
			var folder = EditorPrefs.GetString(FolderKey, "Assets/Inserts/Presets");
			var path = EditorUtility.SaveFilePanelInProject(source != null ? "Duplicate Insert Preset" : "New Insert Preset", baseName, "asset", "Where in the project the preset goes. Any folder under Assets will do.", folder);
			if (string.IsNullOrEmpty(path)) {
				return;
			}
			EditorPrefs.SetString(FolderKey, Path.GetDirectoryName(path)?.Replace('\\', '/'));
			var preset = source != null ? Instantiate(source) : CreateInstance(type);
			var name = Path.GetFileNameWithoutExtension(path);
			switch (preset) {
				case InsertLampPreset lamp:
					lamp.LampName = name;
					break;
				case InsertColorPreset color:
					color.ColorName = name;
					break;
			}
			AssetDatabase.CreateAsset(preset, path);
			AssetDatabase.SaveAssets();
			_selected = AssetDatabase.LoadAssetAtPath(path, type);
			GUIUtility.ExitGUI();
		}

		private void Delete(Object preset)
		{
			var path = AssetDatabase.GetAssetPath(preset);
			if (!EditorUtility.DisplayDialog("Delete Insert Preset", $"Delete {NameOf(preset)}?\n\n{path}\n\nMaterials and lights that followed it keep the values they have.", "Delete", "Cancel")) {
				return;
			}
			_selected = null;
			AssetDatabase.DeleteAsset(path);
			GUIUtility.ExitGUI();
		}
	}
}
