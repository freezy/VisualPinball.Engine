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
using VisualPinball.Engine.VPT.Ramp;

namespace VisualPinball.Unity
{
	internal static class RampExtensions
	{
		public static IItemMainAuthoring SetupGameObject(this Ramp ramp, GameObject obj, IItemMainAuthoring parentAuthoring)
		{
			var mainAuthoring = obj.AddComponent<RampAuthoring>().SetItem(ramp);

			switch (ramp.SubComponent) {
				case ItemSubComponent.None:
					CreateMeshComponents(ramp, obj);
					obj.AddComponent<RampColliderAuthoring>();
					break;

				case ItemSubComponent.Collider: {
					obj.AddComponent<RampColliderAuthoring>();
					if (parentAuthoring != null && parentAuthoring is IHittableAuthoring hittableAuthoring) {
						hittableAuthoring.RemoveHittableComponent();
					}
					break;
				}

				case ItemSubComponent.Mesh: {
					// todo
					CreateMeshComponents(ramp, obj);
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

		private static void CreateMeshComponents(Ramp ramp, GameObject obj)
		{
			if (ramp.IsHabitrail) {
				CreateChild<RampWireMeshAuthoring>(obj, RampMeshGenerator.Wires);
			} else {
				CreateChild<RampFloorMeshAuthoring>(obj, RampMeshGenerator.Floor);
				CreateChild<RampWallMeshAuthoring>(obj, RampMeshGenerator.Wall);
			}
		}

		public static GameObject CreateChild<T>(GameObject obj, string name) where T : MonoBehaviour, IItemMeshAuthoring
		{
			var subObj = new GameObject(name);
			subObj.transform.SetParent(obj.transform, false);
			subObj.AddComponent<T>();
			//subObj.layer = ChildObjectsLayer;
			return subObj;
		}
	}
}
