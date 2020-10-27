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

		public static ConvertedItem SetupGameObject(this Bumper bumper, GameObject obj)
		{
			var mainAuthoring = obj.AddComponent<BumperAuthoring>().SetItem(bumper);
			var meshAuthoring = new List<IItemMeshAuthoring>();
			BumperColliderAuthoring colliderAuthoring = null;

			switch (bumper.SubComponent) {
				case ItemSubComponent.None:
					colliderAuthoring = obj.AddColliderComponent(bumper);
					meshAuthoring.Add(ConvertedItem.CreateChild<BumperBaseMeshAuthoring>(obj, BumperMeshGenerator.Base));
					meshAuthoring.Add(ConvertedItem.CreateChild<BumperCapMeshAuthoring>(obj, BumperMeshGenerator.Cap));

					var ring = ConvertedItem.CreateChild<BumperRingMeshAuthoring>(obj, BumperMeshGenerator.Ring);
					var skirt = ConvertedItem.CreateChild<BumperSkirtMeshAuthoring>(obj, BumperMeshGenerator.Skirt);

					ring.gameObject.AddComponent<BumperRingAnimationAuthoring>();
					skirt.gameObject.AddComponent<BumperSkirtAnimationAuthoring>();

					meshAuthoring.Add(ring);
					meshAuthoring.Add(skirt);
					break;

				case ItemSubComponent.Collider: {
					Logger.Warn("Bumper collider cannot be parented to anything else than bumpers.");
					break;
				}

				case ItemSubComponent.Mesh: {
					Logger.Warn("Bumper mesh cannot be parented to anything else than bumpers.");
					break;
				}

				default:
					throw new ArgumentOutOfRangeException();
			}
			obj.AddComponent<ConvertToEntity>();

			return new ConvertedItem(mainAuthoring, meshAuthoring, colliderAuthoring);
		}

		private static BumperColliderAuthoring AddColliderComponent(this GameObject obj, Bumper bumper)
		{
			return bumper.Data.IsCollidable ? obj.AddComponent<BumperColliderAuthoring>() : null;
		}
	}
}
