using UnityEditor;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(SurfaceAuthoring))]
	public class SurfaceInspector : DragPointsItemInspector
	{
		private SurfaceAuthoring _targetSurf;
		private bool _foldoutColorsAndFormatting = true;
		private bool _foldoutPosition = true;
		private bool _foldoutPhysics = true;
		private bool _foldoutMisc = true;

		protected override void OnEnable()
		{
			base.OnEnable();
			_targetSurf = target as SurfaceAuthoring;
		}

		public override void OnInspectorGUI()
		{
			OnPreInspectorGUI();

			if (_foldoutColorsAndFormatting = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutColorsAndFormatting, "Colors & Formatting")) {
				ItemDataField("Top Visible", ref _targetSurf.data.IsTopBottomVisible);
				TextureField("Top Image", ref _targetSurf.data.Image);
				MaterialField("Top Material", ref _targetSurf.data.TopMaterial);
				ItemDataField("Side Visible", ref _targetSurf.data.IsSideVisible);
				TextureField("Side Image", ref _targetSurf.data.SideImage);
				MaterialField("Side Material", ref _targetSurf.data.SideMaterial);
				MaterialField("Slingshot Material", ref _targetSurf.data.SlingShotMaterial);
				ItemDataField("Animate Slingshot", ref _targetSurf.data.SlingshotAnimation, dirtyMesh: false);
				ItemDataField("Flipbook", ref _targetSurf.data.IsFlipbook, dirtyMesh: false);
			}

			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPosition = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPosition, "Position")) {
				ItemDataField("Top Height", ref _targetSurf.data.HeightTop);
				ItemDataField("Bottom Height", ref _targetSurf.data.HeightBottom);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPhysics, "State & Physics")) {
				ItemDataField("Has Hit Event", ref _targetSurf.data.HitEvent, dirtyMesh: false);
				EditorGUI.BeginDisabledGroup(!_targetSurf.data.HitEvent);
				ItemDataField("Hit Threshold", ref _targetSurf.data.Threshold, dirtyMesh: false);
				EditorGUI.EndDisabledGroup();

				ItemDataField("Slingshot Force", ref _targetSurf.data.SlingshotForce, dirtyMesh: false);
				ItemDataField("Slingshot Threshold", ref _targetSurf.data.SlingshotThreshold, dirtyMesh: false);

				EditorGUI.BeginDisabledGroup(_targetSurf.data.OverwritePhysics);
				MaterialField("Physics Material", ref _targetSurf.data.PhysicsMaterial, dirtyMesh: false);
				EditorGUI.EndDisabledGroup();

				ItemDataField("Overwrite Material Settings", ref _targetSurf.data.OverwritePhysics, dirtyMesh: false);

				EditorGUI.BeginDisabledGroup(!_targetSurf.data.OverwritePhysics);
				ItemDataField("Elasticity", ref _targetSurf.data.Elasticity, dirtyMesh: false);
				ItemDataField("Friction", ref _targetSurf.data.Friction, dirtyMesh: false);
				ItemDataField("Scatter Angle", ref _targetSurf.data.Scatter, dirtyMesh: false);
				EditorGUI.EndDisabledGroup();

				ItemDataField("Can Drop", ref _targetSurf.data.IsDroppable, dirtyMesh: false);
				ItemDataField("Collidable", ref _targetSurf.data.IsCollidable, dirtyMesh: false);
				ItemDataField("Is Bottom Collidable", ref _targetSurf.data.IsBottomSolid, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutMisc = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMisc, "Misc")) {
				ItemDataField("Timer Enabled", ref _targetSurf.data.IsTimerEnabled, dirtyMesh: false);
				ItemDataField("Timer Interval", ref _targetSurf.data.TimerInterval, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();
		}
	}
}
