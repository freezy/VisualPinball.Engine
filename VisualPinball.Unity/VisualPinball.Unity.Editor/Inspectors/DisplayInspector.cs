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

namespace VisualPinball.Unity.Editor
{

	[CustomEditor(typeof(DisplayAuthoring)), CanEditMultipleObjects]
	public class DisplayInspector : UnityEditor.Editor
	{
		private DisplayAuthoring _mb;

		protected void OnEnable()
		{
			_mb = target as DisplayAuthoring;
		}

		public override void OnInspectorGUI()
		{
			var color = EditorGUILayout.ColorField("Color", _mb.Color);
			if (color != _mb.Color) {
				_mb.UpdateColor(color);
			}
		}
	}
}
