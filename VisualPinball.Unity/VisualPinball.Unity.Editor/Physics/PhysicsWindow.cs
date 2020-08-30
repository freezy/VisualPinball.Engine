﻿using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Unity.Editor.Physics
{
	public class PhysicsWindow : EditorWindow
	{
		public static PhysicsWindow Window;

		private Vector2 _scrollPos;

		private IHittableAuthoring _currentHittable;
		private string[] _currentColliders;

		[MenuItem("Visual Pinball/Physics Debug", false, 105)]
		public static void ShowWindow()
		{
			Window = GetWindow<PhysicsWindow>("Physics Debug");
			Window.Show();
		}

		private void OnEnable()
		{
			PhysicsDebug.ShowAabbs = true;
			PhysicsDebug.ItemSelected += OnItemSelected;
		}


		private void OnDisable()
		{
			PhysicsDebug.ShowAabbs = false;
			PhysicsDebug.ItemSelected -= OnItemSelected;
		}

		private void OnItemSelected(object sender, EventArgs e)
		{
			Repaint();
		}

		private void OnGUI()
		{

			GUILayout.BeginHorizontal();

			var showAabbs = EditorGUILayout.Toggle("Show Bounding Boxes", PhysicsDebug.ShowAabbs);
			var refresh = showAabbs == PhysicsDebug.ShowAabbs;
			PhysicsDebug.ShowAabbs = showAabbs;

			var showColliders = EditorGUILayout.Toggle("Show Colliders", PhysicsDebug.ShowColliders);
			refresh = refresh || showColliders == PhysicsDebug.ShowColliders;
			PhysicsDebug.ShowColliders = showColliders;

			GUILayout.EndHorizontal();
			GUILayout.Space(4);

			var selectedObject = Selection.activeObject as GameObject;
			var hittableObj = selectedObject != null ? selectedObject.GetComponent<IHittableAuthoring>() : null;
			if (hittableObj != null) {

				var headerStyle = new GUIStyle(EditorStyles.largeLabel) {
					fontStyle = FontStyle.Bold
				};
				GUILayout.Label(selectedObject.name, headerStyle);
				GUILayout.Space(2f);

				if (_currentHittable != hittableObj) {
					var hitObjects = hittableObj.Hittable.GetHitShapes() ?? new HitObject[0];
					_currentColliders = hitObjects
						.Where(h => h != null)
						.Select((h, i) => $"[{i}] {h.GetType().Name}")
						.ToArray();

					if (_currentColliders.Length == 0) {
						GUILayout.Label("No colliders for this item.");
					}
				}

				_currentHittable = hittableObj;
				_scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
				var selectedCollider = GUILayout.SelectionGrid(PhysicsDebug.SelectedCollider, _currentColliders, 1);
				refresh = refresh || selectedCollider == PhysicsDebug.SelectedCollider;
				PhysicsDebug.SelectedCollider = selectedCollider;
				EditorGUILayout.EndScrollView();

				if (refresh) {
					EditorWindow view = GetWindow<SceneView>();
					view.Repaint();
				}

			} else {
				PhysicsDebug.SelectedCollider = -1;
				GUILayout.Label("No collidable element selected.");
			}
		}
	}
}
