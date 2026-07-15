// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	public sealed class DmdStudioWindow : EditorWindow
	{
		private const string PackageRoot =
			"Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/DmdStudio/";

		[SerializeField] private DmdProjectAsset _project;
		[SerializeField] private DmdCueAsset _selectedCue;
		[SerializeField] private UnityEngine.Object _selectedAsset;
		[SerializeField] private int _selectedLayerIndex = -1;
		[SerializeField] private int _sampleStateIndex;
		[SerializeField] private int _frame;
		[SerializeField] private DmdCanvasMode _canvasMode;
		[SerializeField] private bool _tint = true;
		[SerializeField] private bool _mirrorToScene;

		private ObjectField _projectField;
		private DmdProjectTreeView _projectTree;
		private VisualElement _inspectorHost;
		private Label _inspectorTitle;
		private VisualElement _sampleStateHost;
		private PopupField<string> _sampleStatePopup;
		private Label _frameLabel;
		private Label _status;
		private DropdownField _canvasModeField;
		private Toggle _tintToggle;
		private Toggle _mirrorToggle;
		private IntegerField _cellWidth;
		private IntegerField _cellHeight;
		private ToolbarButton _playButton;
		private DmdCanvasView _canvas;
		private DmdTimelineView _timeline;
		private SerializedObject _inspectedObject;
		private CueRenderer _renderer;
		private CueInstanceState _previewState = new CueInstanceState();
		private CueDiagnostics _diagnostics = new CueDiagnostics();
		private bool _playing;
		private double _lastUpdateTime;
		private double _frameAccumulator;
		private int _lastRenderedFrame = -1;

		[MenuItem("Pinball/DMD Studio", false, 410)]
		public static void ShowWindow()
		{
			var window = GetWindow<DmdStudioWindow>();
			window.titleContent = new GUIContent("DMD Studio");
			window.minSize = new Vector2(900, 480);
		}

		private void OnEnable()
		{
			EditorApplication.update += OnEditorUpdate;
			Undo.undoRedoPerformed += OnUndoRedo;
			_lastUpdateTime = EditorApplication.timeSinceStartup;
		}

		private void OnDisable()
		{
			EditorApplication.update -= OnEditorUpdate;
			Undo.undoRedoPerformed -= OnUndoRedo;
			_canvas?.Dispose();
		}

		public void CreateGUI()
		{
			var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PackageRoot + "DmdStudioWindow.uxml");
			if (tree == null) {
				throw new InvalidOperationException("Could not load DMD Studio UXML.");
			}
			tree.CloneTree(rootVisualElement);
			var style = AssetDatabase.LoadAssetAtPath<StyleSheet>(PackageRoot + "DmdStudioWindow.uss");
			if (style != null) {
				rootVisualElement.styleSheets.Add(style);
			}

			_projectField = rootVisualElement.Q<ObjectField>("project-field");
			_projectField.objectType = typeof(DmdProjectAsset);
			_projectField.SetValueWithoutNotify(_project);
			_projectField.RegisterValueChangedCallback(evt => SetProject(evt.newValue as DmdProjectAsset));
			_projectTree = new DmdProjectTreeView();
			_projectTree.ItemSelected += OnTreeSelectionChanged;
			rootVisualElement.Q<VisualElement>("project-tree-host").Add(_projectTree);

			_inspectorHost = rootVisualElement.Q<VisualElement>("inspector-host");
			_inspectorTitle = rootVisualElement.Q<Label>("inspector-title");
			_sampleStateHost = rootVisualElement.Q<VisualElement>("sample-state-host");
			_status = rootVisualElement.Q<Label>("status");
			_frameLabel = rootVisualElement.Q<Label>("frame-label");
			_cellWidth = rootVisualElement.Q<IntegerField>("cell-width");
			_cellHeight = rootVisualElement.Q<IntegerField>("cell-height");

			_canvas = new DmdCanvasView();
			_canvas.LayerPositionChanged += OnLayerPositionChanged;
			rootVisualElement.Q<VisualElement>("canvas-host").Add(_canvas);
			_timeline = new DmdTimelineView();
			_timeline.FrameChanged += SetFrame;
			rootVisualElement.Q<VisualElement>("timeline-host").Add(_timeline);

			_canvasModeField = rootVisualElement.Q<DropdownField>("canvas-mode");
			_canvasModeField.choices = new List<string> { "Raw", "Dots" };
			_canvasModeField.index = (int)_canvasMode;
			_canvasModeField.RegisterValueChangedCallback(evt => {
				_canvasMode = evt.newValue == "Dots" ? DmdCanvasMode.Dots : DmdCanvasMode.Raw;
				RefreshPreview();
			});
			_tintToggle = rootVisualElement.Q<Toggle>("tint-toggle");
			_tintToggle.SetValueWithoutNotify(_tint);
			_tintToggle.RegisterValueChangedCallback(evt => {
				_tint = evt.newValue;
				RefreshPreview();
			});
			_mirrorToggle = rootVisualElement.Q<Toggle>("mirror-toggle");
			_mirrorToggle.SetValueWithoutNotify(_mirrorToScene);
			_mirrorToggle.RegisterValueChangedCallback(evt => {
				_mirrorToScene = evt.newValue;
				RefreshPreview();
			});

			rootVisualElement.Q<ToolbarButton>("new-project").clicked += CreateProject;
			rootVisualElement.Q<ToolbarButton>("new-cue").clicked += CreateCue;
			rootVisualElement.Q<ToolbarButton>("add-layer").clicked += ShowAddLayerMenu;
			rootVisualElement.Q<ToolbarButton>("import-sprite").clicked += ImportSprite;
			rootVisualElement.Q<ToolbarButton>("import-sequence").clicked += ImportSequence;
			rootVisualElement.Q<ToolbarButton>("import-font").clicked += ImportFont;
			rootVisualElement.Q<Button>("add-default-states").clicked += AddDefaultStates;
			rootVisualElement.Q<ToolbarButton>("previous-frame").clicked += () => SetFrame(_frame - 1);
			rootVisualElement.Q<ToolbarButton>("next-frame").clicked += () => SetFrame(_frame + 1);
			_playButton = rootVisualElement.Q<ToolbarButton>("play");
			_playButton.clicked += TogglePlayback;

			_inspectorHost.RegisterCallback<SerializedPropertyChangeEvent>(_ => OnAuthoredPropertyChanged());
			_sampleStateHost.RegisterCallback<SerializedPropertyChangeEvent>(_ => OnAuthoredPropertyChanged());
			BuildSampleStatePicker();
			SetProject(_project, false);
		}

		private void SetProject(DmdProjectAsset project, bool updateField = true)
		{
			_project = project;
			if (updateField && _projectField != null) {
				_projectField.SetValueWithoutNotify(project);
			}
			if (_project == null) {
				_selectedCue = null;
				_selectedAsset = null;
				_selectedLayerIndex = -1;
			} else if (_selectedCue == null || _project.Cues == null || !_project.Cues.Contains(_selectedCue)) {
				_selectedCue = _project.Cues?.FirstOrDefault(cue => cue != null);
				_selectedAsset = _selectedCue != null ? _selectedCue : _project;
				_selectedLayerIndex = -1;
			}
			_renderer = _project == null ? null : new CueRenderer(_project);
			ResetPreviewState();
			RefreshTree();
			RefreshSelection();
			RefreshSampleStates();
			RefreshPreview();
		}

		private void RefreshTree()
		{
			DmdProjectTreeSelection selection = null;
			if (_selectedCue != null && _selectedLayerIndex >= 0) {
				selection = new DmdProjectTreeSelection(DmdProjectTreeSelectionKind.Layer, _selectedCue,
					_selectedLayerIndex, -1);
			} else if (_selectedAsset is DmdCueAsset cue) {
				selection = new DmdProjectTreeSelection(DmdProjectTreeSelectionKind.Cue, cue, -1, -1);
			} else if (_selectedAsset is DmdSpriteAsset sprite) {
				selection = new DmdProjectTreeSelection(DmdProjectTreeSelectionKind.Sprite, sprite, -1, -1);
			} else if (_selectedAsset is DmdFontAsset font) {
				selection = new DmdProjectTreeSelection(DmdProjectTreeSelectionKind.Font, font, -1, -1);
			} else if (_selectedAsset is DmdPaletteAsset palette) {
				selection = new DmdProjectTreeSelection(DmdProjectTreeSelectionKind.Palette, palette, -1, -1);
			}
			_projectTree?.SetProject(_project, selection);
		}

		private void OnTreeSelectionChanged(DmdProjectTreeSelection entry)
		{
			if (entry == null) {
				return;
			}
			if (entry.Kind == DmdProjectTreeSelectionKind.Layer) {
				_selectedCue = entry.Asset as DmdCueAsset;
				_selectedAsset = _selectedCue;
				_selectedLayerIndex = entry.LayerIndex;
			} else if (entry.Kind == DmdProjectTreeSelectionKind.Cue) {
				_selectedCue = entry.Asset as DmdCueAsset;
				_selectedAsset = _selectedCue;
				_selectedLayerIndex = -1;
			} else if (entry.Kind == DmdProjectTreeSelectionKind.SampleState) {
				_sampleStateIndex = entry.SampleStateIndex;
				RefreshSampleStates();
			} else {
				_selectedAsset = entry.Asset;
				_selectedLayerIndex = -1;
			}
			ResetPreviewState();
			RefreshSelection();
			RefreshPreview();
		}

		private void RefreshSelection()
		{
			if (_inspectorHost == null) {
				return;
			}
			_inspectorHost.Clear();
			_inspectedObject = null;
			var layer = SelectedLayer;
			_canvas?.SetSelection(_selectedCue, layer);
			_timeline?.SetCue(_selectedCue, _project?.FrameRate ?? 30);
			if (layer != null && _selectedCue != null) {
				_inspectorTitle.text = layer.Name ?? layer.GetType().Name;
				_inspectedObject = new SerializedObject(_selectedCue);
				var layers = _inspectedObject.FindProperty(nameof(DmdCueAsset.Layers));
				var property = layers?.GetArrayElementAtIndex(_selectedLayerIndex);
				if (property != null) {
					var field = new PropertyField(property);
					field.Bind(_inspectedObject);
					_inspectorHost.Add(field);
				}
				return;
			}
			var target = _selectedAsset != null ? _selectedAsset : _project;
			_inspectorTitle.text = target != null ? target.name : "Inspector";
			if (target != null) {
				_inspectedObject = new SerializedObject(target);
				_inspectorHost.Add(new InspectorElement(_inspectedObject));
			}
		}

		private void BuildSampleStatePicker()
		{
			var host = rootVisualElement.Q<VisualElement>("sample-state-picker");
			host.Clear();
			_sampleStatePopup = new PopupField<string>(new List<string> { "None" }, 0);
			_sampleStatePopup.RegisterValueChangedCallback(_ => {
				_sampleStateIndex = System.Math.Max(0, _sampleStatePopup.index);
				ResetPreviewState();
				RefreshSampleStateInspector();
				RefreshPreview();
			});
			host.Add(_sampleStatePopup);
		}

		private void RefreshSampleStates()
		{
			if (_sampleStatePopup == null) {
				return;
			}
			var choices = _project?.SampleStates?
				.Select((state, index) => state?.Name ?? $"State {index + 1}").ToList() ?? new List<string>();
			if (choices.Count == 0) {
				choices.Add("None");
			}
			_sampleStatePopup.choices = choices;
			_sampleStateIndex = Mathf.Clamp(_sampleStateIndex, 0, choices.Count - 1);
			_sampleStatePopup.index = _sampleStateIndex;
			RefreshSampleStateInspector();
		}

		private void RefreshSampleStateInspector()
		{
			_sampleStateHost?.Clear();
			if (_project?.SampleStates == null || _project.SampleStates.Count == 0 ||
			    _sampleStateIndex < 0 || _sampleStateIndex >= _project.SampleStates.Count) {
				return;
			}
			var serialized = new SerializedObject(_project);
			var states = serialized.FindProperty(nameof(DmdProjectAsset.SampleStates));
			var state = states.GetArrayElementAtIndex(_sampleStateIndex);
			var field = new PropertyField(state);
			field.Bind(serialized);
			_sampleStateHost.Add(field);
		}

		private void RefreshPreview()
		{
			if (_canvas == null || _timeline == null || _frameLabel == null) {
				return;
			}
			_timeline.SetCue(_selectedCue, _project?.FrameRate ?? 30);
			_frame = Mathf.Clamp(_frame, 0, _timeline.MaxFrame);
			_timeline.Frame = _frame;
			_frameLabel.text = $"Frame {_frame} / {_timeline.MaxFrame}";
			if (_project == null || _selectedCue == null) {
				_status.text = _project == null ? "Select or create a DMD project." : "Select or create a cue.";
				return;
			}

			try {
				if (_frame < _lastRenderedFrame) {
					_previewState = new CueInstanceState();
				}
				var format = _project.ColorMode == DmdColorMode.Rgb24 ? DmdPixelFormat.Rgb24 : DmdPixelFormat.I8;
				var surface = new DmdSurface(_project.Width, _project.Height, format);
				_diagnostics.Clear();
				_renderer ??= new CueRenderer(_project);
				_renderer.Render(surface, _selectedCue, _frame, CurrentParameters(), _previewState, _diagnostics);
				_canvas.SetFrame(surface, _project, _canvasMode, _tint);
				_lastRenderedFrame = _frame;
				_status.text = _diagnostics.Count == 0
					? $"{_project.Width}×{_project.Height} · {_project.ColorMode} · {_project.FrameRate} fps"
					: string.Join(" · ", _diagnostics.Diagnostics.Select(diagnostic => diagnostic.Message));
				if (_mirrorToScene) {
					Mirror(surface);
				}
			} catch (Exception exception) {
				_status.text = exception.Message;
				Debug.LogException(exception);
			}
		}

		private DmdParams CurrentParameters()
		{
			if (_project?.SampleStates == null || _sampleStateIndex < 0 ||
			    _sampleStateIndex >= _project.SampleStates.Count) {
				return new DmdParams();
			}
			return DmdStudioDefaults.ToParams(_project.SampleStates[_sampleStateIndex]);
		}

		private void Mirror(DmdSurface surface)
		{
			var display = Resources.FindObjectsOfTypeAll<DotMatrixDisplayComponent>()
				.FirstOrDefault(candidate => candidate != null && !EditorUtility.IsPersistent(candidate) &&
					candidate.gameObject.scene.IsValid() &&
					string.Equals(candidate.Id, _project.DisplayId, StringComparison.OrdinalIgnoreCase));
			if (display == null) {
				_status.text += $" · no scene DMD named {_project.DisplayId}";
				return;
			}
			display.UpdateDimensions(_project.Width, _project.Height, false);
			display.UpdateFrame(surface.Format == DmdPixelFormat.Rgb24
				? DisplayFrameFormat.Dmd24
				: DisplayFrameFormat.Dmd8, surface.Data);
		}

		private void SetFrame(int frame)
		{
			_frame = Mathf.Clamp(frame, 0, _timeline?.MaxFrame ?? 0);
			RefreshPreview();
		}

		private void TogglePlayback()
		{
			_playing = !_playing;
			_playButton.text = _playing ? "❚❚" : "▶";
			_lastUpdateTime = EditorApplication.timeSinceStartup;
			_frameAccumulator = 0d;
		}

		private void OnEditorUpdate()
		{
			var now = EditorApplication.timeSinceStartup;
			var delta = System.Math.Max(0d, now - _lastUpdateTime);
			_lastUpdateTime = now;
			if (!_playing || _project == null || _selectedCue == null || _timeline == null) {
				return;
			}
			_frameAccumulator += delta * System.Math.Max(1, _project.FrameRate);
			var advance = (int)_frameAccumulator;
			if (advance <= 0) {
				return;
			}
			_frameAccumulator -= advance;
			var count = System.Math.Max(1, _timeline.MaxFrame + 1);
			SetFrame((_frame + advance) % count);
		}

		private void CreateProject()
		{
			var path = EditorUtility.SaveFilePanelInProject("Create DMD Project", "DmdProject", "asset",
				"Choose a location for the DMD project.");
			if (string.IsNullOrEmpty(path)) {
				return;
			}
			var project = CreateInstance<DmdProjectAsset>();
			project.name = Path.GetFileNameWithoutExtension(path);
			DmdStudioDefaults.EnsureSampleStates(project);
			AssetDatabase.CreateAsset(project, path);
			Undo.RegisterCreatedObjectUndo(project, "Create DMD project");
			AssetDatabase.SaveAssets();
			SetProject(project);
			Selection.activeObject = project;
		}

		private void CreateCue()
		{
			if (!RequireProject()) {
				return;
			}
			var path = AssetDatabase.GenerateUniqueAssetPath(DmdSpriteImporter.DefaultAssetPath(_project, "DmdCue"));
			var cue = CreateInstance<DmdCueAsset>();
			cue.name = Path.GetFileNameWithoutExtension(path);
			cue.CueId = UniqueCueId("cue");
			cue.DurationFrames = System.Math.Max(1, _project.FrameRate * 2);
			cue.Loop = true;
			AssetDatabase.CreateAsset(cue, path);
			Undo.RegisterCreatedObjectUndo(cue, "Create DMD cue");
			Undo.RecordObject(_project, "Add DMD cue");
			_project.Cues ??= new List<DmdCueAsset>();
			_project.Cues.Add(cue);
			EditorUtility.SetDirty(_project);
			AssetDatabase.SaveAssets();
			_selectedCue = cue;
			_selectedAsset = cue;
			_selectedLayerIndex = -1;
			RefreshTree();
			RefreshSelection();
			RefreshPreview();
			Selection.activeObject = cue;
		}

		private string UniqueCueId(string stem)
		{
			var id = stem;
			var suffix = 2;
			while (_project.Cues != null && _project.Cues.Any(cue => cue != null &&
			       string.Equals(cue.EffectiveId, id, StringComparison.OrdinalIgnoreCase))) {
				id = $"{stem}-{suffix++}";
			}
			return id;
		}

		private void ShowAddLayerMenu()
		{
			if (_selectedCue == null) {
				_status.text = "Select a cue before adding a layer.";
				return;
			}
			var menu = new GenericMenu();
			menu.AddItem(new GUIContent("Bitmap"), false, () => AddLayer(new BitmapLayer {
				Name = "Background", Sprite = _project.Sprites?.FirstOrDefault(sprite => sprite != null),
				Loop = DmdLoopMode.Loop
			}));
			menu.AddItem(new GUIContent("Text"), false, () => AddLayer(new TextLayer {
				Name = "Score", Font = _project.Fonts?.FirstOrDefault(font => font != null), Text = "{score:N0}",
				X = _project.Width / 2, Y = _project.Height / 2, Anchor = DmdAnchor.MiddleCenter
			}));
			menu.AddItem(new GUIContent("Number"), false, () => AddLayer(new NumberLayer {
				Name = "Number", Font = _project.Fonts?.FirstOrDefault(font => font != null), ParamName = "score",
				X = _project.Width / 2, Y = _project.Height / 2, Anchor = DmdAnchor.MiddleCenter
			}));
			menu.AddItem(new GUIContent("Shape"), false, () => AddLayer(new ShapeLayer {
				Name = "Shape", Width = 8, Height = 8, Filled = true
			}));
			menu.AddItem(new GUIContent("Mask"), false, () => AddLayer(new MaskLayer { Name = "Mask" }));
			menu.ShowAsContext();
		}

		private void AddLayer(DmdLayer layer)
		{
			Undo.RecordObject(_selectedCue, "Add DMD layer");
			_selectedCue.Layers ??= new List<DmdLayer>();
			layer.EndFrame = _selectedCue.DurationFrames;
			_selectedCue.Layers.Add(layer);
			_selectedLayerIndex = _selectedCue.Layers.Count - 1;
			EditorUtility.SetDirty(_selectedCue);
			ResetPreviewState();
			RefreshTree();
			RefreshSelection();
			RefreshPreview();
		}

		private void ImportSprite()
		{
			if (!RequireProject()) {
				return;
			}
			var path = EditorUtility.OpenFilePanel("Import DMD Sprite", string.Empty, "png");
			if (!string.IsNullOrEmpty(path)) {
				ImportSprites(new[] { path }, Path.GetFileNameWithoutExtension(path));
			}
		}

		private void ImportSequence()
		{
			if (!RequireProject()) {
				return;
			}
			var folder = EditorUtility.OpenFolderPanel("Import DMD PNG Sequence", string.Empty, string.Empty);
			if (string.IsNullOrEmpty(folder)) {
				return;
			}
			var files = Directory.GetFiles(folder, "*.png").OrderBy(path => path, StringComparer.Ordinal).ToArray();
			if (files.Length == 0) {
				_status.text = "The selected folder contains no PNG files.";
				return;
			}
			ImportSprites(files, new DirectoryInfo(folder).Name);
		}

		private void ImportSprites(IReadOnlyList<string> paths, string name)
		{
			try {
				var result = DmdSpriteImporter.Import(_project, paths,
					DmdSpriteImporter.DefaultAssetPath(_project, name), new DmdSpriteImportOptions {
						CellWidth = System.Math.Max(0, _cellWidth.value),
						CellHeight = System.Math.Max(0, _cellHeight.value),
						DefaultFrameDuration = 1
					});
				DmdSpriteImporter.ReportWarnings(result.Warnings);
				_selectedAsset = result.Sprite;
				RefreshTree();
				RefreshSelection();
				_status.text = result.Warnings.Count == 0
					? $"Imported {result.Sprite.Frames.Count} sprite frame(s)."
					: string.Join(" · ", result.Warnings);
			} catch (Exception exception) {
				EditorUtility.DisplayDialog("DMD Sprite Import", exception.Message, "OK");
			}
		}

		private void ImportFont()
		{
			if (!RequireProject()) {
				return;
			}
			var descriptor = EditorUtility.OpenFilePanel("Import BMFont Text Descriptor", string.Empty, "fnt");
			if (string.IsNullOrEmpty(descriptor)) {
				return;
			}
			try {
				var font = DmdBmFontImporter.Import(_project, descriptor,
					DmdSpriteImporter.DefaultAssetPath(_project, Path.GetFileNameWithoutExtension(descriptor)));
				_selectedAsset = font;
				RefreshTree();
				RefreshSelection();
				_status.text = $"Imported {font.Glyphs.Count} glyphs and {font.Kerning.Count} kerning pairs.";
			} catch (Exception exception) {
				EditorUtility.DisplayDialog("DMD BMFont Import", exception.Message, "OK");
			}
		}

		private void AddDefaultStates()
		{
			if (!RequireProject()) {
				return;
			}
			Undo.RecordObject(_project, "Add DMD sample states");
			var changed = DmdStudioDefaults.EnsureSampleStates(_project);
			RefreshTree();
			RefreshSampleStates();
			RefreshPreview();
			_status.text = changed ? "Added default sample-state presets." : "Default sample states already exist.";
		}

		private bool RequireProject()
		{
			if (_project != null) {
				return true;
			}
			_status.text = "Select or create a DMD project first.";
			return false;
		}

		private void OnAuthoredPropertyChanged()
		{
			ResetPreviewState();
			RefreshTree();
			RefreshPreview();
		}

		private void OnLayerPositionChanged()
		{
			_inspectedObject?.Update();
			RefreshPreview();
		}

		private void OnUndoRedo()
		{
			ResetPreviewState();
			RefreshTree();
			RefreshSelection();
			RefreshSampleStates();
			RefreshPreview();
		}

		private void ResetPreviewState()
		{
			_previewState = new CueInstanceState();
			_diagnostics = new CueDiagnostics();
			_lastRenderedFrame = -1;
		}

		private DmdLayer SelectedLayer => _selectedCue?.Layers != null && _selectedLayerIndex >= 0 &&
		                                    _selectedLayerIndex < _selectedCue.Layers.Count
			? _selectedCue.Layers[_selectedLayerIndex]
			: null;

	}
}
