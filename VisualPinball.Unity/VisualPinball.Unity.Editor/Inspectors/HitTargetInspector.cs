using UnityEditor;
using VisualPinball.Unity.Editor.Utils;
using VisualPinball.Unity.VPT.HitTarget;

namespace VisualPinball.Unity.Editor.Inspectors
{
	[CustomEditor(typeof(HitTargetBehavior))]
	public class HitTargetInspector : ItemInspector
	{
		private HitTargetBehavior _target;
		private bool _foldoutColorsAndFormatting = true;
		private bool _foldoutPosition = true;
		private bool _foldoutPhysics = true;
		private bool _foldoutMisc = true;

		protected override void OnEnable()
		{
			base.OnEnable();
			_target = target as HitTargetBehavior;
		}

		public override void OnInspectorGUI()
		{
			base.OnPreInspectorGUI();

			if (_foldoutColorsAndFormatting = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutColorsAndFormatting, "Colors & Formatting")) {
				DataFieldUtils.ItemDataField("Drop Speed", ref _target.data.DropSpeed, FinishEdit, ("dirtyMesh", false));
				DataFieldUtils.ItemDataField("Raise Delay", ref _target.data.RaiseDelay, FinishEdit, ("dirtyMesh", false));
				DataFieldUtils.ItemDataField("Depth Bias", ref _target.data.DepthBias, FinishEdit, ("dirtyMesh", false));
				DataFieldUtils.ItemDataField("Visible", ref _target.data.IsVisible, FinishEdit);
				DataFieldUtils.ItemDataField("Reflection Enabled", ref _target.data.IsReflectionEnabled, FinishEdit);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPosition = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPosition, "Position & Translation")) {
				EditorGUILayout.LabelField("Position");
				EditorGUI.indentLevel++;
				DataFieldUtils.ItemDataField("", ref _target.data.Position, FinishEdit);
				EditorGUI.indentLevel--;

				EditorGUILayout.LabelField("Scale");
				EditorGUI.indentLevel++;
				DataFieldUtils.ItemDataField("", ref _target.data.Size, FinishEdit);
				EditorGUI.indentLevel--;

				DataFieldUtils.ItemDataField("Orientation", ref _target.data.RotZ, FinishEdit);

			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPhysics, "Physics")) {
				DataFieldUtils.ItemDataField("Has Hit Event", ref _target.data.UseHitEvent, FinishEdit, ("dirtyMesh", false));
				DataFieldUtils.ItemDataField("Hit Threshold", ref _target.data.Threshold, FinishEdit, ("dirtyMesh", false));

				EditorGUI.BeginDisabledGroup(_target.data.OverwritePhysics);
				DataFieldUtils.ItemDataField("Physics Material", ref _target.data.PhysicsMaterial, FinishEdit, ("dirtyMesh", false));
				EditorGUI.EndDisabledGroup();

				DataFieldUtils.ItemDataField("Overwrite Material Settings", ref _target.data.OverwritePhysics, FinishEdit, ("dirtyMesh", false));

				EditorGUI.BeginDisabledGroup(!_target.data.OverwritePhysics);
				DataFieldUtils.ItemDataField("Elasticity", ref _target.data.Elasticity, FinishEdit, ("dirtyMesh", false));
				DataFieldUtils.ItemDataField("Elasticity Falloff", ref _target.data.ElasticityFalloff, FinishEdit, ("dirtyMesh", false));
				DataFieldUtils.ItemDataField("Friction", ref _target.data.Friction, FinishEdit, ("dirtyMesh", false));
				DataFieldUtils.ItemDataField("Scatter Angle", ref _target.data.Scatter, FinishEdit, ("dirtyMesh", false));
				EditorGUI.EndDisabledGroup();

				DataFieldUtils.ItemDataField("Legacy Mode", ref _target.data.IsLegacy, FinishEdit, ("dirtyMesh", false));
				DataFieldUtils.ItemDataField("Collidable", ref _target.data.IsCollidable, FinishEdit, ("dirtyMesh", false));
				DataFieldUtils.ItemDataField("Is Dropped", ref _target.data.IsDropped, FinishEdit, ("dirtyMesh", false));
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutMisc = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMisc, "Misc")) {
				DataFieldUtils.ItemDataField("Timer Enabled", ref _target.data.IsTimerEnabled, FinishEdit, ("dirtyMesh", false));
				DataFieldUtils.ItemDataField("Timer Interval", ref _target.data.TimerInterval, FinishEdit, ("dirtyMesh", false));
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();
		}
	}
}
