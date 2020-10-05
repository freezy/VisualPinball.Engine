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
using VisualPinball.Engine.VPT.Surface;

namespace VisualPinball.Unity
{
	internal static class BumperExtensions
	{

		public static IItemMainAuthoring SetupGameObject(this Bumper bumper, GameObject obj, IItemMainAuthoring parentAuthoring)
		{
			var mainAuthoring = obj.AddComponent<BumperAuthoring>().SetItem(bumper);

			switch (bumper.SubComponent) {
				case ItemSubComponent.None:
					//obj.AddComponent<BumperColliderAuthoring>();
					CreateChild<BumperBaseMeshAuthoring>(obj, BumperMeshGenerator.Base);
					CreateChild<BumperCapMeshAuthoring>(obj, BumperMeshGenerator.Cap);
					var ring = CreateChild<BumperRingMeshAuthoring>(obj, BumperMeshGenerator.Ring);
					var skirt = CreateChild<BumperSkirtMeshAuthoring>(obj, BumperMeshGenerator.Skirt);
					ring.AddComponent<BumperRingAnimationAuthoring>();
					skirt.AddComponent<BumperSkirtAnimationAuthoring>();

					break;

				case ItemSubComponent.Collider: {
					//obj.AddComponent<BumperColliderAuthoring>();
					if (parentAuthoring != null && parentAuthoring is IHittableAuthoring hittableAuthoring) {
						hittableAuthoring.RemoveHittableComponent();
					}
					break;
				}

				case ItemSubComponent.Mesh: {
					// todo
					if (parentAuthoring != null && parentAuthoring is IMeshAuthoring meshAuthoring) {
						meshAuthoring.RemoveMeshComponent();
					}
					break;
				}

				default:
					throw new ArgumentOutOfRangeException();
			}
			obj.AddComponent<ConvertToEntity>();
			return mainAuthoring;
		}

		private static GameObject CreateChild<T>(GameObject obj, string name) where T : MonoBehaviour, IItemMeshAuthoring
		{
			var subObj = new GameObject(name);
			subObj.transform.SetParent(obj.transform, false);
			subObj.AddComponent<T>();
			//subObj.layer = ChildObjectsLayer;
			return subObj;
		}
	}
}
