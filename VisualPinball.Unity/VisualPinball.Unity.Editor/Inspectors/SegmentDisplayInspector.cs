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
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{
	[CanEditMultipleObjects]
	[CustomEditor(typeof(SegmentDisplayAuthoring))]
	public class SegmentDisplayInspector : DisplayInspector
	{
		[NonSerialized] private SegmentDisplayAuthoring _mb;
		[NonSerialized] private SegmentDisplayAuthoring[] _mbs;

		private float _skewAngleDeg;
		private string _testText;
		private bool _foldoutStyle = true;

		private void OnEnable()
		{
			_mb = target as SegmentDisplayAuthoring;
			_mbs = targets.Select(t => t as SegmentDisplayAuthoring).ToArray();
			_skewAngleDeg = math.degrees(_mb.SkewAngle);
			base.OnEnable();
		}

		public override void OnInspectorGUI()
		{
			_mb.Id = EditorGUILayout.TextField("Id", _mb.Id);
			EditorGUILayout.LabelField("Segment Type", _mb.SegmentTypeName);

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
				}

				var segWidth = EditorGUILayout.Slider("Weight", _mb.SegmentWeight, 0.005f, 0.11f);
				if (segWidth != _mb.SegmentWeight) {
					RecordUndo("Change Segment Weight", this);
					foreach (var mb in _mbs) {
						mb.SegmentWeight = segWidth;
					}
				}

				var innerPadding = EditorGUILayout.Vector2Field("Padding", _mb.Padding);
				if (innerPadding.x != _mb.Padding.x || innerPadding.y != _mb.Padding.y) {
					RecordUndo("Change Segment Padding", this);
					foreach (var mb in _mbs) {
						mb.Padding = innerPadding;
					}
				}

				EditorGUI.indentLevel--;
			}

			var text = EditorGUILayout.TextField("Test Text", _testText);
			if (text != _testText) {
				_mb.SetText(text);
				_testText = text;
			}

			if (GUILayout.Button("Test Alphanum")) {
				// ReSharper disable once PossibleNullReferenceException
				(target as SegmentDisplayAuthoring).SetTestData();
			}
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
