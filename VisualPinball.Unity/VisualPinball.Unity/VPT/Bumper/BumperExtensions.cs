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
using System.Collections.Generic;
using NLog;
using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Bumper;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	internal static class BumperExtensions
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public static (IItemMainAuthoring, IEnumerable<IItemMeshAuthoring>) SetupGameObject(this Bumper bumper, GameObject obj, IItemMainAuthoring parentAuthoring)
		{
			var meshAuthoring = new List<IItemMeshAuthoring>();
			var mainAuthoring = obj.AddComponent<BumperAuthoring>().SetItem(bumper);

			switch (bumper.SubComponent) {
				case ItemSubComponent.None:
					obj.AddColliderComponent(bumper);
					meshAuthoring.Add(CreateChild<BumperBaseMeshAuthoring>(obj, BumperMeshGenerator.Base));
					meshAuthoring.Add(CreateChild<BumperCapMeshAuthoring>(obj, BumperMeshGenerator.Cap));

					var ring = CreateChild<BumperRingMeshAuthoring>(obj, BumperMeshGenerator.Ring);
					var skirt = CreateChild<BumperSkirtMeshAuthoring>(obj, BumperMeshGenerator.Skirt);

					ring.gameObject.AddComponent<BumperRingAnimationAuthoring>();
					skirt.gameObject.AddComponent<BumperSkirtAnimationAuthoring>();

					meshAuthoring.Add(ring);
					meshAuthoring.Add(skirt);
					break;

				case ItemSubComponent.Collider: {
					Logger.Error("Bumper collider cannot be parented to anything else than bumpers.");
					break;
				}

				case ItemSubComponent.Mesh: {
					Logger.Error("Bumper mesh cannot be parented to anything else than bumpers.");
					break;
				}

				default:
					throw new ArgumentOutOfRangeException();
			}
			obj.AddComponent<ConvertToEntity>();

			return (mainAuthoring, meshAuthoring);
		}

		private static void AddColliderComponent(this GameObject obj, Bumper bumper)
		{
			if (bumper.Data.IsCollidable) {
				obj.AddComponent<BumperColliderAuthoring>();
			}
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
