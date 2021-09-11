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

using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Playfield;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(PlayfieldMeshComponent)), CanEditMultipleObjects]
	public class PlayfieldMeshInspector : ItemMeshInspector<TableData, PlayfieldComponent, PlayfieldMeshComponent>
	{
		private SerializedProperty _autoGenerateProperty;

		protected override void OnEnable()
		{
			base.OnEnable();

			_autoGenerateProperty = serializedObject.FindProperty(nameof(PlayfieldMeshComponent.AutoGenerate));
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			serializedObject.Update();

			OnPreInspectorGUI();

			var mf = MeshComponent.GetComponent<MeshFilter>();
			PropertyField(_autoGenerateProperty, rebuildMesh: true, onChanging: () => {
				if (mf) {
					mf.sharedMesh = _autoGenerateProperty.boolValue
						? new Mesh { name = $"{target.name} (Generated)" } // when switching to legacy mesh, instantiate new mesh
						: null;                                            // when switching to referenced mesh, reset reference.
					serializedObject.ApplyModifiedProperties();
				}
			});

			EditorGUI.BeginDisabledGroup(_autoGenerateProperty.boolValue);
			if (mf) {
				EditorGUI.BeginChangeCheck();
				var newMesh = (Mesh)EditorGUILayout.ObjectField("Mesh", mf.sharedMesh, typeof(Mesh), true);
				if (EditorGUI.EndChangeCheck()) {
					mf.sharedMesh = newMesh;
				}
			}
			EditorGUI.EndDisabledGroup();

			base.OnInspectorGUI();

			serializedObject.ApplyModifiedProperties();
		}
	}
}
