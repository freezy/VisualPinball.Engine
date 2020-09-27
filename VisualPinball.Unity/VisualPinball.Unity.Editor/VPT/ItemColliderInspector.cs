// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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
	public class ItemColliderInspector<TItem, TData, TAuthoring, TColliderAuthoring> : ItemInspector
		where TColliderAuthoring : ItemColliderAuthoring<TItem, TData, TAuthoring>
		where TData : ItemData
		where TItem : Item<TData>, IHittable, IRenderable
		where TAuthoring : ItemAuthoring<TItem, TData>
	{
		private TColliderAuthoring _colliderAuthoring;

		protected TData Data => _colliderAuthoring == null ? null : _colliderAuthoring.Data;

		private bool _foldoutSceneView = true;
		private bool _foldoutColliders;
		private string[] _currentColliders;
		private Vector2 _scrollPos;

		protected override void OnEnable()
		{
			_colliderAuthoring = target as TColliderAuthoring;
			if (_colliderAuthoring != null) {
				_colliderAuthoring.ShowGizmos = true;
			}
			base.OnEnable();
		}

		private void OnDestroy()
		{
			if (_colliderAuthoring != null) {
				_colliderAuthoring.ShowGizmos = false;
			}
		}

		public override void OnInspectorGUI()
		{
			if (_colliderAuthoring == null) {
				return;
			}

			var refresh = false;

			// scene view toggles
			if (_foldoutSceneView = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutSceneView, "Scene View")) {

				var showAabbs = EditorGUILayout.Toggle("Show Bounding Boxes", _colliderAuthoring.ShowAabbs);
				refresh = showAabbs == _colliderAuthoring.ShowAabbs;
				_colliderAuthoring.ShowAabbs = showAabbs;

				var showColliders = EditorGUILayout.Toggle("Show Colliders", _colliderAuthoring.ShowColliderMesh);
				refresh = refresh || showColliders == _colliderAuthoring.ShowColliderMesh;
				_colliderAuthoring.ShowColliderMesh = showColliders;
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			// individual collider list
			if (_foldoutColliders = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutColliders, "Colliders")) {

				var hitObjects = _colliderAuthoring.HitObjects ?? new HitObject[0];
				_currentColliders = hitObjects
					.Where(h => h != null)
					.Select((h, i) => $"[{i}] {h.GetType().Name} ({h.ObjType})")
					.ToArray();

				if (_currentColliders.Length == 0) {
					GUILayout.Label("No colliders for this item.");
				}

				_scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.ExpandWidth(true),
					GUILayout.ExpandHeight(true));
				var selectedCollider = GUILayout.SelectionGrid(_colliderAuthoring.SelectedCollider, _currentColliders, 1);
				refresh = refresh || selectedCollider == _colliderAuthoring.SelectedCollider;
				_colliderAuthoring.SelectedCollider = selectedCollider;
				EditorGUILayout.EndScrollView();
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			// refresh scene view manually
			if (refresh) {
				EditorWindow.GetWindow<SceneView>().Repaint();
			}
		}

		protected void NoDataPanel()
		{
			// todo add more details
			GUILayout.Label("No data! Parent missing?");
		}
	}
}
