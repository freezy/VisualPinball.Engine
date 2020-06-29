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
				ItemDataField("", ref _light.data.Center, dirtyMesh: false);
				SurfaceField("Surface", ref _light.data.Surface);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();
		}
	}
}
