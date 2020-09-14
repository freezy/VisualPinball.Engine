// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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

using UnityEngine;
using UnityEditor;
using System;

namespace VisualPinball.Unity.Editor
{
	public class ManagerListTextFieldPopup : PopupWindowContent
	{
		private string _label = "";
		private string _value = "";
		private Action<string> _saveAction;

		public ManagerListTextFieldPopup(string label, string value, Action<string> saveAction)
		{
			_label = label;
			_value = value;
			_saveAction = saveAction;
		}

		public override Vector2 GetWindowSize()
		{
			return new Vector2(200, 80);
		}

		public override void OnGUI(Rect rect)
		{
			EditorGUILayout.LabelField("Add " + _label); 

			_value = EditorGUILayout.TextField(_value);

			if (GUILayout.Button("Save"))
			{
				if (_value.Trim().Length > 0)
				{
					_saveAction(_value.Trim());
					editorWindow.Close();
				}
			}
		}
	}
}
