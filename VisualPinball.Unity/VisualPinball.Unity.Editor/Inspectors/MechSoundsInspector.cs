// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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
using static VisualPinball.Unity.MechSoundsComponent;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(MechSoundsComponent)), CanEditMultipleObjects]
	public class MechanicalSoundInspector : UnityEditor.Editor
	{

		private MechSoundsComponent _myTarget;
		private SerializedProperty _soundList;
		private const float _buttonWidth = 150;

		private void OnEnable()
		{

			_myTarget = (MechSoundsComponent)target;
			_soundList = serializedObject.FindProperty(nameof(MechSoundsComponent.SoundList));

		}

		public override void OnInspectorGUI()
		{

			MechSoundsComponent ob = (MechSoundsComponent)serializedObject.targetObject;
			GameObject go = ob.gameObject;

			ISoundEmitter component = ob.GetComponent<ISoundEmitter>();
			if (component == null)
			{
				if(EditorUtility.DisplayDialog("Error", "No component attached to this game object implements ISoundEmitter interface.", "ok"))
				{ return; }
				
			}

			serializedObject.Update();

			EditorGUILayout.LabelField("Current Sounds");

			for (int i = 0; i < _soundList.arraySize; i++)
			{
				EditorGUILayout.PropertyField(_soundList.GetArrayElementAtIndex(i), GUIContent.none);
				EditorGUILayout.Space(15);
				GUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();
				if (GUILayout.Button("Remove this sound", GUILayout.Width(_buttonWidth)))
				{
					_soundList.DeleteArrayElementAtIndex(i);

				}
				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();

				EditorGUILayout.Space();
				EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
			}

			EditorGUILayout.Space();
			EditorGUILayout.Space();

			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			if (GUILayout.Button("Add a new sound", GUILayout.Width(_buttonWidth)))
			{
				_myTarget.SoundList.Add(new MechSound());

			}
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();

			serializedObject.ApplyModifiedProperties();
		}



	}
}
