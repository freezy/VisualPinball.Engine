// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using UnityEditor;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(SpringHingeAnimationComponent)), CanEditMultipleObjects]
	public class SpringHingeAnimationInspector : UnityEditor.Editor
	{
		private SerializedProperty _emitter;
		private SerializedProperty _rotationAxis;

		private void OnEnable()
		{
			_emitter = serializedObject.FindProperty(nameof(SpringHingeAnimationComponent._emitter));
			_rotationAxis = serializedObject.FindProperty(nameof(SpringHingeAnimationComponent.RotationAxis));
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();
			EditorGUILayout.PropertyField(_emitter);
			EditorGUILayout.PropertyField(_rotationAxis);
			serializedObject.ApplyModifiedProperties();
			EditorGUILayout.HelpBox("Keep this moving transform below the fixed spring-hinge pivot. Put the visual toy and any owned magnet below this transform.", MessageType.Info);
		}
	}
}
