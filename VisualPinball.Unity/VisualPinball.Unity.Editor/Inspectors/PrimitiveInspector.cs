using UnityEditor;
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
				ItemDataField("Physics Material", ref _prim.data.PhysicsMaterial, dirtyMesh: false);
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
	}
}
