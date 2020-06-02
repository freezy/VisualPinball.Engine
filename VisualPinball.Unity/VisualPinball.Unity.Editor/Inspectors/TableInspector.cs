// ReSharper disable ArrangeTypeMemberModifiers
// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedType.Global

using System.Linq;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Common;
using VisualPinball.Unity.Physics.Engine;
using VisualPinball.Unity.VPT.Table;

namespace VisualPinball.Unity.Editor.Inspectors
{
	[CustomEditor(typeof(TableBehavior))]
	[CanEditMultipleObjects]
	public class TableInspector : UnityEditor.Editor
	{
		private IPhysicsEngineNew[] _physicsEngines;
		private string[] _physicsEngineNames;
		private int _physicsEngineIndex;

		private IPhysicsEngineNew[] _debugUIs;
		private string[] _debugUINames;
		private int _debugUIIndex;

		public override void OnInspectorGUI()
		{
			DrawEngineSelector("Physics Engine", ref _physicsEngines, ref _physicsEngineNames, ref _physicsEngineIndex);
			DrawEngineSelector("Debug UI", ref _debugUIs, ref _debugUINames, ref _debugUIIndex);

			if (!EditorApplication.isPlaying) {
				DrawDefaultInspector();
				if (GUILayout.Button("Export VPX")) {
					var tableComponent = (TableBehavior) target;
					var table = tableComponent.RecreateTable();
					var path = EditorUtility.SaveFilePanel(
						"Export table as VPX",
						"",
						table.Name + ".vpx",
						"vpx");

					if (!string.IsNullOrEmpty(path)) {
						table.Save(path);
					}
				}
			}
		}

		private void DrawEngineSelector<T>(string engineName, ref T[] instances, ref string[] names, ref int index) where T : IEngine
		{
			var tableComponent = (TableBehavior) target;
			var engineProvider = EngineProvider<T>.Instance;
			if (instances == null) {
				instances = engineProvider.GetAll().ToArray();
				names = instances.Select(x => x.Name).ToArray();
			}
			var newPhysicsEngineIndex = EditorGUILayout.Popup(engineName, index, names);
			if (index != newPhysicsEngineIndex) {
				tableComponent.physicsEngineId = engineProvider.GetId(instances[newPhysicsEngineIndex]);
				index = newPhysicsEngineIndex;
			}
		}
	}
}
