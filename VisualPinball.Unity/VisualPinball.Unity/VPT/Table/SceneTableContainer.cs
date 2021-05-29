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

using System;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.VPT.Table;
using Material = VisualPinball.Engine.VPT.Material;
using Texture = VisualPinball.Engine.VPT.Texture;

namespace VisualPinball.Unity
{
	public class SceneTableContainer : TableContainer, IDisposable
	{
		public Table Table => _tableAuthoring.Table;

		public override Material GetMaterial(string name)
		{
			throw new NotImplementedException();
		}
		public override Texture GetTexture(string name)
		{
			throw new NotImplementedException();
		}

		private readonly TableAuthoring _tableAuthoring;

		public SceneTableContainer(TableAuthoring ta)
		{
			_tableAuthoring = ta;

#if UNITY_EDITOR
			EditorApplication.hierarchyChanged += OnHierarchyChanged;
#endif
		}

		public void Refresh()
		{
			OnHierarchyChanged();
		}

		private void OnHierarchyChanged()
		{
			WalkChildren(_tableAuthoring.transform);
		}

		private void WalkChildren(Transform node)
		{
			foreach (Transform childTransform in node) {
				RefreshChild(childTransform);
				WalkChildren(childTransform);
			}
		}

		private void RefreshChild(Transform node)
		{
			Debug.Log(node.gameObject.name);
		}

		public void Dispose()
		{
#if UNITY_EDITOR
			EditorApplication.hierarchyChanged -= OnHierarchyChanged;
#endif
		}
	}
}
