// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System.Linq;
using UnityEditor;
using VisualPinball.Engine.Common;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(Player))]
	[CanEditMultipleObjects]
	public class PlayerInspector : UnityEditor.Editor
	{
		private IPhysicsEngine[] _physicsEngines;
		private string[] _physicsEngineNames;
		private int _physicsEngineIndex;

		private IDebugUI[] _debugUIs;
		private string[] _debugUINames;
		private int _debugUIIndex;

		public override void OnInspectorGUI()
		{
			var player = (Player) target;
			if (player == null) {
				return;
			}
			DrawEngineSelector("Physics Engine", ref player.physicsEngineId, ref _physicsEngines, ref _physicsEngineNames, ref _physicsEngineIndex);
			DrawEngineSelector("Debug UI", ref player.debugUiId, ref _debugUIs, ref _debugUINames, ref _debugUIIndex);
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
