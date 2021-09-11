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
using UnityEngine;
using VisualPinball.Engine.VPT.Primitive;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(PrimitiveMeshComponent)), CanEditMultipleObjects]
	public class PrimitiveMeshInspector : ItemMeshInspector<PrimitiveData, PrimitiveComponent, PrimitiveMeshComponent>
	{
		private SerializedProperty _sidesProperty;
		private SerializedProperty _useLegacyMeshProperty;


		protected override void OnEnable()
		{
			base.OnEnable();

			_sidesProperty = serializedObject.FindProperty(nameof(PrimitiveMeshComponent.Sides));
			_useLegacyMeshProperty = serializedObject.FindProperty(nameof(PrimitiveMeshComponent.UseLegacyMesh));
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			serializedObject.Update();

			OnPreInspectorGUI();

			EditorGUI.BeginDisabledGroup(_useLegacyMeshProperty.boolValue);
			var mf = MeshComponent.GetComponent<MeshFilter>();
			if (mf) {
				EditorGUI.BeginChangeCheck();
				var newMesh = (Mesh)EditorGUILayout.ObjectField("Mesh", mf.sharedMesh, typeof(Mesh), true);
				if (EditorGUI.EndChangeCheck()) {
					mf.sharedMesh = newMesh;
				}
			}
			EditorGUI.EndDisabledGroup();

			PropertyField(_useLegacyMeshProperty, rebuildMesh: true, onChanging: () => {
				if (mf) {
					mf.sharedMesh = _useLegacyMeshProperty.boolValue
						? new Mesh { name = $"{target.name} (Generated)" } // when switching to legacy mesh, instantiate new mesh
						: null; // when switching to referenced mesh, reset reference.
					serializedObject.ApplyModifiedProperties();
				}
			});
			EditorGUI.BeginDisabledGroup(!_useLegacyMeshProperty.boolValue);
			PropertyField(_sidesProperty, rebuildMesh: true);
			EditorGUI.EndDisabledGroup();

			base.OnInspectorGUI();

			serializedObject.ApplyModifiedProperties();
		}
	}
}
