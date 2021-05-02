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

using System;
using System.Linq;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	[CanEditMultipleObjects]
	[CustomEditor(typeof(SegmentDisplayAuthoring))]
	public class SegmentDisplayInspector : DisplayInspector
	{
		[NonSerialized] private SegmentDisplayAuthoring _mb;
		[NonSerialized] private SegmentDisplayAuthoring[] _mbs;

		private float _skewAngleDeg;
		private float _segmentWidth;
		private string _testText;

		private void OnEnable()
		{
			_mb = target as SegmentDisplayAuthoring;
			_mbs = targets.Select(t => t as SegmentDisplayAuthoring).ToArray();
			_skewAngleDeg = math.degrees(_mb.SkewAngle);
			_segmentWidth = _mb.SegmentWidth;
			base.OnEnable();
		}

		public override void OnInspectorGUI()
		{
			_mb.Id = EditorGUILayout.TextField("Id", _mb.Id);

			base.OnInspectorGUI();

			var width = EditorGUILayout.IntSlider("Chars", _mb.NumChars, 1, 20);
			if (width != _mb.NumChars) {
				_mb.NumChars = width;
				_mb.RegenerateMesh();
			}

			var skewAngleDeg = EditorGUILayout.Slider("Skew Angle", _skewAngleDeg, -45f, 45f);
			if (skewAngleDeg != _skewAngleDeg) {
				foreach (var mb in _mbs) {
					mb.SkewAngle = math.radians(skewAngleDeg);
				}
				_skewAngleDeg = skewAngleDeg;
			}

			var segWidth = EditorGUILayout.Slider("Weight", _segmentWidth, 0.005f, 0.11f);
			if (segWidth != _segmentWidth) {
				foreach (var mb in _mbs) {
					mb.SegmentWidth = segWidth;
				}
				_segmentWidth = segWidth;
			}

			var ar = EditorGUILayout.Slider("Width", _mb.AspectRatio, 0.1f, 3f);
			if (ar != _mb.AspectRatio) {
				foreach (var mb in _mbs) {
					mb.AspectRatio = ar;
					mb.RegenerateMesh();
				}
			}

			var innerPadding = EditorGUILayout.Vector2Field("Inner Padding", _mb.InnerPadding);
			if (innerPadding.x != _mb.InnerPadding.x || innerPadding.y != _mb.InnerPadding.y) {
				foreach (var mb in _mbs) {
					mb.InnerPadding = innerPadding;
				}
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
