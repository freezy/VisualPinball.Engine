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
using VisualPinball.Engine.VPT.Plunger;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(PlungerAuthoring))]
	public class PlungerInspector : ItemMainInspector<Plunger, PlungerData, PlungerAuthoring>
	{
		private SerializedProperty _positionProperty;
		private SerializedProperty _widthProperty;
		private SerializedProperty _heightProperty;
		private SerializedProperty _zAdjustProperty;
		private SerializedProperty _surfaceProperty;

		protected override void OnEnable()
		{
			base.OnEnable();

			_positionProperty = serializedObject.FindProperty(nameof(PlungerAuthoring.Position));
			_widthProperty = serializedObject.FindProperty(nameof(PlungerAuthoring.Width));
			_heightProperty = serializedObject.FindProperty(nameof(PlungerAuthoring.Height));
			_zAdjustProperty = serializedObject.FindProperty(nameof(PlungerAuthoring.ZAdjust));
			_surfaceProperty = serializedObject.FindProperty(nameof(PlungerAuthoring._surface));
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			serializedObject.Update();

			OnPreInspectorGUI();

			PropertyField(_positionProperty, rebuildMesh: true);
			PropertyField(_widthProperty, rebuildMesh: true);
			PropertyField(_heightProperty, rebuildMesh: true);
			PropertyField(_zAdjustProperty, rebuildMesh: true);
			PropertyField(_surfaceProperty, rebuildMesh: true);

			base.OnInspectorGUI();

			serializedObject.ApplyModifiedProperties();
		}
	}
}
