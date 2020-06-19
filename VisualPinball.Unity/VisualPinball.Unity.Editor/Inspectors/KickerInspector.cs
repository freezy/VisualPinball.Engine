using UnityEditor;
using VisualPinball.Unity.Editor.Utils;
using VisualPinball.Unity.VPT.Kicker;

namespace VisualPinball.Unity.Editor.Inspectors
{
	[CustomEditor(typeof(KickerBehavior))]
	public class KickerInspector : ItemInspector
	{
		private KickerBehavior _kicker;
		private bool _foldoutColorsAndFormatting = true;
		private bool _foldoutPosition = true;
		private bool _foldoutPhysics = true;
		private bool _foldoutMisc = true;

		protected override void OnEnable()
		{
			base.OnEnable();
			_kicker = target as KickerBehavior;
		}

		public override void OnInspectorGUI()
		{
			base.OnPreInspectorGUI();

			if (_foldoutColorsAndFormatting = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutColorsAndFormatting, "Colors & Formatting")) {
				DataFieldUtils.ItemDataField("Radius", ref _kicker.data.Radius, FinishEdit);
				DataFieldUtils.ItemDataField("Orientation", ref _kicker.data.Orientation, FinishEdit);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPosition = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPosition, "Position")) {
				DataFieldUtils.ItemDataField("", ref _kicker.data.Center, FinishEdit);
				SurfaceField("Surface", ref _kicker.data.Surface);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPhysics, "State & Physics")) {
				DataFieldUtils.ItemDataField("Enabled", ref _kicker.data.IsEnabled, FinishEdit, ("dirtyMesh", false));
				DataFieldUtils.ItemDataField("Fall Through", ref _kicker.data.FallThrough, FinishEdit, ("dirtyMesh", false));
				DataFieldUtils.ItemDataField("Legacy", ref _kicker.data.LegacyMode, FinishEdit, ("dirtyMesh", false));
				DataFieldUtils.ItemDataField("Scatter Angle", ref _kicker.data.Scatter, FinishEdit, ("dirtyMesh", false));
				DataFieldUtils.ItemDataField("Hit Accuracy", ref _kicker.data.HitAccuracy, FinishEdit, ("dirtyMesh", false));
				DataFieldUtils.ItemDataField("Hit Height", ref _kicker.data.HitHeight, FinishEdit, ("dirtyMesh", false));
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutMisc = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMisc, "Misc")) {
				DataFieldUtils.ItemDataField("Timer Enabled", ref _kicker.data.IsTimerEnabled, FinishEdit, ("dirtyMesh", false));
				DataFieldUtils.ItemDataField("Timer Interval", ref _kicker.data.TimerInterval, FinishEdit, ("dirtyMesh", false));
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();
		}
	}
}
