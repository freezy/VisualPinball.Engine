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

using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{

	[CustomEditor(typeof(DefaultGamelogicEngine)), CanEditMultipleObjects]
	public class DefaultGamelogicEngineInspector : BaseEditor<DefaultGamelogicEngine>
	{
		private DefaultGamelogicEngine _gamelogicEngine;

		private int _value1;
		private int _value2;
		private int _value3;

		private void OnEnable()
		{
			_gamelogicEngine = target as DefaultGamelogicEngine;
		}

		public override void OnInspectorGUI()
		{
			_value1 = EditorGUILayout.IntSlider("Value 1", _value1, 0, 255);
			_value2 = EditorGUILayout.IntSlider("Value 2", _value2, 0, 255);
			_value3 = EditorGUILayout.IntSlider("Value 3", _value3, 0, 255);

			if (GUILayout.Button("Apply Each")) {
				_gamelogicEngine.SetLamp("gi_1", _value1);
				_gamelogicEngine.SetLamp("gi_2", _value2);
				_gamelogicEngine.SetLamp("gi_3", _value3);
			}

			if (GUILayout.Button("Apply At Once")) {
				_gamelogicEngine.SetLamps(new LampEventArgs[] {
					new LampEventArgs("gi_1", _value1),
					new LampEventArgs("gi_2", _value2),
					new LampEventArgs("gi_3", _value3),
				});
			}
		}
	}
}
