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

// ReSharper disable AssignmentInConditionalExpression

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity.Editor
{
	public class ColliderInspector<TData, TMainComponent, TColliderComponent> : ItemInspector
		where TColliderComponent : ColliderComponent<TData, TMainComponent>
		where TData : ItemData
		where TMainComponent : MainRenderableComponent<TData>
	{
		protected TColliderComponent ColliderComponent;

		private bool _foldoutDebug = true;
		private bool _foldoutColliders;
		private string[] _currentColliders;
		private Vector2 _scrollPos;

		protected override MonoBehaviour UndoTarget => ColliderComponent.MainComponent;

		private bool HasMainComponent => ColliderComponent == null || !ColliderComponent.HasMainComponent;


		protected override void OnEnable()
		{
			ColliderComponent = target as TColliderComponent;
			if (ColliderComponent != null) {
				ColliderComponent.ShowGizmos = true;

				// if no meshes active, show collider
				if (ColliderComponent.MainComponent && !ColliderComponent.MainComponent.GetComponentsInChildren<MeshRenderer>().Any(mr => mr.enabled)) {
					ColliderComponent.ShowColliderMesh = true;
				}
			}

			base.OnEnable();
		}

		private void OnDestroy()
		{
			if (ColliderComponent != null) {
				ColliderComponent.ShowGizmos = false;
			}
		}

		public override void OnInspectorGUI()
		{
			if (ColliderComponent == null) {
				return;
			}

			var refresh = false;

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
			if (!HasMainComponent) {
				NoDataError();
				return true;
			}

			if (!ColliderComponent.IsCorrectlyParented) {
				InvalidParentError();
				return true;
			}

			return false;
		}

		private static void NoDataError()
		{
			EditorGUILayout.HelpBox($"Cannot find main component!\n\nYou must have a {typeof(TMainComponent).Name} component on this GameObject.", MessageType.Error);
		}

		private void InvalidParentError()
		{
			var validParentTypes = ColliderComponent.ValidParents.ToArray();
			var typeMessage = validParentTypes.Length > 0
				? $"Supported parents are: [ {string.Join(", ", validParentTypes.Select(t => t.Name))} ]."
				: $"In this case, colliders for {ColliderComponent.ItemName} don't support any parenting at all.";
			EditorGUILayout.HelpBox($"Invalid parent. This {ColliderComponent.ItemName} is parented to a {ColliderComponent.ParentComponent.ItemName}, which VPE doesn't support.\n{typeMessage}", MessageType.Error);
			if (GUILayout.Button("Open Documentation", EditorStyles.linkLabel)) {
				Application.OpenURL("https://docs.visualpinball.org/creators-guide/editor/unity-components.html");
			}
		}
	}
}
