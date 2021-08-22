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
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT.Primitive;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(PrimitiveMeshAuthoring)), CanEditMultipleObjects]
	public class PrimitiveMeshInspector : ItemMeshInspector<Primitive, PrimitiveData, PrimitiveAuthoring, PrimitiveMeshAuthoring>
	{
		private SerializedProperty _sidesProperty;
		private SerializedProperty _useLegacyMeshProperty;


		protected override void OnEnable()
		{
			base.OnEnable();

			_sidesProperty = serializedObject.FindProperty(nameof(PrimitiveMeshAuthoring.Sides));
			_useLegacyMeshProperty = serializedObject.FindProperty(nameof(PrimitiveMeshAuthoring.UseLegacyMesh));
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			serializedObject.Update();

			OnPreInspectorGUI();

			EditorGUI.BeginDisabledGroup(_useLegacyMeshProperty.boolValue);
			var mf = MeshAuthoring.GetComponent<MeshFilter>();
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

		/// <summary>
		/// Pop a dialog to save the primitive's mesh as a unity asset
		/// </summary>
		private void ExportMesh()
		{
			var table = MeshAuthoring.GetComponentInParent<TableAuthoring>();
			if (table != null) {
				var rog = MeshAuthoring.MainAuthoring.Item.GetRenderObjects(table.Table, Origin.Original, false);
				if (rog != null && rog.RenderObjects.Length > 0) {
					var unityMesh = rog.RenderObjects[0].Mesh?.ToUnityMesh(MeshAuthoring.IMainAuthoring.Name);
					if (unityMesh != null) {
						var savePath = EditorUtility.SaveFilePanelInProject("Export Mesh", MeshAuthoring.IMainAuthoring.Name, "asset", "Export Mesh");
						if (!string.IsNullOrEmpty(savePath)) {
							AssetDatabase.CreateAsset(unityMesh, savePath);
						}
					}
				}
			}
		}
	}
}
