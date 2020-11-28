using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;


namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(CameraController)), CanEditMultipleObjects]
	public class CameraControllerEditor : UnityEditor.Editor
	{

		SerializedProperty preset; 
		SerializedProperty yoffset; 
		SerializedProperty fov;
		SerializedProperty distance;
		SerializedProperty angle;
		SerializedProperty orbit; 

		private void OnEnable()
		{
			preset = serializedObject.FindProperty("Preset"); 
			yoffset = serializedObject.FindProperty("YOffset");
			fov = serializedObject.FindProperty("FOV");
			distance = serializedObject.FindProperty("Distance");
			angle = serializedObject.FindProperty("Angle");
			orbit = serializedObject.FindProperty("Orbit"); 
		}


		public override void OnInspectorGUI()
		{
			//base.OnInspectorGUI(); 
			//base.OnInspectorGUI(); 
			serializedObject.Update();
			CameraController cc = target as CameraController;
			//EditorGUILayout.Slider(preset, 0, cc.presetCount); 
			EditorGUILayout.PropertyField(yoffset);
			EditorGUILayout.PropertyField(fov);
			EditorGUILayout.PropertyField(distance);
			EditorGUILayout.PropertyField(angle);
			EditorGUILayout.PropertyField(orbit);

			serializedObject.ApplyModifiedProperties();
			cc.ApplyProperties(); 

		}
	}

}
