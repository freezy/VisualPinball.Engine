using UnityEditor;
using VisualPinball.Unity.Editor.Utils;
using VisualPinball.Unity.VPT.Trigger;

namespace VisualPinball.Unity.Editor.Inspectors
{
	[CustomEditor(typeof(TriggerBehavior))]
	public class TriggerInspector : DragPointsItemInspector
	{
		private TriggerBehavior _trigger;
		private bool _foldoutColorsAndFormatting = true;
		private bool _foldoutPosition = true;
		private bool _foldoutPhysics = true;
		private bool _foldoutMisc = true;

		protected override void OnEnable()
		{
			base.OnEnable();
			_trigger = target as TriggerBehavior;
		}

		public override void OnInspectorGUI()
		{
			base.OnPreInspectorGUI();

			if (_foldoutColorsAndFormatting = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutColorsAndFormatting, "Colors & Formatting")) {
				DataFieldUtils.ItemDataField("Visible", ref _trigger.data.IsVisible, FinishEdit);
				DataFieldUtils.ItemDataField("Wire Thickness", ref _trigger.data.WireThickness, FinishEdit);
				DataFieldUtils.ItemDataField("Star Radius", ref _trigger.data.Radius, FinishEdit);
				DataFieldUtils.ItemDataField("Rotation", ref _trigger.data.Rotation, FinishEdit);
				DataFieldUtils.ItemDataField("Animation Speed", ref _trigger.data.AnimSpeed, FinishEdit, ("dirtyMesh", false));
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPosition = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPosition, "Position")) {
				DataFieldUtils.ItemDataField("", ref _trigger.data.Center, FinishEdit);
				SurfaceField("Surface", ref _trigger.data.Surface);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPhysics, "State & Physics")) {
				DataFieldUtils.ItemDataField("Enabled", ref _trigger.data.IsEnabled, FinishEdit, ("dirtyMesh", false));
				DataFieldUtils.ItemDataField("Hit Height", ref _trigger.data.HitHeight, FinishEdit, ("dirtyMesh", false));
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutMisc = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMisc, "Misc")) {
				DataFieldUtils.ItemDataField("Timer Enabled", ref _trigger.data.IsTimerEnabled, FinishEdit, ("dirtyMesh", false));
				DataFieldUtils.ItemDataField("Timer Interval", ref _trigger.data.TimerInterval, FinishEdit, ("dirtyMesh", false));
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();
		}
	}
}
