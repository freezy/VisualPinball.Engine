using UnityEditor;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(BumperAuthoring))]
	public class BumperInspector : ItemInspector
	{
		private BumperAuthoring _bumper;
		private bool _foldoutColorsAndFormatting = true;
		private bool _foldoutPosition = true;
		private bool _foldoutPhysics = true;
		private bool _foldoutMisc = true;

		protected override void OnEnable()
		{
			base.OnEnable();
			_bumper = target as BumperAuthoring;
		}

		public override void OnInspectorGUI()
		{
			OnPreInspectorGUI();

			if (_foldoutColorsAndFormatting = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutColorsAndFormatting, "Colors & Formatting")) {
				MaterialField("Cap Material", ref _bumper.data.CapMaterial);
				MaterialField("Base Material", ref _bumper.data.BaseMaterial);
				MaterialField("Ring Material", ref _bumper.data.RingMaterial);
				MaterialField("Skirt Material", ref _bumper.data.SocketMaterial);
				ItemDataField("Radius", ref _bumper.data.Radius);
				ItemDataField("Height Scale", ref _bumper.data.HeightScale);
				ItemDataField("Orientation", ref _bumper.data.Orientation);
				ItemDataField("Ring Speed", ref _bumper.data.RingSpeed, dirtyMesh: false);
				ItemDataField("Ring Drop Offset", ref _bumper.data.RingDropOffset, dirtyMesh: false);
				ItemDataField("Cap Visible", ref _bumper.data.IsCapVisible);
				ItemDataField("Base Visible", ref _bumper.data.IsBaseVisible);
				ItemDataField("Ring Visible", ref _bumper.data.IsRingVisible);
				ItemDataField("Skirt Visible", ref _bumper.data.IsSocketVisible);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPosition = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPosition, "Position")) {
				ItemDataField("", ref _bumper.data.Center);
				SurfaceField("Surface", ref _bumper.data.Surface);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPhysics, "Physics")) {
				ItemDataField("Has Hit Event", ref _bumper.data.HitEvent, dirtyMesh: false);
				ItemDataField("Force", ref _bumper.data.Force, dirtyMesh: false);
				ItemDataField("Hit Threshold", ref _bumper.data.Threshold, dirtyMesh: false);
				ItemDataField("Scatter Angle", ref _bumper.data.Scatter, dirtyMesh: false);
				ItemDataField("Collidable", ref _bumper.data.IsCollidable, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutMisc = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMisc, "Misc")) {
				ItemDataField("Timer Enabled", ref _bumper.data.IsTimerEnabled, dirtyMesh: false);
				ItemDataField("Timer Interval", ref _bumper.data.TimerInterval, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();
		}
	}
}
