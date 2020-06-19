using UnityEditor;
using VisualPinball.Unity.Editor.Utils;
using VisualPinball.Unity.VPT.Spinner;

namespace VisualPinball.Unity.Editor.Inspectors
{
	[CustomEditor(typeof(SpinnerBehavior))]
	public class SpinnerInspector : ItemInspector
	{
		private SpinnerBehavior _spinner;
		private bool _foldoutColorsAndFormatting = true;
		private bool _foldoutPosition = true;
		private bool _foldoutPhysics = true;
		private bool _foldoutMisc = true;

		protected override void OnEnable()
		{
			base.OnEnable();
			_spinner = target as SpinnerBehavior;
		}

		public override void OnInspectorGUI()
		{
			base.OnPreInspectorGUI();

			if (_foldoutColorsAndFormatting = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutColorsAndFormatting, "Colors & Formatting")) {
				DataFieldUtils.ItemDataField("Visible", ref _spinner.data.IsVisible, FinishEdit);
				DataFieldUtils.ItemDataField("Show Bracket", ref _spinner.data.ShowBracket, FinishEdit);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPosition = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPosition, "Position")) {
				DataFieldUtils.ItemDataField("", ref _spinner.data.Center, FinishEdit);
				DataFieldUtils.ItemDataField("Length", ref _spinner.data.Length, FinishEdit);
				DataFieldUtils.ItemDataField("Height", ref _spinner.data.Height, FinishEdit);
				DataFieldUtils.ItemDataField("Rotation", ref _spinner.data.Rotation, FinishEdit);
				DataFieldUtils.ItemDataField("Angle Max", ref _spinner.data.AngleMax, FinishEdit, ("dirtyMesh", false));
				DataFieldUtils.ItemDataField("Angle Min", ref _spinner.data.AngleMin, FinishEdit, ("dirtyMesh", false));
				DataFieldUtils.ItemDataField("Elasticity", ref _spinner.data.Elasticity, FinishEdit, ("dirtyMesh", false));
				SurfaceField("Surface", ref _spinner.data.Surface);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPhysics, "Physics")) {
				DataFieldUtils.ItemDataField("Damping", ref _spinner.data.Damping, FinishEdit, ("dirtyMesh", false));
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutMisc = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMisc, "Misc")) {
				DataFieldUtils.ItemDataField("Timer Enabled", ref _spinner.data.IsTimerEnabled, FinishEdit, ("dirtyMesh", false));
				DataFieldUtils.ItemDataField("Timer Interval", ref _spinner.data.TimerInterval, FinishEdit, ("dirtyMesh", false));
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();
		}
	}
}
