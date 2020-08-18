using UnityEditor;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(SpinnerAuthoring))]
	public class SpinnerInspector : ItemInspector
	{
		private SpinnerAuthoring _spinner;
		private bool _foldoutColorsAndFormatting = true;
		private bool _foldoutPosition = true;
		private bool _foldoutPhysics = true;
		private bool _foldoutMisc = true;

		protected override void OnEnable()
		{
			base.OnEnable();
			_spinner = target as SpinnerAuthoring;
		}

		public override void OnInspectorGUI()
		{
			OnPreInspectorGUI();

			if (_foldoutColorsAndFormatting = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutColorsAndFormatting, "Colors & Formatting")) {
				ItemDataField("Visible", ref _spinner.data.IsVisible);
				TextureField("Image", ref _spinner.data.Image);
				MaterialField("Material", ref _spinner.data.Material);
				ItemDataField("Show Bracket", ref _spinner.data.ShowBracket);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPosition = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPosition, "Position")) {
				ItemDataField("", ref _spinner.data.Center);
				ItemDataField("Length", ref _spinner.data.Length);
				ItemDataField("Height", ref _spinner.data.Height);
				ItemDataField("Rotation", ref _spinner.data.Rotation);
				ItemDataField("Angle Max", ref _spinner.data.AngleMax, dirtyMesh: false);
				ItemDataField("Angle Min", ref _spinner.data.AngleMin, dirtyMesh: false);
				ItemDataField("Elasticity", ref _spinner.data.Elasticity, dirtyMesh: false);
				SurfaceField("Surface", ref _spinner.data.Surface);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPhysics, "Physics")) {
				ItemDataField("Damping", ref _spinner.data.Damping, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutMisc = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMisc, "Misc")) {
				ItemDataField("Timer Enabled", ref _spinner.data.IsTimerEnabled, dirtyMesh: false);
				ItemDataField("Timer Interval", ref _spinner.data.TimerInterval, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();
		}
	}
}
