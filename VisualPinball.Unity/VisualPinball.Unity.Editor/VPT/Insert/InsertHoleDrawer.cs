// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
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

using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// A hole on one line: its number, as the scene labels it, then X, Y and
	/// the radius.
	/// </summary>
	[CustomPropertyDrawer(typeof(InsertHole))]
	public class InsertHoleDrawer : PropertyDrawer
	{
		private static readonly Regex Index = new(@"\[(\d+)\]$", RegexOptions.Compiled);

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUIUtility.singleLineHeight;

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			var center = property.FindPropertyRelative(nameof(InsertHole.Center));
			var radius = property.FindPropertyRelative(nameof(InsertHole.Radius));
			var match = Index.Match(property.propertyPath);
			var title = new GUIContent(match.Success ? match.Groups[1].Value : label.text, center.tooltip);

			label = EditorGUI.BeginProperty(position, title, property);
			var rect = EditorGUI.PrefixLabel(position, label);
			var indent = EditorGUI.indentLevel;
			var labelWidth = EditorGUIUtility.labelWidth;
			EditorGUI.indentLevel = 0;
			const float gap = 6f;
			var third = (rect.width - 2f * gap) / 3f;
			EditorGUIUtility.labelWidth = 14f;

			var x = center.FindPropertyRelative("x");
			var y = center.FindPropertyRelative("y");
			EditorGUI.PropertyField(new Rect(rect.x, rect.y, third, rect.height), x, new GUIContent("X", center.tooltip));
			EditorGUI.PropertyField(new Rect(rect.x + third + gap, rect.y, third, rect.height), y, new GUIContent("Y", center.tooltip));
			EditorGUI.PropertyField(new Rect(rect.x + 2f * (third + gap), rect.y, third, rect.height), radius, new GUIContent("R", radius.tooltip));

			EditorGUIUtility.labelWidth = labelWidth;
			EditorGUI.indentLevel = indent;
			EditorGUI.EndProperty();
		}
	}
}
