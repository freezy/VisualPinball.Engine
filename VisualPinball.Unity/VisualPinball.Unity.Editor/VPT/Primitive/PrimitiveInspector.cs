// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(PrimitiveAuthoring))]
	public class PrimitiveInspector : ItemInspector
	{
		private PrimitiveAuthoring _prim;
		private bool _foldoutColorsAndFormatting = true;
		private bool _foldoutPosition = true;
		private bool _foldoutPhysics = true;

		protected override void OnEnable()
		{
			base.OnEnable();
			_prim = target as PrimitiveAuthoring;
		}

		public override void OnInspectorGUI()
		{
			OnPreInspectorGUI();

			if (_foldoutColorsAndFormatting = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutColorsAndFormatting, "Colors & Formatting")) {
				GUILayout.BeginHorizontal();
				MeshImporterGui();
				if (GUILayout.Button("Export Mesh")) ExportMesh();
				GUILayout.EndHorizontal();

				TextureField("Image", ref _prim.Data.Image);
				TextureField("Normal Map", ref _prim.Data.NormalMap);
				EditorGUI.indentLevel++;
				ItemDataField("Object Space", ref _prim.Data.ObjectSpaceNormalMap);
				EditorGUI.indentLevel--;
				MaterialField("Material", ref _prim.Data.Material);

				ItemDataField("Visible", ref _prim.Data.IsVisible);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPosition = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPosition, "Position & Translation")) {
				EditorGUILayout.LabelField("Base Position");
				EditorGUI.indentLevel++;
				ItemDataField("", ref _prim.Data.Position);
				EditorGUI.indentLevel--;

				EditorGUILayout.LabelField("Base Size");
				EditorGUI.indentLevel++;
				ItemDataField("", ref _prim.Data.Size);
				EditorGUI.indentLevel--;

				EditorGUILayout.LabelField("Rotation and Transposition");
				EditorGUI.indentLevel++;
				ItemDataField("0: RotX", ref _prim.Data.RotAndTra[0]);
				ItemDataField("1: RotY", ref _prim.Data.RotAndTra[1]);
				ItemDataField("2: RotZ", ref _prim.Data.RotAndTra[2]);
				ItemDataField("3: TransX", ref _prim.Data.RotAndTra[3]);
				ItemDataField("4: TransY", ref _prim.Data.RotAndTra[4]);
				ItemDataField("5: TransZ", ref _prim.Data.RotAndTra[5]);
				ItemDataField("6: ObjRotX", ref _prim.Data.RotAndTra[6]);
				ItemDataField("7: ObjRotY", ref _prim.Data.RotAndTra[7]);
				ItemDataField("8: ObjRotZ", ref _prim.Data.RotAndTra[8]);
				EditorGUI.indentLevel--;
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPhysics, "Physics")) {
				EditorGUI.BeginDisabledGroup(_prim.Data.IsToy || !_prim.Data.IsCollidable);

				ItemDataField("Has Hit Event", ref _prim.Data.HitEvent, dirtyMesh: false);
				EditorGUI.BeginDisabledGroup(!_prim.Data.HitEvent);
				ItemDataField("Has Hit Event", ref _prim.Data.Threshold, dirtyMesh: false);
				EditorGUI.EndDisabledGroup();

				EditorGUI.BeginDisabledGroup(_prim.Data.OverwritePhysics);
				MaterialField("Physics Material", ref _prim.Data.PhysicsMaterial, dirtyMesh: false);
				EditorGUI.EndDisabledGroup();
				ItemDataField("Overwrite Material Settings", ref _prim.Data.OverwritePhysics, dirtyMesh: false);
				EditorGUI.BeginDisabledGroup(!_prim.Data.OverwritePhysics);
				ItemDataField("Elasticity", ref _prim.Data.Elasticity, dirtyMesh: false);
				ItemDataField("Elasticity Falloff", ref _prim.Data.ElasticityFalloff, dirtyMesh: false);
				ItemDataField("Friction", ref _prim.Data.Friction, dirtyMesh: false);
				ItemDataField("Scatter Angle", ref _prim.Data.Scatter, dirtyMesh: false);
				EditorGUI.EndDisabledGroup();

				EditorGUI.EndDisabledGroup();

				EditorGUI.BeginDisabledGroup(_prim.Data.IsToy);
				ItemDataField("Collidable", ref _prim.Data.IsCollidable, dirtyMesh: false);
				EditorGUI.EndDisabledGroup();

				ItemDataField("Toy", ref _prim.Data.IsToy, dirtyMesh: false);

				EditorGUI.BeginDisabledGroup(_prim.Data.IsToy);
				ItemDataSlider("Reduce Polygons By", ref _prim.Data.CollisionReductionFactor, 0f, 1f, dirtyMesh: false);
				EditorGUI.EndDisabledGroup();
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
				_prim.Data.Use3DMesh = true;
				_prim.Data.Mesh = mesh.ToVpMesh();
			}
		}

		/// <summary>
		/// Pop a dialog to save the primitive's mesh as a unity asset
		/// </summary>
		private void ExportMesh()
		{
			var table = _prim.GetComponentInParent<TableAuthoring>();
			if (table != null) {
				var rog = _prim.Item.GetRenderObjects(table.Table, Origin.Original, false);
				if (rog != null && rog.RenderObjects.Length > 0) {
					var unityMesh = rog.RenderObjects[0].Mesh?.ToUnityMesh(_prim.name);
					if (unityMesh != null) {
						string savePath = EditorUtility.SaveFilePanelInProject("Export Mesh", _prim.name, "asset", "Export Mesh");
						AssetDatabase.CreateAsset(unityMesh, savePath);
					}
				}
			}
		}
	}
}
