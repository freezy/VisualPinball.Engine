
using UnityEditor;
using VisualPinball.Unity.Editor.Utils;
using VisualPinball.Unity.VPT.Surface;

namespace VisualPinball.Unity.Editor.Inspectors
{
	[CustomEditor(typeof(SurfaceBehavior))]
	public class SurfaceInspector : DragPointsItemInspector
	{
		private SurfaceBehavior _targetSurf;
		private bool _foldoutColorsAndFormatting = true;
		private bool _foldoutPosition = true;
		private bool _foldoutPhysics = true;
		private bool _foldoutMisc = true;

		protected override void OnEnable()
		{
			base.OnEnable();
			_targetSurf = target as SurfaceBehavior;
		}

		public override void OnInspectorGUI()
		{
			base.OnPreInspectorGUI();

			if (_foldoutColorsAndFormatting = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutColorsAndFormatting, "Colors & Formatting")) {
				DataFieldUtils.ItemDataField("Top Visible", ref _targetSurf.data.IsTopBottomVisible, FinishEdit);
				DataFieldUtils.ItemDataField("Side Visible", ref _targetSurf.data.IsSideVisible, FinishEdit);
				DataFieldUtils.ItemDataField("Animate Slingshot", ref _targetSurf.data.SlingshotAnimation, FinishEdit, ("dirtyMesh", false));
				DataFieldUtils.ItemDataField("Flipbook", ref _targetSurf.data.IsFlipbook, FinishEdit, ("dirtyMesh", false));
			}

			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPosition = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPosition, "Position")) {
				DataFieldUtils.ItemDataField("Top Height", ref _targetSurf.data.HeightTop, FinishEdit);
				DataFieldUtils.ItemDataField("Bottom Height", ref _targetSurf.data.HeightBottom, FinishEdit);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPhysics, "State & Physics")) {
				DataFieldUtils.ItemDataField("Has Hit Event", ref _targetSurf.data.HitEvent, FinishEdit, ("dirtyMesh", false));
				EditorGUI.BeginDisabledGroup(!_targetSurf.data.HitEvent);
				DataFieldUtils.ItemDataField("Hit Threshold", ref _targetSurf.data.Threshold, FinishEdit, ("dirtyMesh", false));
				EditorGUI.EndDisabledGroup();

				DataFieldUtils.ItemDataField("Slingshot Force", ref _targetSurf.data.SlingshotForce, FinishEdit, ("dirtyMesh", false));
				DataFieldUtils.ItemDataField("Slingshot Threshold", ref _targetSurf.data.SlingshotThreshold, FinishEdit, ("dirtyMesh", false));

				EditorGUI.BeginDisabledGroup(_targetSurf.data.OverwritePhysics);
				DataFieldUtils.ItemDataField("Physics Material", ref _targetSurf.data.PhysicsMaterial, FinishEdit, ("dirtyMesh", false));
				EditorGUI.EndDisabledGroup();

				DataFieldUtils.ItemDataField("Overwrite Material Settings", ref _targetSurf.data.OverwritePhysics, FinishEdit, ("dirtyMesh", false));

				EditorGUI.BeginDisabledGroup(!_targetSurf.data.OverwritePhysics);
				DataFieldUtils.ItemDataField("Elasticity", ref _targetSurf.data.Elasticity, FinishEdit, ("dirtyMesh", false));
				DataFieldUtils.ItemDataField("Friction", ref _targetSurf.data.Friction, FinishEdit, ("dirtyMesh", false));
				DataFieldUtils.ItemDataField("Scatter Angle", ref _targetSurf.data.Scatter, FinishEdit, ("dirtyMesh", false));
				EditorGUI.EndDisabledGroup();

				DataFieldUtils.ItemDataField("Can Drop", ref _targetSurf.data.IsDroppable, FinishEdit, ("dirtyMesh", false));
				DataFieldUtils.ItemDataField("Collidable", ref _targetSurf.data.IsCollidable, FinishEdit, ("dirtyMesh", false));
				DataFieldUtils.ItemDataField("Is Bottom Collidable", ref _targetSurf.data.IsBottomSolid, FinishEdit, ("dirtyMesh", false));
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutMisc = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMisc, "Misc")) {
				DataFieldUtils.ItemDataField("Timer Enabled", ref _targetSurf.data.IsTimerEnabled, FinishEdit, ("dirtyMesh", false));
				DataFieldUtils.ItemDataField("Timer Interval", ref _targetSurf.data.TimerInterval, FinishEdit, ("dirtyMesh", false));
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();
		}
	}
}
