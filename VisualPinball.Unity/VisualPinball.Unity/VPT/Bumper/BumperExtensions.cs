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

using System;
using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Bumper;

namespace VisualPinball.Unity
{
	internal static class BumperExtensions
	{
		public static IItemMainAuthoring SetupGameObject(this Bumper bumper, GameObject obj, IItemMainAuthoring parentAuthoring)
		{
			var mainAuthoring = obj.AddComponent<BumperAuthoring>().SetItem(bumper);

			switch (bumper.SubComponent) {
				case ItemSubComponent.None:
					obj.AddComponent<BumperColliderAuthoring>();

					CreateBaseMeshComponent(bumper, obj);
					CreateCapMeshComponent(bumper, obj);

					var ring = CreateRingMeshComponent(bumper, obj);
					var skirt = CreateSkirtMeshComponent(bumper, obj);
					ring.AddComponent<BumperRingAnimationAuthoring>();
					skirt.AddComponent<BumperSkirtAnimationAuthoring>();

					break;

				case ItemSubComponent.Collider: {
					obj.AddComponent<BumperColliderAuthoring>();
					if (parentAuthoring != null && parentAuthoring is IItemMainAuthoring parentMainAuthoring) {
						parentMainAuthoring.DestroyColliderComponent();
					}
					break;
				}

				case ItemSubComponent.Mesh: {
					CreateBaseMeshComponent(bumper, obj);
					CreateCapMeshComponent(bumper, obj);
					CreateRingMeshComponent(bumper, obj);
					CreateSkirtMeshComponent(bumper, obj);
					if (parentAuthoring != null && parentAuthoring is IItemMainAuthoring parentMainAuthoring) {
						parentMainAuthoring.DestroyMeshComponent();
					}
					break;
				}

				default:
					throw new ArgumentOutOfRangeException();
			}
			obj.AddComponent<ConvertToEntity>();
			return mainAuthoring;
		}

		private static void CreateBaseMeshComponent(Bumper rubber, GameObject obj)
		{
			var meshComp = CreateChild<BumperBaseMeshAuthoring>(obj, BumperMeshGenerator.Base);
			meshComp.enabled = rubber.Data.IsBaseVisible;
		}

		private static void CreateCapMeshComponent(Bumper rubber, GameObject obj)
		{
			var meshComp = CreateChild<BumperCapMeshAuthoring>(obj, BumperMeshGenerator.Cap);
			meshComp.enabled = rubber.Data.IsBaseVisible;
		}

		private static GameObject CreateRingMeshComponent(Bumper rubber, GameObject obj)
		{
			var meshComp = CreateChild<BumperRingMeshAuthoring>(obj, BumperMeshGenerator.Ring);
			meshComp.enabled = rubber.Data.IsRingVisible;
			return meshComp.gameObject;
		}

		private static GameObject CreateSkirtMeshComponent(Bumper rubber, GameObject obj)
		{
			var meshComp = CreateChild<BumperSkirtMeshAuthoring>(obj, BumperMeshGenerator.Skirt);
			meshComp.enabled = rubber.Data.IsSocketVisible;
			return meshComp.gameObject;
		}

		private static T CreateChild<T>(GameObject obj, string name) where T : MonoBehaviour, IItemMeshAuthoring
		{
			var subObj = new GameObject(name);
			subObj.transform.SetParent(obj.transform, false);
			var comp = subObj.AddComponent<T>();
			//subObj.layer = ChildObjectsLayer;
			return comp;
		}
	}
}
