// Visual Pinball Engine
// Copyright (C) 2022 freezy and VPE Team
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

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(PhysicsMaterialComponent)), CanEditMultipleObjects]
	public class PhysicsMaterialInspector : UnityEditor.Editor
	{
		SerializedProperty Elasticity;
		SerializedProperty ElasticityFalloff;
		SerializedProperty ElasticityOverVelocity;
		SerializedProperty Friction;
		SerializedProperty FrictionOverVelocity;
		//SerializedProperty FrictionOverAngularMomentum;

		SerializedProperty ScatterAngle;


		private bool _foldoutDebug = true;
		private bool _foldoutColliders;
		private string[] _currentColliders;
		private Vector2 _scrollPos;

		//protected override MonoBehaviour UndoTarget => null;


		private void OnEnable()
		{
			Elasticity = serializedObject.FindProperty("Elasticity");
			ElasticityFalloff = serializedObject.FindProperty("ElasticityFalloff");
			ElasticityOverVelocity = serializedObject.FindProperty("ElasticityOverVelocity");
			Friction = serializedObject.FindProperty("Friction");
			ScatterAngle = serializedObject.FindProperty("ScatterAngle");
			FrictionOverVelocity = serializedObject.FindProperty("FrictionOverVelocity");
			//FrictionOverAngularMomentum = serializedObject.FindProperty("FrictionOverAngularMomentum");
		}

		private void OnDestroy()
		{
			/*
			if (PhysicsMaterial != null) {
				PhysicsMaterial.ShowGizmos = false;
			}
			*/
		}

		

		public override void OnInspectorGUI()
		{
			//base.DrawDefaultInspector();

			var physicsMaterial = (PhysicsMaterialComponent)target;  //casting from type object to type Physicsmaterial
			

			GUILayout.Label("Elasticity:");
			EditorGUI.indentLevel++;
			//physicsMaterial.Elasticity = EditorGUILayout.DelayedFloatField(physicsMaterial.Elasticity);
			serializedObject.Update();
			//EditorGUILayout.LabelField("Elasticity", physicsMaterial.Elasticity.ToString());
			GUI.enabled = (ElasticityOverVelocity.animationCurveValue.keys.Length == 0);
			EditorGUILayout.PropertyField(Elasticity, true);
			EditorGUILayout.PropertyField(ElasticityFalloff, true);
			GUI.enabled = true;
			EditorGUILayout.PropertyField(ElasticityOverVelocity, new GUIContent("Elasticity / Velocity"), true);

			GUILayout.BeginHorizontal();
			GUILayout.Space(15);
			GUILayout.Label("E/V-Curve:");
			GUILayout.FlexibleSpace();
			if (GUILayout.Button("Generate from Elasticity & Falloff", GUILayout.Width(220)))
			{
				ElasticityOverVelocity.animationCurveValue.keys.Initialize();
				Keyframe[] keyframes = new Keyframe[64];
				for (int i = 0; i < keyframes.Length; i++)
				{
					keyframes[i] = new Keyframe(i, Elasticity.floatValue / (1.0f + ElasticityFalloff.floatValue * i / 18.53f));
				}
				var tempcurve = new AnimationCurve(keyframes);
				for (int i = 0; i < keyframes.Length; i++)
				{
					AnimationUtility.SetKeyLeftTangentMode(tempcurve, i, AnimationUtility.TangentMode.ClampedAuto);
					AnimationUtility.SetKeyRightTangentMode(tempcurve, i, AnimationUtility.TangentMode.ClampedAuto);
				}
				
				ElasticityOverVelocity.animationCurveValue = tempcurve;
				ElasticityOverVelocity.serializedObject.ApplyModifiedProperties();
			}
			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			if (GUILayout.Button("Generate nFozzy Rubber", GUILayout.Width(220)))
			{
				ElasticityOverVelocity.animationCurveValue.keys.Initialize();
				Keyframe[] keyframes = new Keyframe[5];
				keyframes[0] = new Keyframe(0f, 1.1f);
				keyframes[1] = new Keyframe(3.77f, 0.97f);
				keyframes[2] = new Keyframe(5.76f, 0.967f);
				keyframes[3] = new Keyframe(15.84f, 0.874f);
				keyframes[4] = new Keyframe(56f, 0.64f);
				var tempcurve = new AnimationCurve(keyframes);
				for (int i = 0; i < keyframes.Length; i++)
				{
					AnimationUtility.SetKeyLeftTangentMode(tempcurve, i, AnimationUtility.TangentMode.Linear);
					AnimationUtility.SetKeyRightTangentMode(tempcurve, i, AnimationUtility.TangentMode.Linear);
				}

				ElasticityOverVelocity.animationCurveValue = tempcurve;
				ElasticityOverVelocity.serializedObject.ApplyModifiedProperties();

			}
			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			if (GUILayout.Button("Generate nFozzy Sleeves", GUILayout.Width(220)))
			{
				ElasticityOverVelocity.animationCurveValue.keys.Initialize();
				Keyframe[] keyframes = new Keyframe[2];
				keyframes[0] = new Keyframe(0f, 0.85f);
				keyframes[1] = new Keyframe(56f, 0.85f);
				var tempcurve = new AnimationCurve(keyframes);
				for (int i = 0; i < keyframes.Length; i++)
				{
					AnimationUtility.SetKeyLeftTangentMode(tempcurve, i, AnimationUtility.TangentMode.Linear);
					AnimationUtility.SetKeyRightTangentMode(tempcurve, i, AnimationUtility.TangentMode.Linear);
				}

				ElasticityOverVelocity.animationCurveValue = tempcurve;
				ElasticityOverVelocity.serializedObject.ApplyModifiedProperties();
			}
			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			if (GUILayout.Button("Clear (don't use)", GUILayout.Width(220)))
			{
				ElasticityOverVelocity.animationCurveValue.keys.Initialize();
				ElasticityOverVelocity.animationCurveValue = new AnimationCurve();
				ElasticityOverVelocity.serializedObject.ApplyModifiedProperties();
			}
			GUILayout.EndHorizontal();

			EditorGUI.indentLevel--;
			EditorGUI.indentLevel++;
			GUILayout.Label("Friction");
			EditorGUILayout.PropertyField(Friction, true);
			EditorGUILayout.PropertyField(FrictionOverVelocity, new GUIContent("Friction / Velocity"), true);
			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			if (GUILayout.Button("Clear (don't use)", GUILayout.Width(220)))
			{

			}
			GUILayout.EndHorizontal();
			/*
			 * don't use Friction over AngularMomentum yet
			 * 
			EditorGUILayout.PropertyField(FrictionOverAngularMomentum, new GUIContent("Friction / Ang.Momentum"), true);
			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			if (GUILayout.Button("Clear (don't use)", GUILayout.Width(220)))
			{

			}
			GUILayout.EndHorizontal();
			*/

			EditorGUI.indentLevel--;
			EditorGUILayout.PropertyField(ScatterAngle, true);

			serializedObject.ApplyModifiedProperties();
		}

		

		protected bool HasErrors()
		{
			return false;
			/*
			if (HasMainComponent) {
				return false;
			}
			*/
			NoDataError();
			return true;
		}

		private static void NoDataError()
		{
			EditorGUILayout.HelpBox($"Cannot find main component!\n\nYou must have a blubbel component on this GameObject.", MessageType.Error);
		}
	}
}
