// ReSharper disable ArrangeTypeMemberModifiers
// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedType.Global

using UnityEditor;
using UnityEngine;
using VisualPinball.Unity.Components;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(VisualPinballTable))]
	[CanEditMultipleObjects]
	public class VisualPinballTableEditor : UnityEditor.Editor
	{
		public override void OnInspectorGUI()
		{
			serializedObject.Update();
			DrawDefaultInspector();
			if (GUILayout.Button("Export VPX")) {
				var tableComponent = (VisualPinballTable) target;
				var table = tableComponent.RecreateTable();
				var path = EditorUtility.SaveFilePanel(
					"Export table as VPX",
					"",
					table.Name + ".vpx",
					"vpx");
				table.Save(path);
			}

			//serializedObject.ApplyModifiedProperties();
		}
	}
}
