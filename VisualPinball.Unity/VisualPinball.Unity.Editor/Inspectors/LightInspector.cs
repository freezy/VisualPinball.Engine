using UnityEditor;
using VisualPinball.Unity.VPT.Light;

namespace VisualPinball.Unity.Editor.Inspectors
{
	[CustomEditor(typeof(LightBehavior))]
	public class LightInspector : ItemInspector
	{
		private LightBehavior _light;
		private bool _foldoutColorsAndFormatting = true;
		private bool _foldoutPosition = true;

		protected override void OnEnable()
		{
			base.OnEnable();
			_light = target as LightBehavior;
		}

		public override void OnInspectorGUI()
		{
			if (_foldoutColorsAndFormatting = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutColorsAndFormatting, "Colors & Formatting")) {
				ItemDataField("Falloff", ref _light.data.Falloff);
				ItemDataField("Intensity", ref _light.data.Intensity);

				EditorGUILayout.LabelField("Fade Speed");
				EditorGUI.indentLevel++;
				ItemDataField("Up", ref _light.data.FadeSpeedUp);
				ItemDataField("Down", ref _light.data.FadeSpeedDown);
				EditorGUI.indentLevel--;

				ItemDataField("Color", ref _light.data.Color2); // Note: using color2 since that's the hot/center color in vpx

				EditorGUILayout.LabelField("Bulb");
				EditorGUI.indentLevel++;
				ItemDataField("Enable", ref _light.data.IsBulbLight);
				ItemDataField("Scale Mesh", ref _light.data.MeshRadius);
				EditorGUI.indentLevel--;
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPosition = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPosition, "Position")) {
				ItemDataField("", ref _light.data.Center);
				SurfaceField("Surface", ref _light.data.Surface);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();
		}
	}
}
