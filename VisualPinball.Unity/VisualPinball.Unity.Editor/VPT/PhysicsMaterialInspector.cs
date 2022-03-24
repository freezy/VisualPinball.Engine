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
	[CustomEditor(typeof(PhysicsMaterial)), CanEditMultipleObjects]
	public class PhysicsMaterialInspector : UnityEditor.Editor
	{
		SerializedProperty Elasticity;
		SerializedProperty ElasticityFalloff;
		SerializedProperty ElasticityOverVelocity;
		SerializedProperty Friction;
		SerializedProperty FrictionOverVelocity;
		SerializedProperty FrictionOverAngularMomentum;

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
			FrictionOverAngularMomentum = serializedObject.FindProperty("FrictionOverAngularMomentum");
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

			var physicsMaterial = (PhysicsMaterial)target;  //casting from type object to type Physicsmaterial
			

			GUILayout.Label("Elasticity:");
			EditorGUI.indentLevel++;
			//physicsMaterial.Elasticity = EditorGUILayout.DelayedFloatField(physicsMaterial.Elasticity);
			serializedObject.Update();
			//EditorGUILayout.LabelField("Elasticity", physicsMaterial.Elasticity.ToString());
			EditorGUILayout.PropertyField(Elasticity, true);
			EditorGUILayout.PropertyField(ElasticityFalloff, true);
			EditorGUILayout.PropertyField(ElasticityOverVelocity, new GUIContent("Elasticity / Velocity"), true);
			GUILayout.BeginHorizontal();
			GUILayout.Space(15);
			GUILayout.Label("E/V-Curve:");
			GUILayout.FlexibleSpace();
			if (GUILayout.Button("Generate from Elasticity & Falloff", GUILayout.Width(220)))
			{

			}
			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			if (GUILayout.Button("Generate nFozzy Rubber", GUILayout.Width(220)))
			{

			}
			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			if (GUILayout.Button("Generate nFozzy Post", GUILayout.Width(220)))
			{

			}
			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			if (GUILayout.Button("Clear (don't use)", GUILayout.Width(220)))
			{

			}
			GUILayout.EndHorizontal();


			//EditorGUILayout.PropertyField(Friction, true);
			//EditorGUILayout.PropertyField(ElasticityFalloff, true);



			//EditorGUILayout.DropdownButton(new GUIContent("Bla"), FocusType.Keyboard);
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

			EditorGUILayout.PropertyField(FrictionOverAngularMomentum, new GUIContent("Friction / Ang.Momentum"), true);
			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			if (GUILayout.Button("Clear (don't use)", GUILayout.Width(220)))
			{

			}
			GUILayout.EndHorizontal();


			EditorGUI.indentLevel--;
			EditorGUILayout.PropertyField(ScatterAngle, true);
			//GUILayout.Label("Test 2 Inspector");

			serializedObject.ApplyModifiedProperties();

			var refresh = false;
			/*
			// scene view toggles
			if (_foldoutDebug = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutDebug, "Debug")) {

				var showAabbs = EditorGUILayout.Toggle("Show Bounding Boxes", ColliderComponent.ShowAabbs);
				refresh = showAabbs != ColliderComponent.ShowAabbs;
				ColliderComponent.ShowAabbs = showAabbs;

				var showColliders = EditorGUILayout.Toggle("Show Colliders", ColliderComponent.ShowColliderMesh);
				refresh = refresh || showColliders != ColliderComponent.ShowColliderMesh;
				ColliderComponent.ShowColliderMesh = showColliders;
			}
			EditorGUILayout.EndFoldoutHeaderGroup();
			*/


			// individual collider list
			/*
			if (_foldoutColliders = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutColliders, "Colliders")) {

				var hitObjects = ColliderComponent.Colliders ?? new List<ICollider>(0);
				_currentColliders = hitObjects
					.Where(h => h != null)
					.Select((h, i) => $"[{i}] {h.GetType().Name}")
					.ToArray();

				if (_currentColliders.Length == 0) {
					GUILayout.Label("No colliders for this item.");
				}

				_scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.ExpandWidth(true),
					GUILayout.ExpandHeight(true));
				var selectedCollider = GUILayout.SelectionGrid(ColliderComponent.SelectedCollider, _currentColliders, 1);
				refresh = refresh || selectedCollider == ColliderComponent.SelectedCollider;
				ColliderComponent.SelectedCollider = selectedCollider;
				EditorGUILayout.EndScrollView();
			}
			EditorGUILayout.EndFoldoutHeaderGroup();*/

			// refresh scene view manually
			if (refresh) {
				EditorWindow.GetWindow<SceneView>().Repaint();
			}
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
