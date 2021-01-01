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

using System.Linq;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Physics;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity.Editor
{
	public class ItemColliderInspector<TItem, TData, TMainAuthoring, TColliderAuthoring> : ItemInspector
		where TColliderAuthoring : ItemColliderAuthoring<TItem, TData, TMainAuthoring>
		where TData : ItemData
		where TItem : Item<TData>, IHittable, IRenderable
		where TMainAuthoring : ItemMainRenderableAuthoring<TItem, TData>
	{
		protected TColliderAuthoring ColliderAuthoring;

		protected TData Data => ColliderAuthoring == null ? null : ColliderAuthoring.Data;

		private bool _foldoutDebug;
		private bool _foldoutColliders;
		private string[] _currentColliders;
		private Vector2 _scrollPos;

		public override MonoBehaviour UndoTarget => ColliderAuthoring.MainAuthoring as MonoBehaviour;

		protected override void OnEnable()
		{
			ColliderAuthoring = target as TColliderAuthoring;
			if (ColliderAuthoring != null) {
				ColliderAuthoring.ShowGizmos = true;
			}
			base.OnEnable();
		}

		private void OnDestroy()
		{
			if (ColliderAuthoring != null) {
				ColliderAuthoring.ShowGizmos = false;
			}
		}

		public override void OnInspectorGUI()
		{
			if (ColliderAuthoring == null) {
				return;
			}

			var refresh = false;

			// scene view toggles
			if (_foldoutDebug = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutDebug, "Debug")) {

				var showAabbs = EditorGUILayout.Toggle("Show Bounding Boxes", ColliderAuthoring.ShowAabbs);
				refresh = showAabbs != ColliderAuthoring.ShowAabbs;
				ColliderAuthoring.ShowAabbs = showAabbs;

				var showColliders = EditorGUILayout.Toggle("Show Colliders", ColliderAuthoring.ShowColliderMesh);
				refresh = refresh || showColliders != ColliderAuthoring.ShowColliderMesh;
				ColliderAuthoring.ShowColliderMesh = showColliders;
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			// individual collider list
			if (_foldoutColliders = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutColliders, "Colliders")) {

				var hitObjects = ColliderAuthoring.HitObjects ?? new HitObject[0];
				_currentColliders = hitObjects
					.Where(h => h != null)
					.Select((h, i) => $"[{i}] {h.GetType().Name} ({h.ObjType})")
					.ToArray();

				if (_currentColliders.Length == 0) {
					GUILayout.Label("No colliders for this item.");
				}

				_scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.ExpandWidth(true),
					GUILayout.ExpandHeight(true));
				var selectedCollider = GUILayout.SelectionGrid(ColliderAuthoring.SelectedCollider, _currentColliders, 1);
				refresh = refresh || selectedCollider == ColliderAuthoring.SelectedCollider;
				ColliderAuthoring.SelectedCollider = selectedCollider;
				EditorGUILayout.EndScrollView();
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			// refresh scene view manually
			if (refresh) {
				EditorWindow.GetWindow<SceneView>().Repaint();
			}
		}

		protected bool HasErrors()
		{
			if (Data == null) {
				NoDataError();
				return true;
			}

			if (!ColliderAuthoring.IsCorrectlyParented) {
				InvalidParentError();
				return true;
			}

			return false;
		}

		private void NoDataError()
		{
			EditorGUILayout.HelpBox($"Cannot find main component!\n\nYou must have a {typeof(TMainAuthoring).Name} component on either this GameObject, its parent or grand parent.", MessageType.Error);
		}

		private void InvalidParentError()
		{
			var validParentTypes = ColliderAuthoring.ValidParents.ToArray();
			var typeMessage = validParentTypes.Length > 0
				? $"Supported parents are: [ {string.Join(", ", validParentTypes.Select(t => t.Name))} ]."
				: $"In this case, colliders for {ColliderAuthoring.Item.ItemName} don't support any parenting at all.";
			EditorGUILayout.HelpBox($"Invalid parent. This {ColliderAuthoring.Item.ItemName} is parented to a {ColliderAuthoring.ParentAuthoring.IItem.ItemName}, which VPE doesn't support.\n{typeMessage}", MessageType.Error);
			if (GUILayout.Button("Open Documentation", EditorStyles.linkLabel)) {
				Application.OpenURL("https://docs.visualpinball.org/creators-guide/editor/unity-components.html");
			}
		}
	}
}
