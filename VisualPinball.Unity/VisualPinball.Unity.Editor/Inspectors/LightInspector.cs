using UnityEditor;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(LightAuthoring))]
	public class LightInspector : ItemInspector
	{
		private LightAuthoring _light;
		private bool _foldoutColorsAndFormatting = true;
		private bool _foldoutPosition = true;
		private bool _foldoutStateAndPhysics = true;
		private bool _foldoutMisc = true;

		private static string[] _lightStateStrings = { "Off", "On", "Blinking" };
		private static int[] _lightStateValues = { LightStatus.LightStateOff, LightStatus.LightStateOn, LightStatus.LightStateBlinking };

		protected override void OnEnable()
		{
			base.OnEnable();
			_light = target as LightAuthoring;
		}

		public override void OnInspectorGUI()
		{
			if (_foldoutColorsAndFormatting = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutColorsAndFormatting, "Colors & Formatting")) {
				ItemDataField("Falloff", ref _light.data.Falloff, dirtyMesh: false);
				ItemDataField("Intensity", ref _light.data.Intensity, dirtyMesh: false);

				EditorGUILayout.LabelField("Fade Speed");
				EditorGUI.indentLevel++;
				ItemDataField("Up", ref _light.data.FadeSpeedUp, dirtyMesh: false);
				ItemDataField("Down", ref _light.data.FadeSpeedDown, dirtyMesh: false);
				EditorGUI.indentLevel--;

				ItemDataField("Color", ref _light.data.Color2, dirtyMesh: false); // Note: using color2 since that's the hot/center color in vpx

				EditorGUILayout.LabelField("Bulb");
				EditorGUI.indentLevel++;
				ItemDataField("Enable", ref _light.data.IsBulbLight, dirtyMesh: false);
				ItemDataField("Scale Mesh", ref _light.data.MeshRadius, dirtyMesh: false);
				EditorGUI.indentLevel--;
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPosition = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPosition, "Position")) {
				ItemDataField("", ref _light.data.Center);
				SurfaceField("Surface", ref _light.data.Surface);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutStateAndPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutStateAndPhysics, "State & Physics")) {
				DropDownField("State", ref _light.data.State, _lightStateStrings, _lightStateValues);
				ItemDataField("Blink Pattern", ref _light.data.BlinkPattern, dirtyMesh: false);
				ItemDataField("Blink Interval", ref _light.data.BlinkInterval, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutMisc = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMisc, "Misc")) {
				ItemDataField("Timer Enabled", ref _light.data.IsTimerEnabled, dirtyMesh: false);
				ItemDataField("Timer Interval", ref _light.data.TimerInterval, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();
		}
	}
}
