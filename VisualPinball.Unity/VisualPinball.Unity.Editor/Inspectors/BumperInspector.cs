using UnityEditor;
using VisualPinball.Unity.Editor.Utils;
using VisualPinball.Unity.VPT.Bumper;

namespace VisualPinball.Unity.Editor.Inspectors
{
	[CustomEditor(typeof(BumperBehavior))]
	public class BumperInspector : ItemInspector
	{
		private BumperBehavior _bumper;
		private bool _foldoutColorsAndFormatting = true;
		private bool _foldoutPosition = true;
		private bool _foldoutPhysics = true;
		private bool _foldoutMisc = true;

		protected override void OnEnable()
		{
			base.OnEnable();
			_bumper = target as BumperBehavior;
		}

		public override void OnInspectorGUI()
		{
			base.OnPreInspectorGUI();

			if (_foldoutColorsAndFormatting = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutColorsAndFormatting, "Colors & Formatting")) {
				MaterialField("Cap Material", ref _bumper.data.CapMaterial);
				MaterialField("Base Material", ref _bumper.data.BaseMaterial);
				MaterialField("Ring Material", ref _bumper.data.RingMaterial);
				MaterialField("Skirt Material", ref _bumper.data.SocketMaterial);
				DataFieldUtils.ItemDataField("Radius", ref _bumper.data.Radius, FinishEdit);
				DataFieldUtils.ItemDataField("Height Scale", ref _bumper.data.HeightScale, FinishEdit);
				DataFieldUtils.ItemDataField("Orientation", ref _bumper.data.Orientation, FinishEdit);
				DataFieldUtils.ItemDataField("Ring Speed", ref _bumper.data.RingSpeed, FinishEdit, false);
				DataFieldUtils.ItemDataField("Ring Drop Offset", ref _bumper.data.RingDropOffset, FinishEdit, false);
				DataFieldUtils.ItemDataField("Reflection Enabled", ref _bumper.data.IsReflectionEnabled, FinishEdit);
				DataFieldUtils.ItemDataField("Cap Visible", ref _bumper.data.IsCapVisible, FinishEdit);
				DataFieldUtils.ItemDataField("Base Visible", ref _bumper.data.IsBaseVisible, FinishEdit);
				DataFieldUtils.ItemDataField("Ring Visible", ref _bumper.data.IsRingVisible, FinishEdit);
				DataFieldUtils.ItemDataField("Skirt Visible", ref _bumper.data.IsSocketVisible, FinishEdit);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPosition = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPosition, "Position")) {
				DataFieldUtils.ItemDataField("", ref _bumper.data.Center, FinishEdit);
				SurfaceField("Surface", ref _bumper.data.Surface);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPhysics, "Physics")) {
				DataFieldUtils.ItemDataField("Has Hit Event", ref _bumper.data.HitEvent, FinishEdit, false);
				DataFieldUtils.ItemDataField("Force", ref _bumper.data.Force, FinishEdit, false);
				DataFieldUtils.ItemDataField("Hit Threshold", ref _bumper.data.Threshold, FinishEdit, false);
				DataFieldUtils.ItemDataField("Scatter Angle", ref _bumper.data.Scatter, FinishEdit, false);
				DataFieldUtils.ItemDataField("Collidable", ref _bumper.data.IsCollidable, FinishEdit, false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutMisc = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMisc, "Misc")) {
				DataFieldUtils.ItemDataField("Timer Enabled", ref _bumper.data.IsTimerEnabled, FinishEdit, false);
				DataFieldUtils.ItemDataField("Timer Interval", ref _bumper.data.TimerInterval, FinishEdit, false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();
		}
	}
}
