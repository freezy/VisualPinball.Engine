// ReSharper disable ArrangeTypeMemberModifiers
// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedType.Global

using System.Linq;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Common;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(TableAuthoring))]
	[CanEditMultipleObjects]
	public class TableInspector : UnityEditor.Editor
	{
		private IPhysicsEngine[] _physicsEngines;
		private string[] _physicsEngineNames;
		private int _physicsEngineIndex;

		private IDebugUI[] _debugUIs;
		private string[] _debugUINames;
		private int _debugUIIndex;

		public override void OnInspectorGUI()
		{
			var tableComponent = (TableAuthoring) target;
			DrawEngineSelector("Physics Engine", ref tableComponent.physicsEngineId, ref _physicsEngines, ref _physicsEngineNames, ref _physicsEngineIndex);
			DrawEngineSelector("Debug UI", ref tableComponent.debugUiId, ref _debugUIs, ref _debugUINames, ref _debugUIIndex);

			if (!EditorApplication.isPlaying) {
				DrawDefaultInspector();
				if (GUILayout.Button("Export VPX")) {
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

		private void DrawEngineSelector<T>(string engineName, ref string engineId, ref T[] instances, ref string[] names, ref int index) where T : IEngine
		{
			if (instances == null) {
				// get all instances
				instances = EngineProvider<T>.GetAll().ToArray();
				names = instances.Select(x => x.Name).ToArray();

				// set the current index based on the table's ID
				index = -1;
				for (var i = 0; i < instances.Length; i++) {
					if (EngineProvider<T>.GetId(instances[i]) == engineId) {
						index = i;
						break;
					}
				}
				if (instances.Length > 0 && index < 0) {
					index = 0;
					engineId = EngineProvider<T>.GetId(instances[index]);
				}
			}
			if (names.Length == 0) {
				return;
			}
			var newIndex = EditorGUILayout.Popup(engineName, index, names);
			if (index != newIndex) {
				index = newIndex;
				engineId = EngineProvider<T>.GetId(instances[newIndex]);
			}
		}
	}
}
