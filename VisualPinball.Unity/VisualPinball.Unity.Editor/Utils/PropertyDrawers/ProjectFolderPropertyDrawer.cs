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
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{
	[Serializable]
	public class ProjectFolderReference
	{
		public string Guid = string.Empty;
	}

	[CustomPropertyDrawer(typeof(ProjectFolderReference))]
	public class ProjectFolderPropertyDrawer : PropertyDrawer
	{
		private SerializedProperty guid;
		private Object obj;

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			guid = property.FindPropertyRelative("Guid");
			obj = AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(guid.stringValue));

			GUIContent guiContent = EditorGUIUtility.ObjectContent(obj, typeof(DefaultAsset));

			Rect r = EditorGUI.PrefixLabel(position, label);

			Rect textFieldRect = r;
			textFieldRect.width -= 19f;

			GUIStyle textFieldStyle = new GUIStyle("TextField") {
				imagePosition = obj ? ImagePosition.ImageLeft : ImagePosition.TextOnly
			};

			if (GUI.Button(textFieldRect, guiContent, textFieldStyle) && obj)
				EditorGUIUtility.PingObject(obj);

			if (textFieldRect.Contains(Event.current.mousePosition)) {
				if (Event.current.type == EventType.DragUpdated) {
					Object reference = DragAndDrop.objectReferences[0];
					string path = AssetDatabase.GetAssetPath(reference);
					DragAndDrop.visualMode = Directory.Exists(path) ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
					Event.current.Use();
				} else if (Event.current.type == EventType.DragPerform) {
					Object reference = DragAndDrop.objectReferences[0];
					string path = AssetDatabase.GetAssetPath(reference);
					if (Directory.Exists(path)) {
						obj = reference;
						guid.stringValue = AssetDatabase.AssetPathToGUID(path);
					}
					Event.current.Use();
				}
			}

			Rect objectFieldRect = r;
			objectFieldRect.x = textFieldRect.xMax + 1f;
			objectFieldRect.width = 19f;

			if (GUI.Button(objectFieldRect, "", GUI.skin.GetStyle("IN ObjectField"))) {
				string path = EditorUtility.OpenFolderPanel("Select a folder", "", "");
				if (path != string.Empty) {
					if (path.Contains(Application.dataPath)) {
						path = "Assets" + path.Substring(Application.dataPath.Length);
						obj = AssetDatabase.LoadAssetAtPath(path, typeof(DefaultAsset));
						guid.stringValue = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(obj));
					} else Debug.LogError("The path must be in the Assets folder");
				}
			}
		}
	}
}
