using UnityEditor;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(RampAuthoring))]
	public class RampInspector : DragPointsItemInspector
	{
		private RampAuthoring _ramp;
		private bool _foldoutColorsAndFormatting = true;
		private bool _foldoutPosition = true;
		private bool _foldoutPhysics = true;
		private bool _foldoutMisc = true;

		private static string[] _rampTypeStrings = {
			"Flat",
			"1 Wire",
			"2 Wire",
			"3 Wire Left",
			"3 Wire Right",
			"4 Wire",
		};
		private static int[] _rampTypeValues = {
			RampType.RampTypeFlat,
			RampType.RampType1Wire,
			RampType.RampType2Wire,
			RampType.RampType3WireLeft,
			RampType.RampType3WireRight,
			RampType.RampType4Wire,
		};
		private static string[] _rampImageAlignmentStrings = {
			"World",
			"Wrap",
		};
		private static int[] _rampImageAlignmentValues = {
			RampImageAlignment.ImageModeWorld,
			RampImageAlignment.ImageModeWrap,
		};

		protected override void OnEnable()
		{
			base.OnEnable();
			_ramp = target as RampAuthoring;
		}

		public override void OnInspectorGUI()
		{
			OnPreInspectorGUI();

			if (_foldoutColorsAndFormatting = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutColorsAndFormatting, "Colors & Formatting")) {
				DropDownField("Type", ref _ramp.data.RampType, _rampTypeStrings, _rampTypeValues);
				TextureField("Image", ref _ramp.data.Image);
				MaterialField("Material", ref _ramp.data.Material);
				DropDownField("Image Mode", ref _ramp.data.ImageAlignment, _rampImageAlignmentStrings, _rampImageAlignmentValues);
				ItemDataField("Apply Image To Wall", ref _ramp.data.ImageWalls);
				ItemDataField("Visible", ref _ramp.data.IsVisible);
				ItemDataField("Depth Bias", ref _ramp.data.DepthBias);
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
				MaterialField("Physics Material", ref _ramp.data.PhysicsMaterial, dirtyMesh: false);
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
