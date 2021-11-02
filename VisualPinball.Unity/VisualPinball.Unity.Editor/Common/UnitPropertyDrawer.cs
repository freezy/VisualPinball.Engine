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

using System;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	[CustomPropertyDrawer(typeof(UnitAttribute))]
	public class UnitPropertyDrawer : PropertyDrawer
	{
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			var labelAttribute = attribute as UnitAttribute;
			var labelWidth = labelAttribute!.width + 2f;
			var leftRect = position;
			var rightRect = position;
			leftRect.width -= labelWidth;
			rightRect.x = position.width;
			rightRect.width = labelWidth;

			EditorGUI.PropertyField(leftRect, property, label);
			GUI.Label(rightRect, labelAttribute.label, labelAttribute.labelStyle);
		}
	}
}
