// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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

// ReSharper disable CheckNamespace
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable AssignmentInConditionalExpression

using System;
using System.Linq;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(SegmentDisplayAuthoring)), CanEditMultipleObjects]
	public class SegmentDisplayInspector : DisplayInspector
	{
		private static readonly (int, string)[] NumSegmentsTypes = {
			(7, "Seven-segment"),
			(9, "Nine-segment"),
			(14, "Fourteen-segment"),
			(16, "Sixteen-segment"),
		};

		private static readonly (int, string)[] SeparatorTypes = {
			(0, "None"),
			(1, "Period"),
			(2, "Two-segment comma"),
		};

		[NonSerialized] private SegmentDisplayAuthoring _mb;
		[NonSerialized] private SegmentDisplayAuthoring[] _mbs;

		private int _numSegmentsIndex;
		private int _separatorTypeIndex;
		private float _skewAngleDeg;
		private string _testText;
		private bool _foldoutStyle = true;

		private new void OnEnable()
		{
			_mb = target as SegmentDisplayAuthoring;
			_mbs = targets.Select(t => t as SegmentDisplayAuthoring).ToArray();
			_skewAngleDeg = math.degrees(_mb.SkewAngle);
			_numSegmentsIndex = NumSegmentsTypes
				.Select((tuple, index) => new {tuple, index})
				.First(pair => pair.tuple.Item1 == _mb.NumSegments).index;
			_separatorTypeIndex = SeparatorTypes
				.Select((tuple, index) => new {tuple, index})
				.First(pair => pair.tuple.Item1 == _mb.SeparatorType).index;
			base.OnEnable();
		}

		public override void OnInspectorGUI()
		{
			_mb.Id = EditorGUILayout.TextField("Id", _mb.Id);
			EditorGUILayout.LabelField("Segment Type", _mb.SegmentTypeName);

			EditorGUI.BeginChangeCheck();
			var typeIndex = EditorGUILayout.Popup("Type", _numSegmentsIndex, NumSegmentsTypes.Select(s => s.Item2).ToArray());
			if (EditorGUI.EndChangeCheck()) {
				_numSegmentsIndex = typeIndex;
				RecordUndo("Change Number of Segments", this);
				foreach (var mb in _mbs) {
					mb.NumSegments = NumSegmentsTypes[typeIndex].Item1;
					mb.HorizontalMiddle = 0f;
				}
			}

			EditorGUI.BeginChangeCheck();
			var sepIndex = EditorGUILayout.Popup("Separator", _separatorTypeIndex, SeparatorTypes.Select(s => s.Item2).ToArray());
			if (EditorGUI.EndChangeCheck()) {
				_separatorTypeIndex = sepIndex;
				RecordUndo("Change Separator Type", this);
				foreach (var mb in _mbs) {
					mb.SeparatorType = SeparatorTypes[sepIndex].Item1;
				}
			}

			var width = EditorGUILayout.IntSlider("Chars", _mb.NumChars, 1, 20);
			if (width != _mb.NumChars) {
				_mb.NumChars = width;
				_mb.RegenerateMesh();
			}

			var ar = EditorGUILayout.Slider("Width", _mb.AspectRatio, 0.1f, 3f);
			if (ar != _mb.AspectRatio) {
				foreach (var mb in _mbs) {
					mb.AspectRatio = ar;
					mb.RegenerateMesh();
				}
			}

			if (_foldoutStyle = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutStyle, "Style")) {
				EditorGUI.indentLevel++;
				base.OnInspectorGUI();

				var skewAngleDeg = EditorGUILayout.Slider("Skew Angle", _skewAngleDeg, -45f, 45f);
				if (skewAngleDeg != _skewAngleDeg) {
					RecordUndo("Change Skew Angle", this);
					foreach (var mb in _mbs) {
						mb.SkewAngle = math.radians(skewAngleDeg);
					}
					_skewAngleDeg = skewAngleDeg;
				}

				var segWeight = EditorGUILayout.Slider("Weight", _mb.SegmentWeight, 0.005f, 0.11f);
				if (segWeight != _mb.SegmentWeight) {
					RecordUndo("Change Segment Weight", this);
					foreach (var mb in _mbs) {
						mb.SegmentWeight = segWeight;
					}
				}

				if (_mb.NumSegments == 9) {
					var hPos = EditorGUILayout.Slider("Middle Shift", _mb.HorizontalMiddle, -0.5f, 0.5f);
					if (hPos != _mb.HorizontalMiddle) {
						RecordUndo("Change Segment Middle", this);
						foreach (var mb in _mbs) {
							mb.HorizontalMiddle = hPos;
						}
					}
				}

				var padding = EditorGUILayout.Vector2Field("Padding", _mb.Padding);
				if (padding.x != _mb.Padding.x || padding.y != _mb.Padding.y) {
					RecordUndo("Change Segment Padding", this);
					foreach (var mb in _mbs) {
						mb.Padding = padding;
					}
				}

				var separatorPos = EditorGUILayout.Vector2Field("Separator Position", _mb.SeparatorPos);
				if (separatorPos.x != _mb.SeparatorPos.x || separatorPos.y != _mb.SeparatorPos.y) {
					RecordUndo("Change Separator Position", this);
					foreach (var mb in _mbs) {
						mb.SeparatorPos = separatorPos;
					}
				}

				var separatorEveryThree = EditorGUILayout.ToggleLeft("Separator Every Third Only", _mb.SeparatorEveryThreeOnly);
				if (separatorEveryThree != _mb.SeparatorEveryThreeOnly) {
					RecordUndo("Change Separator Visibility", this);
					foreach (var mb in _mbs) {
						mb.SeparatorEveryThreeOnly = separatorEveryThree;
					}
				}

				EditorGUI.indentLevel--;
			}

			EditorGUILayout.Space();
			EditorGUILayout.Separator();

			EditorGUI.BeginDisabledGroup(_mb.NumSegments < 14);
			var text = EditorGUILayout.TextField("Test Text", _testText);
			if (text != _testText) {
				_mb.SetText(text);
				_testText = text;
			}
			EditorGUI.EndDisabledGroup();

			// if (GUILayout.Button("Test Alphanum")) {
			// 	// ReSharper disable once PossibleNullReferenceException
			// 	(target as SegmentDisplayAuthoring).SetTestData();
			// }
		}

		[MenuItem("GameObject/Visual Pinball/Segment Display", false, 13)]
		private static void CreateSegmentDisplayGameObject()
		{
			var go = new GameObject {
				name = "Segment Display"
			};

			if (Selection.activeGameObject != null) {
				go.transform.parent = Selection.activeGameObject.transform;

			} else {
				go.transform.localPosition = new Vector3(0f, 0.36f, 1.1f);
				go.transform.localScale = new Vector3(GameObjectScale, GameObjectScale, GameObjectScale);
			}

			var display = go.AddComponent<SegmentDisplayAuthoring>();
			display.UpdateDimensions(6, 1);
		}
	}
}
