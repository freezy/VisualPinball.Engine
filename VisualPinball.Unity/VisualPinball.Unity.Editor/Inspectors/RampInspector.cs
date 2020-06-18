using UnityEditor;
using VisualPinball.Unity.VPT.Ramp;

namespace VisualPinball.Unity.Editor.Inspectors
{
	[CustomEditor(typeof(RampBehavior))]
	public class RampInspector : ItemInspector
	{
		private RampBehavior _ramp;
		private bool _foldoutColorsAndFormatting = true;
		private bool _foldoutPosition = true;
		private bool _foldoutPhysics = true;
		private bool _foldoutMisc = true;

		protected override void OnEnable()
		{
			base.OnEnable();
			_ramp = target as RampBehavior;
		}

		public override void OnInspectorGUI()
		{
			_dragPointsEditor.OnInspectorGUI(target);

			base.OnPreInspectorGUI();

			if (_foldoutColorsAndFormatting = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutColorsAndFormatting, "Colors & Formatting")) {
				ItemDataField("Visible", ref _ramp.data.IsVisible);
				ItemDataField("Depth Bias", ref _ramp.data.DepthBias);
				ItemDataField("Reflection Enabled", ref _ramp.data.IsReflectionEnabled);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPosition = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPosition, "Position")) {
				ItemDataField("Top Height", ref _ramp.data.HeightTop);
				ItemDataField("Bottom Height", ref _ramp.data.HeightBottom);
				
				EditorGUILayout.Space(10);
				ItemDataField("Top Width", ref _ramp.data.WidthTop);
				ItemDataField("Bottom Width", ref _ramp.data.WidthBottom);
				
				EditorGUILayout.Space(10);
				EditorGUILayout.LabelField("Visible Wall");
				EditorGUI.indentLevel++;
				ItemDataField("Left Wall", ref _ramp.data.LeftWallHeightVisible);
				ItemDataField("Right Wall", ref _ramp.data.RightWallHeightVisible);
				EditorGUI.indentLevel--;
				EditorGUILayout.LabelField("Wire Ramp");
				EditorGUI.indentLevel++;
				ItemDataField("Diameter", ref _ramp.data.WireDiameter);
				ItemDataField("Distance X", ref _ramp.data.WireDistanceX);
				ItemDataField("Distance Y", ref _ramp.data.WireDistanceY);
				EditorGUI.indentLevel--;
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPhysics, "Physics")) {
				ItemDataField("Has Hit Event", ref _ramp.data.HitEvent, dirtyMesh: false);
				ItemDataField("Hit Threshold", ref _ramp.data.Threshold, dirtyMesh: false);

				EditorGUILayout.LabelField("Physical Wall");
				EditorGUI.indentLevel++;
				ItemDataField("Left Wall", ref _ramp.data.LeftWallHeight);
				ItemDataField("Right Wall", ref _ramp.data.RightWallHeight);
				EditorGUI.indentLevel--;

				EditorGUI.BeginDisabledGroup(_ramp.data.OverwritePhysics);
				ItemDataField("Physics Material", ref _ramp.data.PhysicsMaterial, dirtyMesh: false);
				EditorGUI.EndDisabledGroup();

				ItemDataField("Overwrite Material Settings", ref _ramp.data.OverwritePhysics, dirtyMesh: false);

				EditorGUI.BeginDisabledGroup(!_ramp.data.OverwritePhysics);
				ItemDataField("Elasticity", ref _ramp.data.Elasticity, dirtyMesh: false);
				ItemDataField("Friction", ref _ramp.data.Friction, dirtyMesh: false);
				ItemDataField("Scatter Angle", ref _ramp.data.Scatter, dirtyMesh: false);
				EditorGUI.EndDisabledGroup();

				ItemDataField("Collidable", ref _ramp.data.IsCollidable, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutMisc = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMisc, "Misc")) {
				ItemDataField("Timer Enabled", ref _ramp.data.IsTimerEnabled, dirtyMesh: false);
				ItemDataField("Timer Interval", ref _ramp.data.TimerInterval, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();
		}
	}
}
