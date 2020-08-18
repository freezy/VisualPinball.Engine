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

				TextureField("Image", ref _prim.data.Image);
				TextureField("Normal Map", ref _prim.data.NormalMap);
				EditorGUI.indentLevel++;
				ItemDataField("Object Space", ref _prim.data.ObjectSpaceNormalMap);
				EditorGUI.indentLevel--;
				MaterialField("Material", ref _prim.data.Material);

				ItemDataField("Visible", ref _prim.data.IsVisible);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPosition = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPosition, "Position & Translation")) {
				EditorGUILayout.LabelField("Base Position");
				EditorGUI.indentLevel++;
				ItemDataField("", ref _prim.data.Position);
				EditorGUI.indentLevel--;

				EditorGUILayout.LabelField("Base Size");
				EditorGUI.indentLevel++;
				ItemDataField("", ref _prim.data.Size);
				EditorGUI.indentLevel--;

				EditorGUILayout.LabelField("Rotation and Transposition");
				EditorGUI.indentLevel++;
				ItemDataField("0: RotX", ref _prim.data.RotAndTra[0]);
				ItemDataField("1: RotY", ref _prim.data.RotAndTra[1]);
				ItemDataField("2: RotZ", ref _prim.data.RotAndTra[2]);
				ItemDataField("3: TransX", ref _prim.data.RotAndTra[3]);
				ItemDataField("4: TransY", ref _prim.data.RotAndTra[4]);
				ItemDataField("5: TransZ", ref _prim.data.RotAndTra[5]);
				ItemDataField("6: ObjRotX", ref _prim.data.RotAndTra[6]);
				ItemDataField("7: ObjRotY", ref _prim.data.RotAndTra[7]);
				ItemDataField("8: ObjRotZ", ref _prim.data.RotAndTra[8]);
				EditorGUI.indentLevel--;
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPhysics, "Physics")) {
				EditorGUI.BeginDisabledGroup(_prim.data.IsToy || !_prim.data.IsCollidable);

				ItemDataField("Has Hit Event", ref _prim.data.HitEvent, dirtyMesh: false);
				EditorGUI.BeginDisabledGroup(!_prim.data.HitEvent);
				ItemDataField("Has Hit Event", ref _prim.data.Threshold, dirtyMesh: false);
				EditorGUI.EndDisabledGroup();

				EditorGUI.BeginDisabledGroup(_prim.data.OverwritePhysics);
				MaterialField("Physics Material", ref _prim.data.PhysicsMaterial, dirtyMesh: false);
				EditorGUI.EndDisabledGroup();
				ItemDataField("Overwrite Material Settings", ref _prim.data.OverwritePhysics, dirtyMesh: false);
				EditorGUI.BeginDisabledGroup(!_prim.data.OverwritePhysics);
				ItemDataField("Elasticity", ref _prim.data.Elasticity, dirtyMesh: false);
				ItemDataField("Elasticity Falloff", ref _prim.data.ElasticityFalloff, dirtyMesh: false);
				ItemDataField("Friction", ref _prim.data.Friction, dirtyMesh: false);
				ItemDataField("Scatter Angle", ref _prim.data.Scatter, dirtyMesh: false);
				EditorGUI.EndDisabledGroup();

				EditorGUI.EndDisabledGroup();

				EditorGUI.BeginDisabledGroup(_prim.data.IsToy);
				ItemDataField("Collidable", ref _prim.data.IsCollidable, dirtyMesh: false);
				EditorGUI.EndDisabledGroup();

				ItemDataField("Toy", ref _prim.data.IsToy, dirtyMesh: false);

				EditorGUI.BeginDisabledGroup(_prim.data.IsToy);
				ItemDataField("Reduce Polygons By", ref _prim.data.CollisionReductionFactor, dirtyMesh: false);
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
				_prim.data.Use3DMesh = true;
				_prim.data.Mesh = mesh.ToVpMesh();
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
