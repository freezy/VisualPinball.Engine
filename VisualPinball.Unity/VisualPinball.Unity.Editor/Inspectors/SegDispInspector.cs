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

using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEditor;

namespace VisualPinball.Unity.Editor
{

	[CustomEditor(typeof(SegDisplayAuthoring)), CanEditMultipleObjects]
	public class SegDispInspector : DisplayInspector
	{
		private SegDisplayAuthoring _mb;

		private float _skewAngle;
		private float _segmentWidth;

		private void OnEnable()
		{
			_mb = target as SegDisplayAuthoring;
			_skewAngle = -math.degrees(_mb.SkewAngle);
			_segmentWidth = _mb.SegmentWidth;
		}
		public override void OnInspectorGUI()
		{
			EditorGUI.BeginChangeCheck();

			_mb.Color = EditorGUILayout.ColorField("Color", _mb.Color);

			var width = EditorGUILayout.IntSlider("Chars", _mb.Width, 1, 20);
			if (width != _mb.Width) {
				_mb.Width = width;
				_mb.RegenerateMesh();
			}

			var skew = EditorGUILayout.Slider("Skew Angle", _skewAngle, -45f, 45f);
			if (skew != _skewAngle) {
				_mb.SkewAngle = -math.radians(skew);
				_skewAngle = skew;
			}

			var segWidth = EditorGUILayout.Slider("Segment Width", _segmentWidth, 0.005f, 0.11f);
			if (segWidth != _segmentWidth) {
				_mb.SegmentWidth = segWidth;
			}

			var ar = EditorGUILayout.Slider("Width", _mb.AspectRatio, 0.1f, 3f);
			if (ar != _mb.AspectRatio) {
				_mb.AspectRatio = ar;
				_mb.RegenerateMesh();
			}

			_mb.OuterPadding = EditorGUILayout.Vector2Field("Outer Padding", _mb.OuterPadding);
			_mb.InnerPadding = EditorGUILayout.Vector2Field("Inner Padding", _mb.InnerPadding);

			base.OnInspectorGUI();
		}
	}
}
