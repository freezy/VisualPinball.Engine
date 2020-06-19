using UnityEditor;
using VisualPinball.Unity.Editor.Utils;
using VisualPinball.Unity.VPT.Primitive;

namespace VisualPinball.Unity.Editor.Inspectors
{
	[CustomEditor(typeof(PrimitiveBehavior))]
	public class PrimitiveInspector : ItemInspector
	{
		private PrimitiveBehavior _prim;
		private bool _foldoutPosition = true;
		private bool _foldoutPhysics = true;

		protected override void OnEnable()
		{
			base.OnEnable();
			_prim = target as PrimitiveBehavior;
		}

		public override void OnInspectorGUI()
		{
			base.OnPreInspectorGUI();

			if (_foldoutPosition = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPosition, "Position & Translation")) {
				EditorGUILayout.LabelField("Base Position");
				EditorGUI.indentLevel++;
				DataFieldUtils.ItemDataField("", ref _prim.data.Position, FinishEdit);
				EditorGUI.indentLevel--;

				EditorGUILayout.LabelField("Base Size");
				EditorGUI.indentLevel++;
				DataFieldUtils.ItemDataField("", ref _prim.data.Size, FinishEdit);
				EditorGUI.indentLevel--;

				EditorGUILayout.LabelField("Rotation and Transposition");
				EditorGUI.indentLevel++;
				DataFieldUtils.ItemDataField("0: RotX", ref _prim.data.RotAndTra[0], FinishEdit);
				DataFieldUtils.ItemDataField("1: RotY", ref _prim.data.RotAndTra[1], FinishEdit);
				DataFieldUtils.ItemDataField("2: RotZ", ref _prim.data.RotAndTra[2], FinishEdit);
				DataFieldUtils.ItemDataField("3: TransX", ref _prim.data.RotAndTra[3], FinishEdit);
				DataFieldUtils.ItemDataField("4: TransY", ref _prim.data.RotAndTra[4], FinishEdit);
				DataFieldUtils.ItemDataField("5: TransZ", ref _prim.data.RotAndTra[5], FinishEdit);
				DataFieldUtils.ItemDataField("6: ObjRotX", ref _prim.data.RotAndTra[6], FinishEdit);
				DataFieldUtils.ItemDataField("7: ObjRotY", ref _prim.data.RotAndTra[7], FinishEdit);
				DataFieldUtils.ItemDataField("8: ObjRotZ", ref _prim.data.RotAndTra[8], FinishEdit);
				EditorGUI.indentLevel--;
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPhysics, "Physics")) {
				EditorGUI.BeginDisabledGroup(_prim.data.IsToy || !_prim.data.IsCollidable);

				DataFieldUtils.ItemDataField("Has Hit Event", ref _prim.data.HitEvent, FinishEdit, ("dirtyMesh", false));
				EditorGUI.BeginDisabledGroup(!_prim.data.HitEvent);
				DataFieldUtils.ItemDataField("Has Hit Event", ref _prim.data.Threshold, FinishEdit, ("dirtyMesh", false));
				EditorGUI.EndDisabledGroup();

				EditorGUI.BeginDisabledGroup(_prim.data.OverwritePhysics);
				DataFieldUtils.ItemDataField("Physics Material", ref _prim.data.PhysicsMaterial, FinishEdit, ("dirtyMesh", false));
				EditorGUI.EndDisabledGroup();
				DataFieldUtils.ItemDataField("Overwrite Material Settings", ref _prim.data.OverwritePhysics, FinishEdit, ("dirtyMesh", false));
				EditorGUI.BeginDisabledGroup(!_prim.data.OverwritePhysics);
				DataFieldUtils.ItemDataField("Elasticity", ref _prim.data.Elasticity, FinishEdit, ("dirtyMesh", false));
				DataFieldUtils.ItemDataField("Elasticity Falloff", ref _prim.data.ElasticityFalloff, FinishEdit, ("dirtyMesh", false));
				DataFieldUtils.ItemDataField("Friction", ref _prim.data.Friction, FinishEdit, ("dirtyMesh", false));
				DataFieldUtils.ItemDataField("Scatter Angle", ref _prim.data.Scatter, FinishEdit, ("dirtyMesh", false));
				EditorGUI.EndDisabledGroup();

				EditorGUI.EndDisabledGroup();

				EditorGUI.BeginDisabledGroup(_prim.data.IsToy);
				DataFieldUtils.ItemDataField("Collidable", ref _prim.data.IsCollidable, FinishEdit, ("dirtyMesh", false));
				EditorGUI.EndDisabledGroup();

				DataFieldUtils.ItemDataField("Toy", ref _prim.data.IsToy, FinishEdit, ("dirtyMesh", false));

				EditorGUI.BeginDisabledGroup(_prim.data.IsToy);
				DataFieldUtils.ItemDataField("Reduce Polygons By", ref _prim.data.CollisionReductionFactor, FinishEdit, ("dirtyMesh", false));
				EditorGUI.EndDisabledGroup();
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();
		}
	}
}
