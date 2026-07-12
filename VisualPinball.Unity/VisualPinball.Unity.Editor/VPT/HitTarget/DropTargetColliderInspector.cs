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

using UnityEditor;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(DropTargetColliderComponent)), CanEditMultipleObjects]
	public class DropTargetColliderInspector : TargetColliderInspector<DropTargetColliderComponent>
	{
		private SerializedProperty _collisionColliderMeshProperty;
		private SerializedProperty _physicsModeProperty;
		private SerializedProperty _mechanicalProfileProperty;
		private SerializedProperty _overrideMechanicalProfileProperty;
		private SerializedProperty _mechanicalOverridesProperty;
		private SerializedProperty _rothConfigProperty;

		protected override void OnEnable()
		{
			base.OnEnable();
			_collisionColliderMeshProperty = serializedObject.FindProperty(nameof(DropTargetColliderComponent.CollisionColliderMesh));
			_physicsModeProperty = serializedObject.FindProperty(nameof(DropTargetColliderComponent.PhysicsMode));
			_mechanicalProfileProperty = serializedObject.FindProperty(nameof(DropTargetColliderComponent.MechanicalProfile));
			_overrideMechanicalProfileProperty = serializedObject.FindProperty(nameof(DropTargetColliderComponent.OverrideMechanicalProfile));
			_mechanicalOverridesProperty = serializedObject.FindProperty(nameof(DropTargetColliderComponent.MechanicalOverrides));
			_rothConfigProperty = serializedObject.FindProperty(nameof(DropTargetColliderComponent.RothConfig));
		}

		protected override void OnTargetInspectorGUI()
		{
			PropertyField(_physicsModeProperty, updateColliders: true);
			PropertyField(_collisionColliderMeshProperty, "Collision Collider", updateColliders: true);

			var mode = (DropTargetPhysicsMode)_physicsModeProperty.intValue;
			if (mode == DropTargetPhysicsMode.RothCompatible) {
				EditorGUILayout.PropertyField(_rothConfigProperty, true);
			} else if (mode == DropTargetPhysicsMode.Mechanical) {
				PropertyField(_mechanicalProfileProperty);
				PropertyField(_overrideMechanicalProfileProperty);
				if (_mechanicalProfileProperty.objectReferenceValue == null || _overrideMechanicalProfileProperty.boolValue) {
					EditorGUILayout.PropertyField(_mechanicalOverridesProperty, true);
				}
			}
		}
	}
}
