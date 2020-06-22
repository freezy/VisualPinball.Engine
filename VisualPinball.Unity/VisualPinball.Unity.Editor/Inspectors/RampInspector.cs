using UnityEditor;
using VisualPinball.Unity.Editor.Utils;
using VisualPinball.Unity.VPT.Ramp;

namespace VisualPinball.Unity.Editor.Inspectors
{
	[CustomEditor(typeof(RampBehavior))]
	public class RampInspector : DragPointsItemInspector
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
			base.OnPreInspectorGUI();

			if (_foldoutColorsAndFormatting = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutColorsAndFormatting, "Colors & Formatting")) {
				DataFieldUtils.ItemDataField("Visible", ref _ramp.data.IsVisible, FinishEdit);
				DataFieldUtils.ItemDataField("Depth Bias", ref _ramp.data.DepthBias, FinishEdit);
				DataFieldUtils.ItemDataField("Reflection Enabled", ref _ramp.data.IsReflectionEnabled, FinishEdit);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPosition = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPosition, "Position")) {
				DataFieldUtils.ItemDataField("Top Height", ref _ramp.data.HeightTop, FinishEdit);
				DataFieldUtils.ItemDataField("Bottom Height", ref _ramp.data.HeightBottom, FinishEdit);
				
				EditorGUILayout.Space(10);
				DataFieldUtils.ItemDataField("Top Width", ref _ramp.data.WidthTop, FinishEdit);
				DataFieldUtils.ItemDataField("Bottom Width", ref _ramp.data.WidthBottom, FinishEdit);
				
				EditorGUILayout.Space(10);
				EditorGUILayout.LabelField("Visible Wall");
				EditorGUI.indentLevel++;
				DataFieldUtils.ItemDataField("Left Wall", ref _ramp.data.LeftWallHeightVisible, FinishEdit);
				DataFieldUtils.ItemDataField("Right Wall", ref _ramp.data.RightWallHeightVisible, FinishEdit);
				EditorGUI.indentLevel--;
				EditorGUILayout.LabelField("Wire Ramp");
				EditorGUI.indentLevel++;
				DataFieldUtils.ItemDataField("Diameter", ref _ramp.data.WireDiameter, FinishEdit);
				DataFieldUtils.ItemDataField("Distance X", ref _ramp.data.WireDistanceX, FinishEdit);
				DataFieldUtils.ItemDataField("Distance Y", ref _ramp.data.WireDistanceY, FinishEdit);
				EditorGUI.indentLevel--;
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPhysics, "Physics")) {
				DataFieldUtils.ItemDataField("Has Hit Event", ref _ramp.data.HitEvent, FinishEdit, ("dirtyMesh", false));
				DataFieldUtils.ItemDataField("Hit Threshold", ref _ramp.data.Threshold, FinishEdit, ("dirtyMesh", false));

				EditorGUILayout.LabelField("Physical Wall");
				EditorGUI.indentLevel++;
				DataFieldUtils.ItemDataField("Left Wall", ref _ramp.data.LeftWallHeight, FinishEdit);
				DataFieldUtils.ItemDataField("Right Wall", ref _ramp.data.RightWallHeight, FinishEdit);
				EditorGUI.indentLevel--;

				EditorGUI.BeginDisabledGroup(_ramp.data.OverwritePhysics);
				DataFieldUtils.ItemDataField("Physics Material", ref _ramp.data.PhysicsMaterial, FinishEdit, ("dirtyMesh", false));
				EditorGUI.EndDisabledGroup();

				DataFieldUtils.ItemDataField("Overwrite Material Settings", ref _ramp.data.OverwritePhysics, FinishEdit, ("dirtyMesh", false));

				EditorGUI.BeginDisabledGroup(!_ramp.data.OverwritePhysics);
				DataFieldUtils.ItemDataField("Elasticity", ref _ramp.data.Elasticity, FinishEdit, ("dirtyMesh", false));
				DataFieldUtils.ItemDataField("Friction", ref _ramp.data.Friction, FinishEdit, ("dirtyMesh", false));
				DataFieldUtils.ItemDataField("Scatter Angle", ref _ramp.data.Scatter, FinishEdit, ("dirtyMesh", false));
				EditorGUI.EndDisabledGroup();

				DataFieldUtils.ItemDataField("Collidable", ref _ramp.data.IsCollidable, FinishEdit, ("dirtyMesh", false));
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutMisc = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMisc, "Misc")) {
				DataFieldUtils.ItemDataField("Timer Enabled", ref _ramp.data.IsTimerEnabled, FinishEdit, ("dirtyMesh", false));
				DataFieldUtils.ItemDataField("Timer Interval", ref _ramp.data.TimerInterval, FinishEdit, ("dirtyMesh", false));
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();
		}
	}
}
