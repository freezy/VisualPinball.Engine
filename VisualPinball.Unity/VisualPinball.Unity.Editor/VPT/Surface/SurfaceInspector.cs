// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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
	[CustomEditor(typeof(SurfaceComponent)), CanEditMultipleObjects]
	public class SurfaceInspector : MainInspector<SurfaceData, SurfaceComponent>
	{
		private SerializedProperty _heightTopProperty;
		private SerializedProperty _heightBottomProperty;

		protected override void OnEnable()
		{
			base.OnEnable();

			_heightTopProperty = serializedObject.FindProperty(nameof(SurfaceComponent.HeightTop));
			_heightBottomProperty = serializedObject.FindProperty(nameof(SurfaceComponent.HeightBottom));
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			BeginEditing();

			OnPreInspectorGUI();

			PropertyField(_heightTopProperty, "Top Height", true);
			PropertyField(_heightBottomProperty, "Bottom Height", true);

			DragPointSplineInspectorGUI.OnInspectorGUI(MainComponent.DragPointSpline);

			base.OnInspectorGUI();

			EndEditing();
		}

	}
}
