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

// ReSharper disable AssignmentInConditionalExpression

using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(CollisionSwitchComponent)), CanEditMultipleObjects]
	public class CollisionSwitchInspector : ItemInspector
	{
		protected override MonoBehaviour UndoTarget => target as MonoBehaviour;

		public override void OnInspectorGUI()
		{
			base.OnInspectorGUI();

			if (Application.isPlaying) {
				var tableApi = TableComponent.GetComponent<Player>().TableApi;
				var switchApi = tableApi.Switchable((MonoBehaviour)target);

				if (switchApi is CollisionSwitchApi { IsHittable: true }) {
					DrawSwitch($"Collision switch exposed as {target.name}.", switchApi.IsSwitchEnabled);
				}
				else {
					GUILayout.Label($"Collision switch not connected to hittable.");
				}
			}
			else {
				GUILayout.Label($"Collision switch will be exposed as {target.name}.");
			}
		}

		private static void DrawSwitch(string label, bool sw)
		{
			var labelPos = EditorGUILayout.GetControlRect();
			labelPos.height = 18;

			var icon = Icons.Switch(sw, IconSize.Small, sw ? IconColor.Orange : IconColor.Gray);
			var width = ((labelPos.height / icon.height) * icon.width) + 2;

			labelPos.x += width;
			labelPos.width -= width;

			var switchPos = new Rect(labelPos.x - width, labelPos.y, labelPos.height, labelPos.height);
			GUI.Label(labelPos, label);
			GUI.DrawTexture(switchPos, icon);
		}
	}
}
