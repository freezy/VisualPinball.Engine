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

// ReSharper disable AssignmentInConditionalExpression

using UnityEditor;
using VisualPinball.Engine.VPT.Surface;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(SurfaceAuthoring))]
	public class SurfaceInspector : DragPointsItemInspector<Surface, SurfaceData, SurfaceAuthoring>
	{
		private SerializedProperty _heightTopProperty;
		private SerializedProperty _heightBottomProperty;
		private SerializedProperty _isDroppableProperty;

		protected override void OnEnable()
		{
			base.OnEnable();

			_heightTopProperty = serializedObject.FindProperty(nameof(SurfaceAuthoring.HeightTop));
			_heightBottomProperty = serializedObject.FindProperty(nameof(SurfaceAuthoring.HeightBottom));
			_isDroppableProperty = serializedObject.FindProperty(nameof(SurfaceAuthoring.IsDroppable));
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			serializedObject.Update();

			OnPreInspectorGUI();

			PropertyField(_heightTopProperty, "Top Height", true);
			PropertyField(_heightBottomProperty, "Bottom Height", true);
			PropertyField(_isDroppableProperty);

			base.OnInspectorGUI();

			serializedObject.ApplyModifiedProperties();
		}
	}
}
