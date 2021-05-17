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

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VisualPinball.Unity
{
	public class ConvertedItem
	{
		public readonly IItemMainAuthoring MainAuthoring;
		public IEnumerable<IItemMeshAuthoring> MeshAuthoring;
		public IItemColliderAuthoring ColliderAuthoring;

		public ConvertedItem()
		{
			MainAuthoring = null;
			MeshAuthoring = new IItemMeshAuthoring[0];
			ColliderAuthoring = null;
		}

		public ConvertedItem(IItemMainAuthoring mainAuthoring)
		{
			MainAuthoring = mainAuthoring;
			MeshAuthoring = new IItemMeshAuthoring[0];
			ColliderAuthoring = null;
		}

		public ConvertedItem(IItemMainAuthoring mainAuthoring, IEnumerable<IItemMeshAuthoring> meshAuthoring)
		{
			MainAuthoring = mainAuthoring;
			MeshAuthoring = meshAuthoring;
			ColliderAuthoring = null;
		}

		public ConvertedItem(IItemMainAuthoring mainAuthoring, IEnumerable<IItemMeshAuthoring> meshAuthoring, IItemColliderAuthoring colliderAuthoring)
		{
			MainAuthoring = mainAuthoring;
			MeshAuthoring = meshAuthoring;
			ColliderAuthoring = colliderAuthoring;
		}

		public void Destroy()
		{
			MainAuthoring.Destroy();
		}

		public void DestroyMeshComponent()
		{
			if (MainAuthoring is IItemMainRenderableAuthoring renderableAuthoring) {
				renderableAuthoring.DestroyMeshComponent();
				MeshAuthoring = new IItemMeshAuthoring[0];
			}
		}

		public void DestroyColliderComponent()
		{
			if (MainAuthoring is IItemMainRenderableAuthoring renderableAuthoring) {
				renderableAuthoring.DestroyColliderComponent();
				ColliderAuthoring = null;
			}
		}

		public bool IsValidChild(ConvertedItem parent)
		{
			if (MeshAuthoring.Any()) {
				return MeshAuthoring.First().ValidParents.Contains(parent.MainAuthoring.GetType());
			}

			if (ColliderAuthoring != null) {
				return ColliderAuthoring.ValidParents.Contains(parent.MainAuthoring.GetType());
			}

			return MainAuthoring.ValidParents.Contains(parent.MainAuthoring.GetType());
		}

		public static T CreateChild<T>(GameObject obj, string name) where T : MonoBehaviour, IItemMeshAuthoring
		{
			var subObj = new GameObject(name);
			subObj.transform.SetParent(obj.transform, false);
			var comp = subObj.AddComponent<T>();
			subObj.layer = VpxConverter.ChildObjectsLayer;
			return comp;
		}
	}
}
