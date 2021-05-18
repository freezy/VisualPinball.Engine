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
using System.Collections.Generic;
using NLog;
using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Ramp;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	public static class RampExtensions
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public static ConvertedItem SetupGameObject(this Ramp ramp, GameObject obj)
		{
			var mainAuthoring = obj.AddComponent<RampAuthoring>().SetItem(ramp);
			var meshAuthoring = new List<IItemMeshAuthoring>();
			RampColliderAuthoring colliderAuthoring = null;

			switch (ramp.SubComponent) {
				case ItemSubComponent.None:
					colliderAuthoring = obj.AddColliderComponent(ramp);
					if (ramp.IsHabitrail) {
						meshAuthoring.Add(ConvertedItem.CreateChild<RampWireMeshAuthoring>(obj, RampMeshGenerator.Wires));
					} else {
						meshAuthoring.Add(ConvertedItem.CreateChild<RampFloorMeshAuthoring>(obj, RampMeshGenerator.Floor));
						meshAuthoring.Add(ConvertedItem.CreateChild<RampWallMeshAuthoring>(obj, RampMeshGenerator.Wall));
					}
					break;

				case ItemSubComponent.Collider: {
					colliderAuthoring = obj.AddColliderComponent(ramp);
					break;
				}

				case ItemSubComponent.Mesh: {
					Logger.Warn("Cannot parent a ramp mesh to a different object than a ramp!");
					break;
				}

				default:
					throw new ArgumentOutOfRangeException();
			}
			obj.AddComponent<ConvertToEntity>();
			return new ConvertedItem(mainAuthoring, meshAuthoring, colliderAuthoring);
		}

		private static RampColliderAuthoring AddColliderComponent(this GameObject obj, Ramp ramp)
		{
			return ramp.Data.IsCollidable ? obj.AddComponent<RampColliderAuthoring>() : null;
		}
	}
}
