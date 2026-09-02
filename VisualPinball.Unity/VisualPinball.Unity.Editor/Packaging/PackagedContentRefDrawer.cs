// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team

using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	[CustomPropertyDrawer(typeof(PackagedContentRef))]
	public sealed class PackagedContentRefDrawer : PropertyDrawer
	{
		private static readonly string[] Fields = {
			"Kind", "EntryPoint", "Id", "ContentHash", "FileCount", "TotalBytes", "SourceDirectory", "ValidationStatus"
		};

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			if (!property.isExpanded) {
				return EditorGUIUtility.singleLineHeight;
			}
			return (Fields.Length + 1) * (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing);
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			EditorGUI.BeginProperty(position, label, property);
			var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
			property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, label, true);
			if (property.isExpanded) {
				EditorGUI.indentLevel++;
				using (new EditorGUI.DisabledScope(true)) {
					foreach (var fieldName in Fields) {
						line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
						var child = property.FindPropertyRelative(fieldName);
						if (child == null) {
							continue;
						}
						if (fieldName == "TotalBytes") {
							EditorGUI.TextField(line, ObjectNames.NicifyVariableName(fieldName), EditorUtility.FormatBytes(child.longValue));
						} else {
							EditorGUI.PropertyField(line, child, new GUIContent(ObjectNames.NicifyVariableName(fieldName)));
						}
					}
				}
				EditorGUI.indentLevel--;
			}
			EditorGUI.EndProperty();
		}
	}
}
