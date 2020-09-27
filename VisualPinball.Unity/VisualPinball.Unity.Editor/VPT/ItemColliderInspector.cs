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

using System;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity.Editor
{
	public class ItemColliderInspector<TItem, TData, TAuthoring, TColliderAuthoring> : ItemInspector
		where TColliderAuthoring : ItemColliderAuthoring<TItem, TData, TAuthoring>
		where TData : ItemData
		where TItem : Item<TData>, IHittable, IRenderable
		where TAuthoring : ItemAuthoring<TItem, TData>
	{
		public TData Data {
			get {
				var mb = target as TColliderAuthoring;
				return mb == null ? null : mb.Data;
			}
		}

		private bool _foldoutSceneView = true;

		protected override void OnEnable()
		{
			if (target is TColliderAuthoring mb) {
				mb.ShowGizmos = true;
			}
			base.OnEnable();
		}

		private void OnDestroy()
		{
			if (target is TColliderAuthoring mb) {
				mb.ShowGizmos = false;
			}
		}

		public override void OnInspectorGUI()
		{
			var mb = target as TColliderAuthoring;
			if (mb == null) {
				return;
			}

			if (_foldoutSceneView = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutSceneView, "Scene View")) {

				var showAabbs = EditorGUILayout.Toggle("Show Bounding Boxes", mb.ShowAabbs);
				var refresh = showAabbs == mb.ShowAabbs;
				mb.ShowAabbs = showAabbs;

				var showColliders = EditorGUILayout.Toggle("Show Colliders", mb.ShowColliderMesh);
				refresh = refresh || showColliders == mb.ShowColliderMesh;
				mb.ShowColliderMesh = showColliders;

				if (refresh) {
					EditorWindow.GetWindow<SceneView>().Repaint();
				}
			}
			EditorGUILayout.EndFoldoutHeaderGroup();
		}

		protected void NoDataPanel()
		{
			// todo add more details
			GUILayout.Label("No data! Parent missing?");
		}
	}
}
