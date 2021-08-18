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
		private bool _foldoutColorsAndFormatting = true;
		private bool _foldoutPosition = true;

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			if (_foldoutColorsAndFormatting = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutColorsAndFormatting, "Colors & Formatting")) {
				GUILayout.BeginHorizontal();
				MeshImporterGui();
				if (GUILayout.Button("Export Mesh")) ExportMesh();
				GUILayout.EndHorizontal();

				TextureFieldLegacy("Texture", ref Data.Image);
				TextureFieldLegacy("Normal Map", ref Data.NormalMap);
				EditorGUI.indentLevel++;
				ItemDataField("Object Space", ref Data.ObjectSpaceNormalMap);
				EditorGUI.indentLevel--;
				MaterialFieldLegacy("Material", ref Data.Material);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPosition = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPosition, "Position & Translation")) {
				EditorGUILayout.LabelField("Base Position");
				EditorGUI.indentLevel++;
				ItemDataField("", ref Data.Position);
				EditorGUI.indentLevel--;

				EditorGUILayout.LabelField("Base Size");
				EditorGUI.indentLevel++;
				ItemDataField("", ref Data.Size);
				EditorGUI.indentLevel--;

				EditorGUILayout.LabelField("Rotation and Transposition");
				EditorGUI.indentLevel++;
				ItemDataField("0: RotX", ref Data.RotAndTra[0]);
				ItemDataField("1: RotY", ref Data.RotAndTra[1]);
				ItemDataField("2: RotZ", ref Data.RotAndTra[2]);
				ItemDataField("3: TransX", ref Data.RotAndTra[3]);
				ItemDataField("4: TransY", ref Data.RotAndTra[4]);
				ItemDataField("5: TransZ", ref Data.RotAndTra[5]);
				ItemDataField("6: ObjRotX", ref Data.RotAndTra[6]);
				ItemDataField("7: ObjRotY", ref Data.RotAndTra[7]);
				ItemDataField("8: ObjRotZ", ref Data.RotAndTra[8]);
				EditorGUI.indentLevel--;
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();
		}

		/// <summary>
		/// Shows a gui to bring a unity mesh into the table data. This immediately "bakes" right in the VPX data structures
		/// </summary>
		private void MeshImporterGui()
		{
			EditorGUI.BeginChangeCheck();
			var mesh = (Mesh)EditorGUILayout.ObjectField("Import Mesh", null, typeof(Mesh), false);
			if (mesh != null && EditorGUI.EndChangeCheck()) {
				FinishEdit("Import Mesh", true);
				Data.Use3DMesh = true;
				Data.Mesh = mesh.ToVpMesh();
			}
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
