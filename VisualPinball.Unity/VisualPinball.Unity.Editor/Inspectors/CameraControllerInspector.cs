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
		SerializedProperty xoffset;
		SerializedProperty yoffset;
		SerializedProperty zoffset;
		SerializedProperty fov;
		SerializedProperty distance;
		SerializedProperty angle;
		SerializedProperty orbit;
		CameraController cameraController;


		private void OnEnable()
		{
			
			preset = serializedObject.FindProperty("Preset"); 
			xoffset = serializedObject.FindProperty("XOffset");
			yoffset = serializedObject.FindProperty("YOffset");
			zoffset = serializedObject.FindProperty("ZOffset");
			fov = serializedObject.FindProperty("FOV");
			distance = serializedObject.FindProperty("Distance");
			angle = serializedObject.FindProperty("Angle");
			orbit = serializedObject.FindProperty("Orbit"); 
		}


		public override void OnInspectorGUI()
		{
			//base.OnInspectorGUI(); 
			//base.OnInspectorGUI(); 
			cameraController = target as CameraController;
			serializedObject.Update();
			EditorGUILayout.PropertyField(xoffset);
			EditorGUILayout.PropertyField(yoffset);
			EditorGUILayout.PropertyField(zoffset);
			EditorGUILayout.PropertyField(fov);
			EditorGUILayout.PropertyField(distance);
			EditorGUILayout.PropertyField(angle);
			EditorGUILayout.PropertyField(orbit);


			if(cameraController == null) return; 
			
			EditorGUILayout.IntSlider(preset, 1, cameraController.presetCount);

			serializedObject.ApplyModifiedProperties();

			if(GUILayout.Button("Add New Preset"))
			{
				cameraController.CreatePreset();
			}
			if(GUILayout.Button("Delete Preset"))
			{
				cameraController.RemovePreset();
			}
			if(GUILayout.Button("Orbit"))
			{
				cameraController.AnimateOrbit(); 
			}

			cameraController.ApplyProperties(); 

		}
	}

}
