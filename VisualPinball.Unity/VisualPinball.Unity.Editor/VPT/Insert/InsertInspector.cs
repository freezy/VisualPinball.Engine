// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// The insert's inspector: its geometry with a top view of it, its bake and
	/// its clear cover, each in a section of its own, with every control saying
	/// what it does and switched off where it would do nothing.
	/// </summary>
	[CustomEditor(typeof(InsertComponent))]
	[CanEditMultipleObjects]
	public class InsertInspector : UnityEditor.Editor
	{
		private SerializedProperty _shape;
		private SerializedProperty _customMesh;
		private SerializedProperty _customCorner;
		private SerializedProperty _width;
		private SerializedProperty _height;
		private SerializedProperty _triangleBase;
		private SerializedProperty _triangleApex;
		private SerializedProperty _rectangleCorner;
		private SerializedProperty _diamondSide;
		private SerializedProperty _diamondTip;
		private SerializedProperty _diamondShear;
		private SerializedProperty _flashWidth;
		private SerializedProperty _flashHeight;
		private SerializedProperty _flashTip;
		private SerializedProperty _flashNotch;
		private SerializedProperty _flashStep;
		private SerializedProperty _flashMirror;
		private SerializedProperty _flashTipCorner;
		private SerializedProperty _flashSpikeCorner;
		private SerializedProperty _flashNotchCorner;
		private SerializedProperty _flashGroove;
		private SerializedProperty _flashGrooveInset;
		private SerializedProperty _flashGrooveWidth;
		private SerializedProperty _flashGrooveDepth;
		private SerializedProperty _flashGrooveSegments;
		private SerializedProperty _bulletTip;
		private SerializedProperty _bulletShoulder;
		private SerializedProperty _bulletShoulderCorner;
		private SerializedProperty _bulletBaseCorner;
		private SerializedProperty _arrowTip;
		private SerializedProperty _arrowNotchDepth;
		private SerializedProperty _arrowNotchWidth;
		private SerializedProperty _arrowFootAngle;
		private SerializedProperty _arrowNotchCorner;
		private SerializedProperty _arrowInnerCorner;
		private SerializedProperty _arrowFootCorner;
		private SerializedProperty _diameter;
		private SerializedProperty _bananaDiameter;
		private SerializedProperty _circleVertices;
		private SerializedProperty _bananaThickness;
		private SerializedProperty _bananaAngle;
		private SerializedProperty _bananaSegments;
		private SerializedProperty _bananaEndSegments;
		private SerializedProperty _depth;
		private SerializedProperty _channel;
		private SerializedProperty _channelWidth;
		private SerializedProperty _channelDepth;
		private SerializedProperty _starburst;
		private SerializedProperty _starburstBursts;
		private SerializedProperty _starburstCenter;
		private SerializedProperty _starburstOffset;
		private SerializedProperty _ribbed;
		private SerializedProperty _ribAngle;
		private SerializedProperty _ribWidth;
		private SerializedProperty _ribOffset;
		private SerializedProperty _ribRoundness;
		private SerializedProperty _ringed;
		private SerializedProperty _ringCenter;
		private SerializedProperty _ringWidth;
		private SerializedProperty _ringOffset;
		private SerializedProperty _ringRoundness;
		private SerializedProperty _ringStart;
		private SerializedProperty _gem;
		private SerializedProperty _gemCenter;
		private SerializedProperty _gemSides;
		private SerializedProperty _gemTable;
		private SerializedProperty _gemRowLength;
		private SerializedProperty _gemSpread;
		private SerializedProperty _gemOffset;
		private SerializedProperty _stippled;
		private SerializedProperty _stippleHeight;
		private SerializedProperty _stippleWidth;
		private SerializedProperty _stippleMargin;
		private SerializedProperty _stippleWhole;
		private SerializedProperty _stippleOffset;
		private SerializedProperty _bottomBevel;
		private SerializedProperty _bottomBevelSegments;
		private SerializedProperty _holes;
		private SerializedProperty _holeSegments;
		private SerializedProperty _holeRadius;
		private SerializedProperty _bakeSettings;
		private SerializedProperty _coverMaterial;
		private SerializedProperty _usePlayfieldNormals;
		private SerializedProperty _coverPlayfield;

		private static readonly int[] TextureSizes = { 256, 512, 1024, 2048, 4096 };
		private static readonly GUIContent[] TextureSizeNames = { new("256"), new("512"), new("1024"), new("2048"), new("4096") };
		private static readonly int[] MapScales = { 1, 2, 4, 8 };
		private static readonly GUIContent[] MapScaleNames = { new("Same size"), new("Half"), new("Quarter"), new("Eighth") };

		/// <summary>How many holes the "Around" preset spreads out, remembered while the inspector is open.</summary>
		private int _aroundCount = 5;

		private const string GeometryKey = "VisualPinball.Insert.Geometry";
		private const string BakeKey = "VisualPinball.Insert.Bake";
		private const string CoverKey = "VisualPinball.Insert.Cover";
		private const string PresetKey = "VisualPinball.Insert.Preset";

		private void OnEnable()
		{
			_shape = serializedObject.FindProperty("_shape");
			_customMesh = serializedObject.FindProperty("_customMesh");
			_customCorner = serializedObject.FindProperty("_customCorner");
			_width = serializedObject.FindProperty("_width");
			_height = serializedObject.FindProperty("_height");
			_triangleBase = serializedObject.FindProperty("_triangleBase");
			_triangleApex = serializedObject.FindProperty("_triangleApex");
			_rectangleCorner = serializedObject.FindProperty("_rectangleCorner");
			_diamondSide = serializedObject.FindProperty("_diamondSide");
			_diamondTip = serializedObject.FindProperty("_diamondTip");
			_diamondShear = serializedObject.FindProperty("_diamondShear");
			_flashWidth = serializedObject.FindProperty("_flashWidth");
			_flashHeight = serializedObject.FindProperty("_flashHeight");
			_flashTip = serializedObject.FindProperty("_flashTip");
			_flashNotch = serializedObject.FindProperty("_flashNotch");
			_flashStep = serializedObject.FindProperty("_flashStep");
			_flashMirror = serializedObject.FindProperty("_flashMirror");
			_flashTipCorner = serializedObject.FindProperty("_flashTipCorner");
			_flashSpikeCorner = serializedObject.FindProperty("_flashSpikeCorner");
			_flashNotchCorner = serializedObject.FindProperty("_flashNotchCorner");
			_flashGroove = serializedObject.FindProperty("_flashGroove");
			_flashGrooveInset = serializedObject.FindProperty("_flashGrooveInset");
			_flashGrooveWidth = serializedObject.FindProperty("_flashGrooveWidth");
			_flashGrooveDepth = serializedObject.FindProperty("_flashGrooveDepth");
			_flashGrooveSegments = serializedObject.FindProperty("_flashGrooveSegments");
			_bulletTip = serializedObject.FindProperty("_bulletTip");
			_bulletShoulder = serializedObject.FindProperty("_bulletShoulder");
			_bulletShoulderCorner = serializedObject.FindProperty("_bulletShoulderCorner");
			_bulletBaseCorner = serializedObject.FindProperty("_bulletBaseCorner");
			_arrowTip = serializedObject.FindProperty("_arrowTip");
			_arrowNotchDepth = serializedObject.FindProperty("_arrowNotchDepth");
			_arrowNotchWidth = serializedObject.FindProperty("_arrowNotchWidth");
			_arrowFootAngle = serializedObject.FindProperty("_arrowFootAngle");
			_arrowNotchCorner = serializedObject.FindProperty("_arrowNotchCorner");
			_arrowInnerCorner = serializedObject.FindProperty("_arrowInnerCorner");
			_arrowFootCorner = serializedObject.FindProperty("_arrowFootCorner");
			_diameter = serializedObject.FindProperty("_diameter");
			_bananaDiameter = serializedObject.FindProperty("_bananaDiameter");
			_circleVertices = serializedObject.FindProperty("_circleVertices");
			_bananaThickness = serializedObject.FindProperty("_bananaThickness");
			_bananaAngle = serializedObject.FindProperty("_bananaAngle");
			_bananaSegments = serializedObject.FindProperty("_bananaSegments");
			_bananaEndSegments = serializedObject.FindProperty("_bananaEndSegments");
			_depth = serializedObject.FindProperty("_depth");
			_channel = serializedObject.FindProperty("_channel");
			_channelWidth = serializedObject.FindProperty("_channelWidth");
			_channelDepth = serializedObject.FindProperty("_channelDepth");
			_starburst = serializedObject.FindProperty("_starburst");
			_starburstBursts = serializedObject.FindProperty("_starburstBursts");
			_starburstCenter = serializedObject.FindProperty("_starburstCenter");
			_starburstOffset = serializedObject.FindProperty("_starburstOffset");
			_ribbed = serializedObject.FindProperty("_ribbed");
			_ribAngle = serializedObject.FindProperty("_ribAngle");
			_ribWidth = serializedObject.FindProperty("_ribWidth");
			_ribOffset = serializedObject.FindProperty("_ribOffset");
			_ribRoundness = serializedObject.FindProperty("_ribRoundness");
			_ringed = serializedObject.FindProperty("_ringed");
			_ringCenter = serializedObject.FindProperty("_ringCenter");
			_ringWidth = serializedObject.FindProperty("_ringWidth");
			_ringOffset = serializedObject.FindProperty("_ringOffset");
			_ringRoundness = serializedObject.FindProperty("_ringRoundness");
			_ringStart = serializedObject.FindProperty("_ringStart");
			_gem = serializedObject.FindProperty("_gem");
			_gemCenter = serializedObject.FindProperty("_gemCenter");
			_gemSides = serializedObject.FindProperty("_gemSides");
			_gemTable = serializedObject.FindProperty("_gemTable");
			_gemRowLength = serializedObject.FindProperty("_gemRowLength");
			_gemSpread = serializedObject.FindProperty("_gemSpread");
			_gemOffset = serializedObject.FindProperty("_gemOffset");
			_stippled = serializedObject.FindProperty("_stippled");
			_stippleHeight = serializedObject.FindProperty("_stippleHeight");
			_stippleWidth = serializedObject.FindProperty("_stippleWidth");
			_stippleMargin = serializedObject.FindProperty("_stippleMargin");
			_stippleWhole = serializedObject.FindProperty("_stippleWhole");
			_stippleOffset = serializedObject.FindProperty("_stippleOffset");
			_bottomBevel = serializedObject.FindProperty("_bottomBevel");
			_bottomBevelSegments = serializedObject.FindProperty("_bottomBevelSegments");
			_holes = serializedObject.FindProperty("_holes");
			_holeSegments = serializedObject.FindProperty("_holeSegments");
			_holeRadius = serializedObject.FindProperty("_holeRadius");
			_bakeSettings = serializedObject.FindProperty("_bakeSettings");
			_coverMaterial = serializedObject.FindProperty("_coverMaterial");
			_usePlayfieldNormals = serializedObject.FindProperty("_usePlayfieldNormals");
			_coverPlayfield = serializedObject.FindProperty("_coverPlayfield");
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			DrawEditInProject();

			if (InsertSection.Begin(GeometryKey, "Geometry")) {
				DrawGeometry();
			}
			InsertSection.End();

			if (InsertSection.Begin(BakeKey, "Normal Bake")) {
				DrawBakeSettings();
			}

			serializedObject.ApplyModifiedProperties();

			// Buttons act on the components themselves, once the fields are applied.
			if (SessionState.GetBool(BakeKey, true)) {
				DrawBake();
				DrawHybridMaterial();
			}
			InsertSection.End();

			if (InsertSection.Begin(CoverKey, "Clear Cover")) {
				serializedObject.Update();
				DrawCover();
				serializedObject.ApplyModifiedProperties();
				if (targets.Length == 1 && ((InsertComponent)target).CoverMaterial == null) {
					using (new EditorGUI.DisabledScope(EditorApplication.isPlaying || Shader.Find("Pinball/Insert Cover") == null)) {
						if (GUILayout.Button("Add Clear Cover")) {
							AddCover((InsertComponent)target);
						}
					}
				}
			}
			InsertSection.End();

			if (InsertSection.Begin(PresetKey, "Presets")) {
				DrawPresets();
			}
			InsertSection.End();

			DrawStatus();
		}

		private static void SubHeader(string title)
		{
			EditorGUILayout.Space(4f);
			EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
		}

		/// <summary>Whether the selected shape's vertex counts are its sampling, with no sides to subdivide and no corners to speak of.</summary>
		private bool Sampled => (InsertShape)_shape.enumValueIndex is InsertShape.Circle or InsertShape.Banana;

		/// <summary>The property with its label, and the tooltip its field declares.</summary>
		/// <summary>What the raised middle's surface carries.</summary>
		private enum Pattern
		{
			None = 0,
			Starburst = 1,
			EngravedLine = 2,
			Stippled = 3,
			Ribbed = 4,
			Rings = 5,
			Gem = 6,
		}

		private static readonly Pattern[] PatternChoices = { Pattern.None, Pattern.Starburst, Pattern.Ribbed, Pattern.Rings, Pattern.Gem, Pattern.Stippled };
		private static readonly Pattern[] GroovePatternChoices = { Pattern.None, Pattern.Starburst, Pattern.Ribbed, Pattern.Rings, Pattern.Gem, Pattern.EngravedLine, Pattern.Stippled };
		private static readonly string[] Patterns = { "None", "Starburst", "Ribbed", "Rings", "Gem", "Stippled" };
		private static readonly string[] GroovePatterns = { "None", "Starburst", "Ribbed", "Rings", "Gem", "Engraved Line", "Stippled" };

		/// <summary>The pattern represented by the stored flags.</summary>
		private static Pattern PatternOf(bool starburst, bool ribbed, bool ringed, bool gem, bool groove, bool stippled, bool flash)
		{
			if (stippled) {
				return Pattern.Stippled;
			}
			if (flash && groove) {
				return Pattern.EngravedLine;
			}
			if (starburst) {
				return Pattern.Starburst;
			}
			if (ribbed) {
				return Pattern.Ribbed;
			}
			if (ringed) {
				return Pattern.Rings;
			}
			return gem ? Pattern.Gem : Pattern.None;
		}

		private static void Field(SerializedProperty property, string label, bool children = false)
		{
			EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip), children);
		}

		/// <summary>A corner's radius and segment count on one line, which is all they need.</summary>
		private static void CornerField(SerializedProperty corner, string label)
		{
			var radius = corner.FindPropertyRelative(nameof(InsertCornerSettings.Radius));
			var segments = corner.FindPropertyRelative(nameof(InsertCornerSettings.Segments));
			var rect = EditorGUI.PrefixLabel(EditorGUILayout.GetControlRect(), new GUIContent(label, corner.tooltip));
			var indent = EditorGUI.indentLevel;
			var labelWidth = EditorGUIUtility.labelWidth;
			EditorGUI.indentLevel = 0;
			const float gap = 8f;
			var half = (rect.width - gap) * 0.5f;
			var left = new Rect(rect.x, rect.y, half, rect.height);
			var right = new Rect(rect.x + half + gap, rect.y, half, rect.height);
			EditorGUIUtility.labelWidth = 48f;
			EditorGUI.PropertyField(left, radius, new GUIContent("Radius", radius.tooltip));
			EditorGUIUtility.labelWidth = 64f;
			// A plain field rather than the range slider, which has no room here.
			var content = EditorGUI.BeginProperty(right, new GUIContent("Segments", segments.tooltip), segments);
			EditorGUI.BeginChangeCheck();
			var count = EditorGUI.IntField(right, content, segments.intValue);
			if (EditorGUI.EndChangeCheck()) {
				segments.intValue = Mathf.Clamp(count, 1, 64);
			}
			EditorGUI.EndProperty();
			EditorGUIUtility.labelWidth = labelWidth;
			EditorGUI.indentLevel = indent;
		}

		#region Geometry

		private void DrawGeometry()
		{
			if (targets.Length == 1) {
				DrawPreview((InsertComponent)target);
			}

			SubHeader("Outline");
			Field(_shape, "Shape");
			switch ((InsertShape)_shape.enumValueIndex) {
				case InsertShape.Custom:
					using (new EditorGUILayout.HorizontalScope()) {
						Field(_customMesh, "Source Mesh");
						if (GUILayout.Button(new GUIContent("Reload", "Re-read the source mesh after changing or reimporting it."), GUILayout.Width(55f))) {
							serializedObject.ApplyModifiedProperties();
							foreach (var selected in targets.Cast<InsertComponent>()) { selected.RebuildMesh(); }
						}
					}
					CornerField(_customCorner, "Corners");
					if (targets.Length == 1 && target is InsertComponent custom && custom.Error != null && custom.CustomMesh != null) {
						// The status box at the bottom says so too, but this is where
						// the source is chosen, and the mesh in the scene is the old one.
						EditorGUILayout.HelpBox(custom.Error, MessageType.Error);
					}
					break;
				case InsertShape.Triangle:
					Field(_width, "Width");
					Field(_height, "Height");
					CornerField(_triangleBase, "Base Corners");
					CornerField(_triangleApex, "Apex Corner");
					break;
				case InsertShape.Rectangle:
					Field(_width, "Width");
					Field(_height, "Height");
					CornerField(_rectangleCorner, "Corners");
					break;
				case InsertShape.Circle:
					Field(_diameter, "Diameter");
					Field(_circleVertices, "Vertices");
					break;
				case InsertShape.Diamond:
					Field(_width, "Width");
					Field(_height, "Height");
					CornerField(_diamondSide, "Side Points");
					CornerField(_diamondTip, "Top and Bottom");
					Field(_diamondShear, "Shear");
					break;
				case InsertShape.Flash:
					Field(_flashWidth, "Width");
					Field(_flashHeight, "Height");
					Field(_flashTip, "Tip Width");
					Field(_flashNotch, "Notch Depth");
					Field(_flashStep, "Step Height");
					Field(_flashMirror, "Mirror");
					CornerField(_flashTipCorner, "Tip Corners");
					CornerField(_flashSpikeCorner, "Spike Corners");
					CornerField(_flashNotchCorner, "Notch Corners");
					break;
				case InsertShape.Bullet:
					Field(_width, "Width");
					Field(_height, "Height");
					CornerField(_bulletTip, "Tip");
					Field(_bulletShoulder, "Shoulder Height");
					CornerField(_bulletShoulderCorner, "Shoulder Corners");
					CornerField(_bulletBaseCorner, "Base Corners");
					break;
				case InsertShape.Arrow: {
					Field(_width, "Width");
					Field(_height, "Height");
					CornerField(_arrowTip, "Tip");
					Field(_arrowNotchDepth, "Notch Depth");
					// Without a notch there is nothing else to set; a notch as
					// wide as the base leaves no feet to tilt or inner corners
					// to round. A mixed selection keeps everything editable.
					var mixed = _width.hasMultipleDifferentValues || _arrowNotchDepth.hasMultipleDifferentValues || _arrowNotchWidth.hasMultipleDifferentValues;
					InsertShapes.ArrowTopology(_width.floatValue, _arrowNotchDepth.floatValue, _arrowNotchWidth.floatValue, out var notched, out var footed);
					notched |= mixed;
					footed |= mixed;
					using (new EditorGUI.DisabledScope(!notched)) {
						Field(_arrowNotchWidth, "Notch Width");
						CornerField(_arrowNotchCorner, "Notch Corner");
					}
					using (new EditorGUI.DisabledScope(!footed)) {
						Field(_arrowFootAngle, "Foot Angle");
						CornerField(_arrowInnerCorner, "Inner Corners");
					}
					CornerField(_arrowFootCorner, "Feet");
					break;
				}
				case InsertShape.Banana:
					Field(_bananaDiameter, "Diameter");
					Field(_bananaThickness, "Thickness");
					Field(_bananaAngle, "Angle");
					Field(_bananaSegments, "Arc Vertices");
					Field(_bananaEndSegments, "End Vertices");
					break;
			}
			// A fillet larger than its sides can take is clamped, and the field
			// then stops doing anything; say so here, next to the field.
			if (targets.Length == 1) {
				// The engraved line rounds its own corners, and says so with its settings.
				foreach (var warning in ((InsertComponent)target).Warnings.Where(w => w.Contains("radius") && !w.StartsWith(InsertComponent.EngravedLineWarning))) {
					EditorGUILayout.HelpBox(warning + " A rounder corner needs longer sides either side of it.", MessageType.Info);
				}
			}

			SubHeader("Body");
			Field(_depth, "Depth");

			SubHeader("Channel");
			Field(_channel, "Channel");
			var channel = _channel.hasMultipleDifferentValues || _channel.boolValue;
			using (new EditorGUI.DisabledScope(!channel)) {
				Field(_channelWidth, "Width");
				Field(_channelDepth, "Depth");
			}
			// Ticked but nought wide is no channel either.
			channel = channel && (_channelWidth.hasMultipleDifferentValues || _channelWidth.floatValue > 0f);

			SubHeader("Bottom Bevel");
			// The bevel rounds the wall into the channel's floor: nothing to round without one.
			using (new EditorGUI.DisabledScope(!channel)) {
				Field(_bottomBevel, "Width");
				using (new EditorGUI.DisabledScope(!_bottomBevel.hasMultipleDifferentValues && _bottomBevel.floatValue <= 0f)) {
					Field(_bottomBevelSegments, "Segments");
				}
			}

			SubHeader("Pattern");
			// One choice for the flat surface, with only that pattern's settings shown.
			var engraved = !_shape.hasMultipleDifferentValues && (InsertShape)_shape.enumValueIndex is InsertShape.Flash or InsertShape.Custom;
			var pattern = PatternOf(_starburst.boolValue, _ribbed.boolValue, _ringed.boolValue, _gem.boolValue, _flashGroove.boolValue, _stippled.boolValue, engraved);
			var patternMixed = _starburst.hasMultipleDifferentValues || _ribbed.hasMultipleDifferentValues || _ringed.hasMultipleDifferentValues || _gem.hasMultipleDifferentValues || _flashGroove.hasMultipleDifferentValues || _stippled.hasMultipleDifferentValues || _shape.hasMultipleDifferentValues;
			var choices = engraved ? GroovePatterns : Patterns;
			var choiceValues = engraved ? GroovePatternChoices : PatternChoices;
			var index = System.Math.Max(0, System.Array.IndexOf(choiceValues, pattern));
			EditorGUI.showMixedValue = patternMixed;
			EditorGUI.BeginChangeCheck();
			var selection = EditorGUILayout.Popup(new GUIContent("Pattern", "The flat surface's pattern. Stippled and Engraved Line become normal maps after baking; Starburst, Ribbed, Rings and Gem stay in the mesh."), index, choices);
			if (EditorGUI.EndChangeCheck()) {
				var chosen = choiceValues[selection];
				_starburst.boolValue = chosen == Pattern.Starburst;
				_ribbed.boolValue = chosen == Pattern.Ribbed;
				_ringed.boolValue = chosen == Pattern.Rings;
				_gem.boolValue = chosen == Pattern.Gem;
				_flashGroove.boolValue = chosen == Pattern.EngravedLine;
				_stippled.boolValue = chosen == Pattern.Stippled;
				pattern = chosen;
				patternMixed = false;
			}
			EditorGUI.showMixedValue = false;

			if (!patternMixed && pattern == Pattern.Starburst) {
				Field(_starburstBursts, "Bursts");
				var starburst = _starburstBursts.hasMultipleDifferentValues || _starburstBursts.intValue >= InsertStarburst.MinBursts;
				using (new EditorGUI.DisabledScope(!starburst)) {
					Field(_starburstCenter, "Center");
					if ((InsertShape)_shape.enumValueIndex == InsertShape.Banana && targets.Length == 1) {
						var arc = ((InsertComponent)target).ArcCenter;
						using (new EditorGUI.DisabledScope(_starburstCenter.vector2Value == arc)) {
							if (GUILayout.Button(new GUIContent("Centre on the Arc", "Puts the starburst's centre where the banana's arc has its own, so every spoke crosses the band straight."))) {
								_starburstCenter.vector2Value = arc;
							}
						}
					}
					Field(_starburstOffset, "Offset");
				}
			} else if (!patternMixed && pattern == Pattern.Ribbed) {
				Field(_ribAngle, "Angle");
				Field(_ribWidth, "Width");
				Field(_ribOffset, "Offset");
				Field(_ribRoundness, "Roundness");
			} else if (!patternMixed && pattern == Pattern.Rings) {
				Field(_ringCenter, "Center");
				if ((InsertShape)_shape.enumValueIndex == InsertShape.Banana && targets.Length == 1) {
					var arc = ((InsertComponent)target).ArcCenter;
					using (new EditorGUI.DisabledScope(_ringCenter.vector2Value == arc)) {
						if (GUILayout.Button(new GUIContent("Centre on the Arc", "Puts the rings' centre where the banana's arc has its own, so the rings run along the band."))) {
							_ringCenter.vector2Value = arc;
						}
					}
				}
				Field(_ringWidth, "Width");
				Field(_ringStart, "Start");
				Field(_ringOffset, "Offset");
				Field(_ringRoundness, "Roundness");
			} else if (!patternMixed && pattern == Pattern.Gem) {
				Field(_gemCenter, "Center");
				Field(_gemSides, "Sides");
				Field(_gemTable, "Table");
				Field(_gemRowLength, "Row Length");
				Field(_gemSpread, "Spread");
				Field(_gemOffset, "Offset");
			} else if (!patternMixed && pattern == Pattern.EngravedLine) {
				Field(_flashGrooveInset, "Inset");
				Field(_flashGrooveWidth, "Width");
				Field(_flashGrooveDepth, "Depth");
				Field(_flashGrooveSegments, "Segments");
				if (targets.Length == 1 && target is InsertComponent single && single.Groove != null) {
					if (single.IsMiddleFlat) {
						EditorGUILayout.HelpBox("Baked: the trough is gone from the mesh and the line is in the maps. Changing the line puts its trough back into the mesh until the next bake.", MessageType.None);
					} else if (single.IsBakeFresh) {
						EditorGUILayout.HelpBox("This bake was made before the middle kept its projection, so the trough is still in the mesh. Bake Again to take it out.", MessageType.Warning);
					} else {
						EditorGUILayout.HelpBox("The line is a trough in the mesh, for the bake: once baked, the trough is gone from the mesh and the line is in the maps.", MessageType.None);
					}
					foreach (var warning in single.Warnings.Where(w => w.StartsWith(InsertComponent.EngravedLineWarning))) {
						EditorGUILayout.HelpBox(warning, MessageType.Info);
					}
				}
				// A line that did not fit says so here, where its settings are.
				foreach (var selected in targets) {
					if (selected is InsertComponent component && component.Groove == null) {
						var reasons = component.Warnings.Where(w => w.Contains("line") || w.Contains("Eroding")).ToArray();
						EditorGUILayout.HelpBox(reasons.Length > 0
							? string.Join(" ", reasons)
							: "The engraved line could not be built.", MessageType.Warning);
						break;
					}
				}
			} else if (!patternMixed && pattern == Pattern.Stippled) {
				Field(_stippleHeight, "Stipple Height");
				Field(_stippleWidth, "Stipple Width");
				Field(_stippleMargin, "Stipple Margin");
				Field(_stippleOffset, "Stipple Offset");
				Field(_stippleWhole, "Whole Only");
				if (targets.Length == 1 && target is InsertComponent single) {
					EditorGUILayout.HelpBox(single.IsMiddleFlat
						? "Baked: the pyramids are in the maps and the mesh is flat."
						: "The pyramids are bake geometry. Bake to put them into the maps and flatten the mesh.", MessageType.None);
				}
			}

			SubHeader("Holes");
			if (!channel) {
				EditorGUILayout.HelpBox("Holes cut through the raised middle, down to the channel's floor. Without a channel there is neither.", MessageType.None);
			}
			using (new EditorGUI.DisabledScope(!channel)) {
				Field(_holes, "Holes", true);
				Field(_holeSegments, "Circle Segments");
				Field(_holeRadius, "New Hole Radius");
				DrawHolePresets();
			}
		}

		private const float PreviewHeight = 220f;
		private static readonly Color HoleColor = new(1f, 0.6f, 0.1f);

		private static GUIStyle _holeLabel;
		private static GUIStyle HoleLabel => _holeLabel ??= new GUIStyle(EditorStyles.boldLabel) {
			fontSize = 13,
			normal = { textColor = HoleColor },
		};

		private static GUIStyle _previewLabel;
		private static GUIStyle PreviewLabel => _previewLabel ??= new GUIStyle(EditorStyles.miniLabel) {
			alignment = TextAnchor.LowerRight,
			normal = { textColor = new Color(0.6f, 0.6f, 0.6f) },
		};

		private static GUIStyle _holeNumber;
		private static GUIStyle HoleNumber => _holeNumber ??= new GUIStyle(EditorStyles.miniBoldLabel) {
			normal = { textColor = HoleColor },
		};

		/// <summary>
		/// The insert from above, as the player sees the playfield: the outline,
		/// the raised floor inside the channel, the starburst's spokes over it and
		/// the holes through it, numbered as the list numbers them.
		/// </summary>
		private void DrawPreview(InsertComponent insert)
		{
			var area = EditorGUILayout.GetControlRect(false, PreviewHeight);
			var outline = insert.Outline;
			if (Event.current.type != EventType.Repaint) {
				return;
			}
			var dark = EditorGUIUtility.isProSkin;
			EditorGUI.DrawRect(area, dark ? new Color(0.14f, 0.14f, 0.14f) : new Color(0.8f, 0.8f, 0.8f));
			if (outline == null || outline.Points.Count < 3) {
				return;
			}
			// Everything below is clipped to the preview, and drawn relative to it,
			// so a hole dragged off the insert cannot draw over the fields below.
			GUI.BeginClip(area);
			var rect = new Rect(0f, 0f, area.width, area.height);
			using (new Handles.DrawingScope(Handles.color, Matrix4x4.identity)) {
				DrawPreview(insert, outline, rect, dark);
			}
			GUI.EndClip();
		}

		private void DrawPreview(InsertComponent insert, InsertOutline outline, Rect rect, bool dark)
		{

			var low = new float2(float.MaxValue);
			var high = new float2(float.MinValue);
			foreach (var point in outline.Points) {
				low = math.min(low, point.Position);
				high = math.max(high, point.Position);
			}
			var extent = math.max(high - low, 1e-3f);
			const float margin = 18f;
			var scale = math.min((rect.width - 2f * margin) / extent.x, (rect.height - 2f * margin) / extent.y);
			var middle = (low + high) * 0.5f;
			// Turned as the mesh is: the generator gives a shape half a turn about
			// the up axis, so its +Y points up the table, and up the screen here.
			Vector3 Map(float2 p) => new(rect.center.x - (p.x - middle.x) * scale, rect.center.y - (p.y - middle.y) * scale, 0f);

			var rim = outline.Points.Select(point => point.Position).ToList();
			var rimScreen = rim.Select(Map).ToArray();
			Handles.color = dark ? new Color(0.35f, 0.38f, 0.45f, 0.5f) : new Color(0.55f, 0.6f, 0.7f, 0.5f);
			if (IsConvex(rim)) {
				Handles.DrawAAConvexPolygon(rimScreen);
			} else {
				foreach (var piece in InsertTriangulation.EarClip(rim.Select(p => (double2)p).ToList())) {
					Handles.DrawAAConvexPolygon(rimScreen[piece.x], rimScreen[piece.y], rimScreen[piece.z]);
				}
			}
			Handles.color = dark ? new Color(0.75f, 0.8f, 0.9f) : new Color(0.2f, 0.25f, 0.35f);
			Handles.DrawAAPolyLine(2.5f, Closed(rimScreen));

			// The floor that can be carved: the raised middle inside the channel, or
			// without one, the floor itself.
			var inset = FloorInset(insert);
			var island = rim;
			if (inset > 0f) {
				island = Enumerable.Range(0, outline.Points.Count).Select(i => outline.OffsetPoint(i, inset)).ToList();
				Handles.color = dark ? new Color(0.6f, 0.65f, 0.75f, 0.8f) : new Color(0.3f, 0.35f, 0.45f, 0.8f);
				Handles.DrawAAPolyLine(1.5f, Closed(island.Select(Map).ToArray()));
			}

			if (_starburst.boolValue && _starburstBursts.intValue >= InsertStarburst.MinBursts) {
				var center = new float2(_starburstCenter.vector2Value.x, _starburstCenter.vector2Value.y);
				var sectors = _starburstBursts.intValue * 2;
				// Enough spokes to show the fan, never so many they turn into a smear.
				var stride = math.max(1, (int)math.ceil(sectors / 96f));
				Handles.color = dark ? new Color(0.9f, 0.9f, 0.7f, 0.35f) : new Color(0.3f, 0.3f, 0.1f, 0.35f);
				for (var sector = 0; sector < sectors; sector += stride) {
					var angle = math.PI * 2f * sector / sectors;
					var direction = new float2(math.cos(angle), math.sin(angle));
					foreach (var (enter, exit) in Through(center, direction, island)) {
						Handles.DrawAAPolyLine(1f, Map(center + direction * enter), Map(center + direction * exit));
					}
				}
				var hub = Map(center);
				if (rect.Contains(hub)) {
					Handles.color = dark ? new Color(0.9f, 0.9f, 0.7f) : new Color(0.3f, 0.3f, 0.1f);
					Handles.DrawAAPolyLine(1.5f, hub + Vector3.left * 5f, hub + Vector3.right * 5f);
					Handles.DrawAAPolyLine(1.5f, hub + Vector3.up * 5f, hub + Vector3.down * 5f);
				}
			}

			if (_ribbed.boolValue && !_starburst.boolValue && _ribWidth.floatValue >= InsertRibs.MinWidth) {
				// Every crease across the floor, ridges and grooves alike, the
				// pitch halved between them; the ridge through the origin marked.
				var radians = math.radians(_ribAngle.floatValue);
				var along = new float2(math.cos(radians), math.sin(radians));
				var across = new float2(-along.y, along.x);
				var nearest = float.MaxValue;
				var farthest = float.MinValue;
				var reach = 0f;
				foreach (var point in island) {
					nearest = math.min(nearest, math.dot(point, across));
					farthest = math.max(farthest, math.dot(point, across));
					reach = math.max(reach, math.abs(math.dot(point, along)));
				}
				var half = _ribWidth.floatValue * 0.5f;
				var stride = math.max(1, (int)math.ceil((farthest - nearest) / half / 96f));
				Handles.color = dark ? new Color(0.9f, 0.9f, 0.7f, 0.35f) : new Color(0.3f, 0.3f, 0.1f, 0.35f);
				for (var crease = (int)math.floor(nearest / half); crease <= (int)math.ceil(farthest / half); crease += stride) {
					var origin = across * (crease * half) - along * (reach + half);
					foreach (var (enter, exit) in Through(origin, along, island)) {
						Handles.DrawAAPolyLine(crease == 0 ? 1.5f : 1f, Map(origin + along * enter), Map(origin + along * exit));
					}
				}
			}

			if (_ringed.boolValue && !_starburst.boolValue && !_ribbed.boolValue && _ringWidth.floatValue >= InsertRings.MinWidth) {
				// Every ring that reaches the floor, ridges and grooves alike, drawn
				// whole: a circle is cheap to look at and the floor's edge says
				// where it stops. The centre is marked.
				var center = new float2(_ringCenter.vector2Value.x, _ringCenter.vector2Value.y);
				var reach = 0f;
				foreach (var point in island) {
					reach = math.max(reach, math.distance(point, center));
				}
				var half = _ringWidth.floatValue * 0.5f;
				var start = InsertRings.SnapStart(_ringStart.floatValue, _ringWidth.floatValue);
				var rings = (int)math.ceil(math.max(reach - start, 0f) / half);
				var stride = math.max(1, (int)math.ceil(rings / 64f));
				Handles.color = dark ? new Color(0.9f, 0.9f, 0.7f, 0.35f) : new Color(0.3f, 0.3f, 0.1f, 0.35f);
				for (var ring = start > 0f ? 0 : 1; ring <= rings; ring += stride) {
					Handles.DrawWireDisc(Map(center), Vector3.forward, (start + ring * half) * scale);
				}
				var hub = Map(center);
				if (rect.Contains(hub)) {
					Handles.color = dark ? new Color(0.9f, 0.9f, 0.7f) : new Color(0.3f, 0.3f, 0.1f);
					Handles.DrawAAPolyLine(1.5f, hub + Vector3.left * 5f, hub + Vector3.right * 5f);
					Handles.DrawAAPolyLine(1.5f, hub + Vector3.up * 5f, hub + Vector3.down * 5f);
				}
			}

			if (_gem.boolValue && !_starburst.boolValue && !_ribbed.boolValue && !_ringed.boolValue && island.Count > 0) {
				// Every facet's sides, as the generator will lay them: the table,
				// the star triangles and kites round it, and the girdle facets
				// out to the edge. The centre is marked.
				var center = new float2(_gemCenter.vector2Value.x, _gemCenter.vector2Value.y);
				InsertGem gem = null;
				try {
					gem = InsertGem.Cover(island, center, _gemSides.intValue, _gemTable.floatValue, _gemRowLength.floatValue, _gemSpread.floatValue, 0f, 0f);
				} catch (InsertOutlineException) {
				}
				if (gem != null) {
					Handles.color = dark ? new Color(0.9f, 0.9f, 0.7f, 0.35f) : new Color(0.3f, 0.3f, 0.1f, 0.35f);
					foreach (var (from, to, _) in gem.CreasesWithin(island, 1e-4f)) {
						Handles.DrawAAPolyLine(1f, Map(from), Map(to));
					}
					var hub = Map(center);
					if (rect.Contains(hub)) {
						Handles.color = dark ? new Color(0.9f, 0.9f, 0.7f) : new Color(0.3f, 0.3f, 0.1f);
						Handles.DrawAAPolyLine(1.5f, hub + Vector3.left * 5f, hub + Vector3.right * 5f);
						Handles.DrawAAPolyLine(1.5f, hub + Vector3.up * 5f, hub + Vector3.down * 5f);
					}
				}
			}

			// Holes are drawn only where they are cut: through a raised middle.
			for (var i = 0; inset > 0f && i < insert.Holes.Count; i++) {
				var hole = insert.Holes[i];
				var center = new float2(hole.Center.x, hole.Center.y);
				Handles.color = HoleColor;
				Handles.DrawWireDisc(Map(center), Vector3.forward, hole.Radius * scale);
				var corner = Map(center + new float2(hole.Radius, hole.Radius) * 0.75f);
				GUI.Label(new Rect(corner.x, corner.y - 14f, 24f, 16f), i.ToString(), HoleNumber);
			}

			// Inches: what insert parts are catalogued in, and what the trade
			// still talks in.
			var inch = 1f / InsertShapes.VpxUnitsPerInch;
			GUI.Label(new Rect(rect.x, rect.yMax - 18f, rect.width - 6f, 16f),
				$"{outline.Width * inch:0.##} x {outline.Height * inch:0.##} in, seen from above", PreviewLabel);
		}

		/// <summary>
		/// How far in from the outline the floor that holes and the starburst go
		/// on starts: the channel's width as the generator clamped it for the mesh
		/// that is there, or nothing without a channel, when the floor is the
		/// outline itself.
		/// </summary>
		private static float FloorInset(InsertComponent insert) => insert.Body.ChannelWidth;

		/// <summary>
		/// A point well inside the outline, for a new hole to start from: the
		/// middle of the largest triangle the outline cuts into, which unlike the
		/// centroid is inside even when the shape bends in on itself.
		/// </summary>
		private static Vector2 Inside(InsertOutline outline)
		{
			var polygon = outline.Points.Select(point => (double2)point.Position).ToList();
			var best = double2.zero;
			var largest = -1.0;
			foreach (var piece in InsertTriangulation.EarClip(polygon)) {
				var a = polygon[piece.x];
				var b = polygon[piece.y];
				var c = polygon[piece.z];
				var area = math.abs((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x));
				if (area > largest) {
					largest = area;
					best = (a + b + c) / 3.0;
				}
			}
			return largest < 0.0 ? new Vector2(outline.Centroid.x, outline.Centroid.y) : new Vector2((float)best.x, (float)best.y);
		}

		private static Vector3[] Closed(Vector3[] points)
		{
			var closed = new Vector3[points.Length + 1];
			points.CopyTo(closed, 0);
			closed[^1] = points[0];
			return closed;
		}

		private static bool IsConvex(List<float2> polygon)
		{
			var sign = 0f;
			for (var i = 0; i < polygon.Count; i++) {
				var a = polygon[i];
				var b = polygon[(i + 1) % polygon.Count];
				var c = polygon[(i + 2) % polygon.Count];
				var cross = (b.x - a.x) * (c.y - b.y) - (b.y - a.y) * (c.x - b.x);
				if (math.abs(cross) < 1e-9f) {
					continue;
				}
				if (sign == 0f) {
					sign = math.sign(cross);
				} else if (math.sign(cross) != sign) {
					return false;
				}
			}
			return true;
		}

		/// <summary>
		/// The stretches of a ray from <paramref name="origin"/> along
		/// <paramref name="direction"/> that lie inside a polygon, convex or not:
		/// between each entry and the exit after it, starting at the origin when
		/// that is inside. Crossings are counted half open along each edge, so a
		/// ray through a corner counts it once.
		/// </summary>
		private static IEnumerable<(float Enter, float Exit)> Through(float2 origin, float2 direction, List<float2> polygon)
		{
			var crossings = new List<float>();
			for (var i = 0; i < polygon.Count; i++) {
				var a = polygon[i];
				var b = polygon[(i + 1) % polygon.Count];
				// Judged by the edge's ends, so a corner only grazed counts twice or
				// not at all, and one passed through once.
				var sideA = direction.x * (a.y - origin.y) - direction.y * (a.x - origin.x) > 0f;
				var sideB = direction.x * (b.y - origin.y) - direction.y * (b.x - origin.x) > 0f;
				if (sideA == sideB) {
					continue;
				}
				var edge = b - a;
				var delta = a - origin;
				var t = (delta.x * edge.y - delta.y * edge.x) / (direction.x * edge.y - direction.y * edge.x);
				if (t >= 0f) {
					crossings.Add(t);
				}
			}
			crossings.Sort();
			var i0 = 0;
			if (crossings.Count % 2 == 1) {
				yield return (0f, crossings[0]);
				i0 = 1;
			}
			for (var i = i0; i + 1 < crossings.Count; i += 2) {
				yield return (crossings[i], crossings[i + 1]);
			}
		}

		/// <summary>
		/// Buttons that lay holes out along the raised floor's edge, replacing
		/// whatever holes were there. Each hole can be moved afterwards.
		/// </summary>
		private void DrawHolePresets()
		{
			var insert = (InsertComponent)target;
			var outline = insert.Outline;
			var inset = FloorInset(insert);

			using (new EditorGUI.DisabledScope(outline == null || targets.Length > 1)) {
				// A circle's or a banana's corners are its sampling, not places for holes.
				using (new EditorGUI.DisabledScope(Sampled))
				using (new EditorGUILayout.HorizontalScope()) {
					if (GUILayout.Button(new GUIContent("Corners", "One hole at each corner of the raised floor."))) {
						SetHoles(InsertHoles.AtCorners(outline, inset));
					}
					if (GUILayout.Button(new GUIContent("Corners + Sides", "A hole at each corner and one halfway along each side."))) {
						SetHoles(InsertHoles.AtCornersAndSides(outline, inset));
					}
				}
				using (new EditorGUILayout.HorizontalScope()) {
					_aroundCount = EditorGUILayout.IntSlider(new GUIContent("Around", "How many holes to spread evenly along the raised floor's edge."), _aroundCount, 1, 64);
					if (GUILayout.Button("Around", GUILayout.Width(80f))) {
						SetHoles(InsertHoles.Around(outline, inset, _aroundCount));
					}
				}
			}
			// A new hole goes in the middle of this insert's outline, which means
			// nothing to another insert, so it is one at a time like the presets.
			using (new EditorGUI.DisabledScope(targets.Length > 1))
			using (new EditorGUILayout.HorizontalScope()) {
				if (GUILayout.Button(new GUIContent("Add Hole", "A hole of the radius above, in the middle of the insert, to be moved from there."))) {
					var index = _holes.arraySize;
					_holes.InsertArrayElementAtIndex(index);
					var hole = _holes.GetArrayElementAtIndex(index);
					hole.FindPropertyRelative(nameof(InsertHole.Center)).vector2Value = outline != null ? Inside(outline) : Vector2.zero;
					hole.FindPropertyRelative(nameof(InsertHole.Radius)).floatValue = _holeRadius.floatValue;
				}
				var symmetrize = new GUIContent("Symmetrize",
					"Moves holes near the middle line onto it, and sets holes that nearly mirror each other across it to mirror it exactly. " +
					"Near means within 5% of the insert's larger side.");
				using (new EditorGUI.DisabledScope(outline == null || _holes.arraySize == 0)) {
					if (GUILayout.Button(symmetrize)) {
						Symmetrize(0.05f * math.max(outline.Width, outline.Height));
					}
				}
				using (new EditorGUI.DisabledScope(_holes.arraySize == 0)) {
					if (GUILayout.Button(new GUIContent("Clear", "Removes every hole."))) {
						_holes.ClearArray();
					}
				}
			}
		}

		private void Symmetrize(float tolerance)
		{
			var holes = new InsertHole[_holes.arraySize];
			for (var i = 0; i < holes.Length; i++) {
				var hole = _holes.GetArrayElementAtIndex(i);
				holes[i] = new InsertHole(hole.FindPropertyRelative(nameof(InsertHole.Center)).vector2Value,
					hole.FindPropertyRelative(nameof(InsertHole.Radius)).floatValue);
			}
			var symmetric = InsertHoles.Symmetrize(holes, tolerance);
			for (var i = 0; i < symmetric.Length; i++) {
				_holes.GetArrayElementAtIndex(i).FindPropertyRelative(nameof(InsertHole.Center)).vector2Value = symmetric[i].Center;
			}
		}

		private void SetHoles(List<Vector2> centers)
		{
			_holes.ClearArray();
			for (var i = 0; i < centers.Count; i++) {
				_holes.InsertArrayElementAtIndex(i);
				var hole = _holes.GetArrayElementAtIndex(i);
				hole.FindPropertyRelative(nameof(InsertHole.Center)).vector2Value = centers[i];
				hole.FindPropertyRelative(nameof(InsertHole.Radius)).floatValue = _holeRadius.floatValue;
			}
		}

		#endregion

		#region Bake

		/// <summary>
		/// Whether the insert can't be baked where it is, which locks everything
		/// that would write its bake until it's edited in the project.
		/// </summary>
		private bool IsLocked => targets.Any(selected => !InsertBaker.BakesHere((InsertComponent)selected));

		/// <summary>
		/// The one place an insert is edited in the project, when its bake or
		/// materials can't be changed: a prefab's instance then follows a variant
		/// of its prefab, with the others sharing its shape and materials if asked.
		/// </summary>
		private void DrawEditInProject()
		{
			var inserts = targets.OfType<InsertComponent>().Where(InsertBaker.NeedsEditing).ToArray();
			if (inserts.Length == 0) {
				return;
			}
			using (new EditorGUI.DisabledScope(InsertBaker.Baking != null || EditorApplication.isPlayingOrWillChangePlaymode)) {
				if (targets.Length > 1) {
					EditorGUILayout.HelpBox($"{inserts.Length} of the selected inserts have a bake or materials that can't be changed, from a package like the installed asset library or from a locked asset library. Edit them in the project to change their colors, look or shape: the selected inserts of a prefab with the same shape and materials then follow a variant of it.", MessageType.Info);
					if (GUILayout.Button(new GUIContent($"Edit {inserts.Length} selected inserts in project", "Makes a variant of each selected insert's prefab in the project, with copies of what can't be changed, which the inserts of that prefab with the same shape and materials then follow. Inserts of no prefab get copies of their own."))) {
						InsertBaker.EditInProject(inserts);
						GUIUtility.ExitGUI();
					}
					return;
				}
				var insert = inserts[0];
				var sharing = InsertBaker.Sharing(insert);
				var prefabbed = InsertBaker.IsInstanceRoot(insert);
				var lockedBake = !InsertBaker.BakesHere(insert);
				var lockedMaterials = InsertColorPresets.HasReadOnlyMaterial(insert);
				var what = lockedBake ? lockedMaterials ? "bake and materials" : "bake" : "materials";
				var where = lockedBake ? InsertBaker.ReadOnlyReason(insert.Bake) : "a package like the installed asset library, or to a locked asset library";
				var text = prefabbed
					? $"This insert's {what} belong to {where}, which can't be changed. Edit in project makes a variant of its prefab in this project, with copies of its materials, which the insert and its duplicates then follow. Its colors, look and shape can then be changed, and the variant gets a bake of its own when it's baked."
					: $"This insert's {what} belong to {where}, which can't be changed. Edit in project gives it copies of them in a folder of its own in this project. Its colors, look and shape can then be changed.";
				if (sharing.Length > 0) {
					text += $" Edit {sharing.Length + 1} inserts in project does the same for {(sharing.Length == 1 ? "the other insert" : $"the {sharing.Length} other inserts")} of its prefab in the open scenes with the same shape and materials, which then stay alike.";
				}
				EditorGUILayout.HelpBox(text, MessageType.Info);
				using (new EditorGUILayout.HorizontalScope()) {
					var tooltip = prefabbed
						? "Makes a variant of the insert's prefab in its folder in this project, holding this insert's shape and copies of its materials, and makes this insert an instance of it, keeping its position, lamp and other settings. Bakes the variant when this insert's shape has changed."
						: "Copies the bake and materials into a folder of the insert's own in this project, as material variants that keep following the originals until a value is changed.";
					if (GUILayout.Button(new GUIContent("Edit in project", tooltip + (sharing.Length > 0 ? " The other inserts of its prefab stay as they are." : "")))) {
						InsertBaker.EditInProject(insert, System.Array.Empty<InsertComponent>());
						GUIUtility.ExitGUI();
					}
					if (sharing.Length > 0 && GUILayout.Button(new GUIContent($"Edit {sharing.Length + 1} inserts in project", $"Makes a variant of the insert's prefab in its folder in this project, holding this insert's shape and copies of its materials, and makes this insert and the {sharing.Length} others with the same shape and materials instances of it, keeping their positions, lamps and other settings. Bakes the variant when this insert's shape has changed. All of them follow the variant from then on."))) {
						InsertBaker.EditInProject(insert, sharing);
						GUIUtility.ExitGUI();
					}
				}
			}
		}

		private void DrawBakeSettings()
		{
			if (IsLocked) {
				EditorGUILayout.HelpBox("The bake cannot be written to where it is, so its settings are locked. Edit the insert in the project, at the top, to bake it.", MessageType.None);
			}
			using (new EditorGUI.DisabledScope(IsLocked)) {
				var size = _bakeSettings.FindPropertyRelative(nameof(InsertBakeSettings.TextureSize));
				EditorGUILayout.IntPopup(size, TextureSizeNames, TextureSizes, new GUIContent("Texture Size", size.tooltip));
				DrawMapScale(nameof(InsertBakeSettings.OpticalScale), "Optical Normals", size);
				DrawMapScale(nameof(InsertBakeSettings.ResponseScale), "Light Response", size);
				var padding = _bakeSettings.FindPropertyRelative(nameof(InsertBakeSettings.Padding));
				EditorGUILayout.IntSlider(padding, 1, 32, new GUIContent("Padding (px)", padding.tooltip));
				Field(_bakeSettings.FindPropertyRelative(nameof(InsertBakeSettings.EdgeBevel)), "Edge Bevel");
				var grain = _bakeSettings.FindPropertyRelative(nameof(InsertBakeSettings.NormalGrainStrength));
				Field(grain, "Grain Strength");
				using (new EditorGUI.DisabledScope(!grain.hasMultipleDifferentValues && grain.floatValue <= 0f)) {
					Field(_bakeSettings.FindPropertyRelative(nameof(InsertBakeSettings.NormalGrainSize)), "Grain Size");
				}
				Field(_bakeSettings.FindPropertyRelative(nameof(InsertBakeSettings.Unwrap)), "Unwrap in Blender");
				Field(_bakeSettings.FindPropertyRelative(nameof(InsertBakeSettings.PreferUvPackMaster)), "Prefer UVPackMaster");
				Field(_bakeSettings.FindPropertyRelative(nameof(InsertBakeSettings.Gpu)), "Bake on GPU");
				Field(_bakeSettings.FindPropertyRelative(nameof(InsertBakeSettings.HybridOptics)), "Bake Hybrid Optics");
			}
		}

		/// <summary>
		/// A hybrid map's size as a fraction of the normal map's, with the pixels
		/// that makes, since what the user wants to know is whether the map is
		/// big enough, not the fraction.
		/// </summary>
		private void DrawMapScale(string property, string label, SerializedProperty size)
		{
			var scale = _bakeSettings.FindPropertyRelative(property);
			var pixels = Mathf.Max(InsertBakeSettings.MinMapSize, size.intValue / Mathf.Max(1, scale.intValue));
			var text = scale.hasMultipleDifferentValues || size.hasMultipleDifferentValues ? label : $"{label} ({pixels} px)";
			// A setting from before the scales reads zero: the same size, as it was baked.
			if (!scale.hasMultipleDifferentValues && scale.intValue < 1) {
				scale.intValue = 1;
			}
			EditorGUILayout.IntPopup(scale, MapScaleNames, MapScales, new GUIContent(text, scale.tooltip));
		}

		/// <summary>
		/// Which Blender bakes, whether the insert's bake still fits it, and the
		/// buttons to bake, fork or drop the bake. One insert at a time.
		/// </summary>
		private void DrawBake()
		{
			var insert = (InsertComponent)target;

			// The version found goes into the label; only a problem with it gets a box of its own.
			var blender = InsertBaker.Probe();
			using (new EditorGUILayout.HorizontalScope()) {
				var path = InsertBaker.BlenderPath ?? "";
				var found = blender != null && blender.Error == null;
				var label = found ? $"Blender ({blender.Blender})" : "Blender";
				var packer = !found ? "" : blender.UvPackMaster ? " Packs with UVPackMaster." : " Packs with Blender's packer: install UVPackMaster 4 in Blender for tighter packing.";
				var next = EditorGUILayout.DelayedTextField(new GUIContent(label, "The Blender 5 executable that bakes. Found on its own when left empty." + packer), path);
				if (GUILayout.Button("...", GUILayout.Width(28f))) {
					var picked = EditorUtility.OpenFilePanel("Blender Executable", Path.GetDirectoryName(path) ?? "",
						Application.platform == RuntimePlatform.WindowsEditor ? "exe" : "");
					if (!string.IsNullOrEmpty(picked)) {
						next = picked;
					}
				}
				if (next != path) {
					InsertBaker.BlenderPath = next;
				}
			}

			if (blender?.Error != null) {
				EditorGUILayout.HelpBox(blender.Error, MessageType.Error);
			}

			if (targets.Length > 1) {
				return;
			}

			// Every shape is a prefab: one reshaped on its own goes into its prefab, or becomes a new one.
			var reshaped = IsLocked ? null : InsertBaker.ReshapedPrefab(insert);
			if (insert.Bake != null) {
				if (insert.IsBakeFresh) {
					EditorGUILayout.HelpBox($"Baked. UVs packed by {insert.Bake.Packer}, covering {insert.Bake.UvFill:P0} of the texture.", MessageType.Info);
				} else if (IsLocked) {
					EditorGUILayout.HelpBox("The insert geometry has changed, and its bake cannot be updated where it is. Edit the insert in the project, at the top, to bake it.", MessageType.Warning);
				} else if (reshaped != null) {
					// Told below, with the choice of where the shape goes.
				} else if (insert.IsShowingGeometry) {
					EditorGUILayout.HelpBox("The insert geometry has changed, so the renderer is off and the geometry to be baked is drawn in the scene instead, yellow with its wireframe, an engraved line as a trough. Bake Again to put the geometry into the maps and the renderer back on.", MessageType.Warning);
				} else {
					EditorGUILayout.HelpBox("The insert geometry has changed. The material is kept as a preview, but its baked textures may no longer line up. Bake Again to update the UVs, normals and optical maps.", MessageType.Warning);
				}
			}
			var canBake = !(InsertBaker.Baking != null || blender?.Error != null || insert.Error != null || EditorApplication.isPlayingOrWillChangePlaymode);
			if (reshaped != null) {
				var followers = InsertBaker.Followers(insert);
				// Quoted: library names have commas of their own.
				var prefabName = $"\"{Path.GetFileNameWithoutExtension(reshaped)}\"";
				var stale = insert.IsBakeFresh ? "" : insert.IsShowingGeometry ? " Its bake no longer fits: the geometry to be baked is drawn in the scene, yellow with its wireframe." : " Its bake no longer fits.";
				EditorGUILayout.HelpBox(followers.Length == 0
					? $"This insert's shape differs from its prefab, {prefabName}.{stale} Apply the shape to the prefab and bake it there, or bake it as a new prefab of this shape, which this insert then follows."
					: $"This insert's shape differs from its prefab, {prefabName}, which {(followers.Length == 1 ? "another insert" : $"{followers.Length} other inserts")} in the open scenes follow.{stale} Apply the shape to the prefab and bake it there, for all of them, or bake it as a new prefab of this shape, which only this insert then follows.", MessageType.Warning);
				using (new EditorGUI.DisabledScope(!canBake))
				using (new EditorGUILayout.HorizontalScope()) {
					var applyLabel = followers.Length == 0 ? "Apply to prefab" : $"Apply to all {followers.Length + 1} inserts";
					if (GUILayout.Button(new GUIContent(applyLabel, $"Puts this insert's shape into {prefabName}, which every instance of it that keeps the prefab's shape then has too, and bakes the prefab."))) {
						InsertBaker.ApplyShape(insert);
						InsertBaker.Bake(insert, prefab: reshaped);
						GUIUtility.ExitGUI();
					}
					if (GUILayout.Button(new GUIContent("Bake as new prefab", $"Makes a variant of {prefabName} in its folder, holding this insert's shape and a copy of its material, makes this insert an instance of it, and bakes it, which gives the new prefab a bake of its own. Duplicates of this insert then follow the new prefab, and the other instances of {prefabName} stay as they are."))) {
						InsertBaker.ForkIntoVariant(insert, System.Array.Empty<InsertComponent>());
						if (InsertBaker.Baking == null) {
							InsertBaker.Bake(insert);
						}
						GUIUtility.ExitGUI();
					}
				}
			}

			if (InsertBaker.LastError != null && InsertBaker.LastErrorInsert == insert) {
				EditorGUILayout.HelpBox(InsertBaker.LastError, MessageType.Error);
				if (InsertBaker.LastLog.Count > 0 && GUILayout.Button("Copy Blender's Log")) {
					EditorGUIUtility.systemCopyBuffer = string.Join(System.Environment.NewLine, InsertBaker.LastLog);
				}
			}

			using (new EditorGUILayout.HorizontalScope()) {
				using (new EditorGUI.DisabledScope(IsLocked || reshaped != null || !canBake)) {
					if (GUILayout.Button(new GUIContent(insert.Bake != null ? "Bake Again" : "Bake", "Unwraps and packs the UVs and bakes the normal map, and the optical maps when asked, in Blender. An instance of a prefab in the project bakes the prefab, which all its instances wear. Ctrl/Cmd+Shift+click to bake into the package the insert's prefab comes from, next to the prefab, when that package can be written to."))) {
						InsertBaker.Bake(insert, toPackage: EditorGUI.actionKey && Event.current.shift);
					}
				}
				using (new EditorGUI.DisabledScope(insert.Bake == null || IsLocked)) {
					if (GUILayout.Button(new GUIContent("Clear Bake", "Drops the bake and puts the original material back. The bake's assets stay on disk."))) {
						Undo.RecordObjects(new Object[] { insert, insert.GetComponent<MeshRenderer>() }, "Clear Insert Bake");
						insert.SetBake(null);
					}
				}
			}
		}

		private void DrawHybridMaterial()
		{
			if (targets.Length > 1) {
				return;
			}
			var insert = (InsertComponent)target;
			var data = insert.Bake;
			if (data == null || data.OpticalNormalMap == null || data.OpticalResponseMap == null) {
				return;
			}
			if (data.Material != null && data.Material.HasProperty("_OpticalNormalMap")) {
				var lighting = insert.GetComponent<InsertLightComponent>();
				if (lighting == null || lighting.SpillLight == null) {
					using (new EditorGUI.DisabledScope(EditorApplication.isPlaying)) {
						if (GUILayout.Button(new GUIContent("Add Intensity Controls and Spill Light", "Adds the Insert Light component, with a wide spot light just under the insert, pointing up through it at the ball and whatever else is above."))) {
							AddLighting(insert);
						}
					}
				}
				return;
			}
			using (new EditorGUI.DisabledScope(!insert.IsBakeFresh || InsertBaker.IsReadOnly(insert.Bake) || InsertBaker.Baking != null || EditorApplication.isPlaying || Shader.Find(InsertBaker.HybridShaderName) == null)) {
				if (GUILayout.Button(new GUIContent("Use HDRP Hybrid Material", "Creates a hybrid material with the baked optical maps and puts it on the insert. The previous material stays on disk."))) {
					InsertBaker.UseHybridMaterial(insert);
				}
			}
		}

		/// <summary>
		/// Color presets switch the insert's body and cover materials; look
		/// presets are what the body material follows for its numeric settings,
		/// its colour and Max Brightness aside.
		/// </summary>
		private void DrawPresets()
		{
			var inserts = targets.OfType<InsertComponent>().ToArray();
			// Ctrl/Cmd+click highlights a preset; with Shift, new materials go into a package that can be written to.
			var toPackage = EditorGUI.actionKey && Event.current.shift;
			var revealAsset = EditorGUI.actionKey && !toPackage;
			SubHeader("Color");
			var colors = InsertColorPresets.Find();
			if (colors.Length == 0) {
				EditorGUILayout.HelpBox("No insert color presets found. Pinball → Insert Presets lists where they live and adds one.", MessageType.Warning);
			}
			var worn = new HashSet<int>();
			var current = inserts.Select(insert => insert.GetComponent<MeshRenderer>().sharedMaterial).Where(material => material != null).Select(InsertColorPresets.LinkedPreset).Distinct().ToArray();
			if (current.Length == 1 && current[0] != null) {
				worn.Add(System.Array.IndexOf(colors, current[0]));
			}
			using (new EditorGUI.DisabledScope(!revealAsset && (EditorApplication.isPlaying || !inserts.Any(InsertColorPresets.CanApply)))) {
				var labels = colors.Select(color => InsertPresetGui.Label(color, $"{color.ColorName}. {InsertPresetGui.Where(color)} Materials made for it follow the preset when it changes. Ctrl/Cmd+click to highlight the asset. Ctrl/Cmd+Shift+click to create missing materials next to the insert's own when those are in a package that can be written to, instead of in the project.")).ToArray();
				var clicked = InsertPresetGui.Buttons(labels, worn, () => InsertPresetGui.EditButton(InsertPresetWindow.Kind.Color, current.Length == 1 ? current[0] : null), 64f);
				if (clicked >= 0) {
					var color = colors[clicked];
					if (revealAsset) {
						EditorGUIUtility.PingObject(color);
					} else {
						Undo.IncrementCurrentGroup();
						var group = Undo.GetCurrentGroup();
						Undo.SetCurrentGroupName("Apply Insert Color Preset");
						var changed = 0;
						try {
							foreach (var insert in inserts) {
								if (InsertColorPresets.Apply(color, insert, toPackage)) {
									changed++;
								}
							}
						} finally {
							Undo.CollapseUndoOperations(group);
						}
						Debug.Log($"Applied {color.ColorName} color preset to {changed} insert(s).");
					}
				}
			}

			// A library insert's colors are changed in the project, from the top.
			if (inserts.Any(InsertColorPresets.HasReadOnlyMaterial)) {
				EditorGUILayout.HelpBox("The colors above switch the insert to other materials. To change the colors themselves, edit the insert in the project, at the top.", MessageType.None);
			}

			SubHeader("Look");
			var styled = inserts.Count(InsertMaterialPresets.HasStyledMaterial);
			var bodies = inserts.Select(insert => insert.GetComponent<MeshRenderer>().sharedMaterial).Where(InsertMaterialPresets.CanLink).Distinct().ToArray();
			var followed = bodies.Select(InsertMaterialPresets.LinkedPreset).Distinct().ToArray();
			// Linking a look writes it into the body material, which a library's can't take.
			var lockedBody = inserts.Any(insert => InsertColorPresets.IsReadOnly(insert, insert.GetComponent<MeshRenderer>().sharedMaterial));
			if (lockedBody) {
				EditorGUILayout.HelpBox("The body material can't be changed, so no look can be linked to it. Edit the insert in the project first, at the top.", MessageType.None);
			} else if (styled == 0) {
				EditorGUILayout.HelpBox("A look is how the insert is moulded and behaves optically: every numeric setting of the hybrid body material, but not its colours and Max Brightness, which are the Color's, nor the bulb, which is the Lamp's. Bake the insert and click Use HDRP Hybrid Material first.", MessageType.None);
			} else if (followed.Length == 1 && followed[0] != null) {
				var edited = bodies.Sum(InsertMaterialPresets.Edited);
				EditorGUILayout.HelpBox(edited == 0
					? $"The body material follows the look {followed[0].name}. Clear Plastic and Colored Plastic ship with the package; Manage adds your own."
					: $"The body material follows the look {followed[0].name}, with {edited} values edited in the material, where they are marked •. The look overwrites them when it changes.", MessageType.None);
			} else {
				EditorGUILayout.HelpBox(followed.Length > 1 ? "The selected inserts follow different looks." : "The body material follows no look: its numeric settings are its own. Click a look to link it; Clear Plastic and Colored Plastic ship with the package.", MessageType.None);
			}

			var presets = InsertMaterialPresets.Find();
			var following = new HashSet<int>();
			if (followed.Length == 1 && followed[0] != null) {
				following.Add(System.Array.IndexOf(presets, followed[0]));
			}
			using (new EditorGUI.DisabledScope(!revealAsset && (styled == 0 || lockedBody || EditorApplication.isPlaying))) {
				var labels = presets.Select(preset => InsertPresetGui.Label(preset, $"{preset.name}: links every selected insert's body material to this look, which gives it the look's values now and whenever the look changes. {InsertPresetGui.Where(preset)} Colors, Max Brightness, maps, lamps and the cover stay as they are. A shared body material changes for all inserts that use it. Ctrl/Cmd+click to highlight the asset.")).ToArray();
				var clicked = InsertPresetGui.Buttons(labels, following, () => InsertPresetGui.EditButton(InsertPresetWindow.Kind.Look, followed.Length == 1 ? followed[0] : null), 92f);
				if (clicked >= 0) {
					var preset = presets[clicked];
					if (revealAsset) {
						EditorGUIUtility.PingObject(preset);
					} else if (InsertMaterialPresets.LinkAsking(bodies, preset, bodies.Length == 1, "This insert's")) {
						Debug.Log($"Linked {bodies.Length} body material(s) to the look \"{preset.name}\".");
					}
					GUIUtility.ExitGUI();
				}
			}
			if (presets.Length == 0) {
				EditorGUILayout.HelpBox("No look presets found. The HDRP package ships Clear Plastic and Colored Plastic; Manage adds your own.", MessageType.Warning);
			}
		}

		public static InsertLightComponent AddLighting(InsertComponent insert)
		{
			var lighting = insert.GetComponent<InsertLightComponent>() ?? Undo.AddComponent<InsertLightComponent>(insert.gameObject);
			if (lighting.SpillLight == null) {
				var go = new GameObject("Insert Spill Light");
				Undo.RegisterCreatedObjectUndo(go, "Add Insert Spill Light");
				go.transform.SetParent(insert.transform, false);
				var light = go.AddComponent<Light>();
				var converter = RenderPipeline.Current.LightConverter;
				converter.UpdateLight(light, new VisualPinball.Engine.VPT.Light.LightData(), true);
				Undo.RecordObject(lighting, "Assign Insert Spill Light");
				lighting.SpillLight = light;
				MakeSpillSpot(lighting);
				PrefabUtility.RecordPrefabInstancePropertyModifications(lighting);
			}
			lighting.Apply();
			return lighting;
		}

		/// <summary>
		/// Makes the insert's spill light the spot it is meant to be: under the
		/// insert, pointing up through it, a wide soft cone with no shadows. A
		/// spill made before this was a point light above the insert, dearer
		/// and harder to aim; this turns it round, and gives it the spot's
		/// range and lumens where it still had the point light's defaults.
		/// </summary>
		public static void MakeSpillSpot(InsertLightComponent lighting)
		{
			var light = lighting.SpillLight;
			if (light == null) {
				return;
			}
			var bounds = lighting.GetComponent<MeshFilter>().sharedMesh.bounds;
			// The whole light object, so the pipeline's own light data, added or
			// changed inside InsertSpill, comes back on undo as well.
			Undo.RecordObject(lighting, "Make Insert Spill a Spot Light");
			Undo.RegisterFullObjectHierarchyUndo(light.gameObject, "Make Insert Spill a Spot Light");
			// Placed through the insert's transform, so a light that is not the
			// insert's own child still ends up under the insert, pointing up it.
			var insert = lighting.transform;
			light.transform.position = insert.TransformPoint(new Vector3(bounds.center.x, bounds.min.y - 0.0015f, bounds.center.z));
			light.transform.rotation = insert.rotation * Quaternion.Euler(-90f, 0f, 0f);
			var converter = RenderPipeline.Current.LightConverter;
			converter.InsertSpill(light);
			converter.SetShadow(light, false, false);
			if (Mathf.Approximately(lighting.SpillRange, InsertLightComponent.PointSpillRange)) {
				lighting.SpillRange = InsertLightComponent.SpotSpillRange;
			}
			if (Mathf.Approximately(lighting.SpillLumens, InsertLightComponent.PointSpillLumens)) {
				lighting.SpillLumens = InsertLightComponent.SpotSpillLumens;
			}
			lighting.Apply();
			EditorUtility.SetDirty(lighting);
			PrefabUtility.RecordPrefabInstancePropertyModifications(lighting);
			foreach (var component in light.GetComponents<Component>()) {
				EditorUtility.SetDirty(component);
				PrefabUtility.RecordPrefabInstancePropertyModifications(component);
			}
		}

		#endregion

		#region Cover

		private void DrawCover()
		{
			// Through the component, which puts the material in the renderer's
			// second slot: the slot is the cover, and the field only mirrors it.
			EditorGUI.showMixedValue = _coverMaterial.hasMultipleDifferentValues;
			EditorGUI.BeginChangeCheck();
			var chosen = (Material)EditorGUILayout.ObjectField(new GUIContent("Material", _coverMaterial.tooltip), _coverMaterial.objectReferenceValue, typeof(Material), false);
			EditorGUI.showMixedValue = false;
			if (EditorGUI.EndChangeCheck()) {
				var objects = targets.SelectMany(t => new Object[] { t, ((InsertComponent)t).GetComponent<MeshRenderer>() }).ToArray();
				Undo.RecordObjects(objects, "Set Insert Cover Material");
				foreach (var t in targets) {
					var insert = (InsertComponent)t;
					insert.SetCoverMaterial(chosen);
					PrefabUtility.RecordPrefabInstancePropertyModifications(insert);
					PrefabUtility.RecordPrefabInstancePropertyModifications(insert.GetComponent<MeshRenderer>());
				}
				serializedObject.Update();
			}
			var cover = _coverMaterial.hasMultipleDifferentValues || _coverMaterial.objectReferenceValue != null;
			using (new EditorGUI.DisabledScope(!cover)) {
				Field(_usePlayfieldNormals, "Use Playfield Normals");
				if (_usePlayfieldNormals.boolValue || _usePlayfieldNormals.hasMultipleDifferentValues) {
					Field(_coverPlayfield, "Playfield Override");
					if (targets.Length == 1 && ((InsertComponent)target).CoverPlayfield != null) {
						var playfield = ((InsertComponent)target).CoverPlayfield;
						var material = playfield.sharedMaterial;
						using (new EditorGUI.DisabledScope(true)) {
							EditorGUILayout.ObjectField(new GUIContent("Detected Surface", "The renderer whose normal map and UVs the cover follows."), playfield, typeof(MeshRenderer), true);
							EditorGUILayout.ObjectField("Source Material", material, typeof(Material), false);
							EditorGUILayout.ObjectField("Source Normal Map", material != null && material.HasProperty("_NormalMap") ? material.GetTexture("_NormalMap") : null, typeof(Texture), false);
						}
					}
				}
			}
		}

		public static Material AddCover(InsertComponent insert)
		{
			if (insert.CoverMaterial != null) {
				return insert.CoverMaterial;
			}
			var shader = Shader.Find("Pinball/Insert Cover");
			if (shader == null) {
				throw new System.InvalidOperationException("The HDRP insert cover shader is not installed.");
			}
			// Where the insert's other materials go, named after it like them.
			var folder = InsertBaker.MaterialFolder(insert);
			var material = new Material(shader) { name = $"{insert.name} Cover" };
			AssetDatabase.CreateAsset(material, AssetDatabase.GenerateUniqueAssetPath($"{folder}/{InsertBaker.FileName(insert.name)} Cover.mat"));
			Undo.RecordObjects(new Object[] { insert, insert.GetComponent<MeshRenderer>() }, "Add Clear Insert Cover");
			insert.SetCoverMaterial(material);
			EditorUtility.SetDirty(insert);
			PrefabUtility.RecordPrefabInstancePropertyModifications(insert);
			PrefabUtility.RecordPrefabInstancePropertyModifications(insert.GetComponent<MeshRenderer>());
			AssetDatabase.SaveAssetIfDirty(material);
			return material;
		}

		#endregion

		#region Status and scene

		private void DrawStatus()
		{
			var insert = (InsertComponent)target;
			if (insert.Error != null) {
				EditorGUILayout.HelpBox($"{insert.Error}\n\nShowing the last mesh that worked.", MessageType.Error);
			}
			if (insert.CoverWarning != null) {
				EditorGUILayout.HelpBox(insert.CoverWarning, MessageType.Warning);
			}

			foreach (var warning in insert.Warnings) {
				EditorGUILayout.HelpBox(warning, MessageType.Warning);
			}

			if (insert.Outline != null) {
				// The real-world size is the quickest way to catch a value that was
				// typed in the wrong unit.
				var inch = 1f / InsertShapes.VpxUnitsPerInch;
				var millimetre = InsertShapes.MetresPerInch * 1000f * inch;
				EditorGUILayout.HelpBox(
					$"{insert.Outline.Width * inch:0.###} x {insert.Outline.Height * inch:0.###} in " +
					$"({insert.Outline.Width * millimetre:0.#} x {insert.Outline.Height * millimetre:0.#} mm)\n" +
					$"{insert.Outline.Points.Count} outline vertices, {insert.Outline.Perimeter:0.#} VPX units around.",
					MessageType.None);
			}
		}

		private void OnSceneGUI()
		{
			var insert = (InsertComponent)target;
			var outline = insert.Outline;
			if (outline == null) {
				return;
			}

			var matrix = insert.transform.localToWorldMatrix;
			var points = new Vector3[outline.Points.Count + 1];
			for (var i = 0; i < outline.Points.Count; i++) {
				points[i] = matrix.MultiplyPoint3x4(InsertMeshGenerator.ToLocal(outline.Points[i].Position));
			}
			points[^1] = points[0];

			Handles.color = Color.cyan;
			Handles.DrawPolyLine(points);

			// Corner samples and straight-run subdivisions are told apart on sight,
			// which is the quickest way to see what a segment count actually did.
			var size = HandleUtility.GetHandleSize(points[0]) * 0.02f;
			for (var i = 0; i < outline.Points.Count; i++) {
				Handles.color = outline.Points[i].Kind switch {
					InsertOutlinePointKind.Arc => Color.yellow,
					InsertOutlinePointKind.Side => Color.grey,
					_ => Color.red,
				};
				Handles.DotHandleCap(0, points[i], Quaternion.identity, size, EventType.Repaint);
			}

			DrawHoleHandles(insert);
		}

		/// <summary>
		/// A handle on each hole's centre that drags it across the insert's
		/// plane, and one on its rim that sets its radius.
		/// </summary>
		/// <remarks>
		/// Works on the component itself: the serialized object is off limits in
		/// scene GUI. Every change is recorded for undo first.
		/// </remarks>
		private static void DrawHoleHandles(InsertComponent insert)
		{
			var transform = insert.transform;
			var matrix = transform.localToWorldMatrix;
			var up = transform.TransformDirection(InsertMeshGenerator.ToLocal(float2.zero, 1f)).normalized;
			var across = transform.TransformDirection(InsertMeshGenerator.ToLocal(new float2(1f, 0f))).normalized;
			var along = transform.TransformDirection(InsertMeshGenerator.ToLocal(new float2(0f, 1f))).normalized;

			for (var i = 0; i < insert.Holes.Count; i++) {
				var hole = insert.Holes[i];
				var center = new float2(hole.Center.x, hole.Center.y);

				var world = matrix.MultiplyPoint3x4(InsertMeshGenerator.ToLocal(center));
				var rim = matrix.MultiplyPoint3x4(InsertMeshGenerator.ToLocal(center + new float2(hole.Radius, 0f)));
				var scale = HandleUtility.GetHandleSize(world);

				Handles.color = HoleColor;
				Handles.DrawWireDisc(world, up, Vector3.Distance(world, rim));

				// Numbered as the list numbers its elements, so a hole in the scene
				// can be found in the inspector and the other way round.
				var corner = matrix.MultiplyPoint3x4(InsertMeshGenerator.ToLocal(center + new float2(0.75f, 0.75f) * hole.Radius));
				Handles.Label(corner, i.ToString(), HoleLabel);

				EditorGUI.BeginChangeCheck();
				var moved = Handles.Slider2D(world, up, across, along, scale * 0.06f, Handles.CircleHandleCap, Vector2.zero);
				if (EditorGUI.EndChangeCheck()) {
					var plan = InsertMeshGenerator.FromLocal(transform.InverseTransformPoint(moved));
					SetHole(insert, i, new InsertHole(new Vector2(plan.x, plan.y), hole.Radius), "Move Hole");
					continue;
				}

				EditorGUI.BeginChangeCheck();
				var stretched = Handles.Slider(rim, rim - world, scale * 0.04f, Handles.DotHandleCap, 0f);
				if (EditorGUI.EndChangeCheck()) {
					var plan = InsertMeshGenerator.FromLocal(transform.InverseTransformPoint(stretched));
					var radius = math.max(0.01f, math.distance(plan, center));
					SetHole(insert, i, new InsertHole(hole.Center, radius), "Resize Hole");
				}
			}
		}

		private static void SetHole(InsertComponent insert, int index, InsertHole hole, string action)
		{
			Undo.RecordObject(insert, action);
			insert.SetHole(index, hole);
			PrefabUtility.RecordPrefabInstancePropertyModifications(insert);
		}

		#endregion
	}
}
