using UnityEditor;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(RubberAuthoring))]
	public class RubberInspector : DragPointsItemInspector
	{
		private RubberAuthoring _rubber;
		private bool _foldoutColorsAndFormatting = true;
		private bool _foldoutPosition = true;
		private bool _foldoutPhysics = true;
		private bool _foldoutMisc = true;

		protected override void OnEnable()
		{
			base.OnEnable();
			_rubber = target as RubberAuthoring;
		}

		public override void OnInspectorGUI()
		{
			OnPreInspectorGUI();

			if (_foldoutColorsAndFormatting = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutColorsAndFormatting, "Colors & Formatting")) {
				TextureField("Image", ref _rubber.data.Image);
				MaterialField("Material", ref _rubber.data.Material);
				ItemDataField("Visible", ref _rubber.data.IsVisible);
				ItemDataField("Static", ref _rubber.data.StaticRendering);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPosition = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPosition, "Position")) {
				ItemDataField("Height", ref _rubber.data.Height);
				ItemDataField("Thickness", ref _rubber.data.Thickness);
				EditorGUILayout.LabelField("Orientation");
				EditorGUI.indentLevel++;
				ItemDataField("RotX", ref _rubber.data.RotX);
				ItemDataField("RotY", ref _rubber.data.RotY);
				ItemDataField("RotZ", ref _rubber.data.RotZ);
				EditorGUI.indentLevel--;
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPhysics, "Physics")) {
				EditorGUI.BeginDisabledGroup(_rubber.data.OverwritePhysics);
				MaterialField("Physics Material", ref _rubber.data.PhysicsMaterial, dirtyMesh: false);
				EditorGUI.EndDisabledGroup();

				ItemDataField("Overwrite Material Settings", ref _rubber.data.OverwritePhysics, dirtyMesh: false);

				EditorGUI.BeginDisabledGroup(!_rubber.data.OverwritePhysics);
				ItemDataField("Elasticity", ref _rubber.data.Elasticity, dirtyMesh: false);
				ItemDataField("Elasticity Falloff", ref _rubber.data.ElasticityFalloff, dirtyMesh: false);
				ItemDataField("Friction", ref _rubber.data.Friction, dirtyMesh: false);
				ItemDataField("Scatter Angle", ref _rubber.data.Scatter, dirtyMesh: false);
				EditorGUI.EndDisabledGroup();

				ItemDataField("Hit Height", ref _rubber.data.HitHeight, dirtyMesh: false);
				ItemDataField("Collidable", ref _rubber.data.IsCollidable, dirtyMesh: false);
				ItemDataField("Has Hit Event", ref _rubber.data.HitEvent, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutMisc = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMisc, "Misc")) {
				ItemDataField("Timer Enabled", ref _rubber.data.IsTimerEnabled, dirtyMesh: false);
				ItemDataField("Timer Interval", ref _rubber.data.TimerInterval, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();
		}
	}
}
