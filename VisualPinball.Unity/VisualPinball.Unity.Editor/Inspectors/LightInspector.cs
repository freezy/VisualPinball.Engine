using UnityEditor;
using VisualPinball.Unity.Editor.Utils;
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
				DataFieldUtils.ItemDataField("Falloff", ref _light.data.Falloff, FinishEdit, ("dirtyMesh",false));
				DataFieldUtils.ItemDataField("Intensity", ref _light.data.Intensity, FinishEdit, ("dirtyMesh", false));

				EditorGUILayout.LabelField("Fade Speed");
				EditorGUI.indentLevel++;
				DataFieldUtils.ItemDataField("Up", ref _light.data.FadeSpeedUp, FinishEdit, ("dirtyMesh", false));
				DataFieldUtils.ItemDataField("Down", ref _light.data.FadeSpeedDown, FinishEdit, ("dirtyMesh", false));
				EditorGUI.indentLevel--;

				DataFieldUtils.ItemDataField("Color", ref _light.data.Color2, FinishEdit, ("dirtyMesh", false)); // Note: using color2 since that's the hot/center color in vpx

				EditorGUILayout.LabelField("Bulb");
				EditorGUI.indentLevel++;
				DataFieldUtils.ItemDataField("Enable", ref _light.data.IsBulbLight, FinishEdit, ("dirtyMesh", false));
				DataFieldUtils.ItemDataField("Scale Mesh", ref _light.data.MeshRadius, FinishEdit, ("dirtyMesh", false));
				EditorGUI.indentLevel--;
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPosition = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPosition, "Position")) {
				DataFieldUtils.ItemDataField("", ref _light.data.Center, FinishEdit, ("dirtyMesh", false));
				SurfaceField("Surface", ref _light.data.Surface);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();
		}
	}
}
