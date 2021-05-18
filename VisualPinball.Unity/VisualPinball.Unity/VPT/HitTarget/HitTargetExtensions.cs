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
using VisualPinball.Engine.VPT.HitTarget;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	public static class HitTargetExtensions
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public static ConvertedItem SetupGameObject(this HitTarget hitTarget, GameObject obj)
		{
			var mainAuthoring = obj.AddComponent<HitTargetAuthoring>().SetItem(hitTarget);
			var meshAuthoring = new List<IItemMeshAuthoring>();
			HitTargetColliderAuthoring colliderAuthoring = null;

			switch (hitTarget.SubComponent) {
				case ItemSubComponent.None:
					colliderAuthoring = obj.AddColliderComponent(hitTarget);
					meshAuthoring.Add(obj.AddComponent<HitTargetMeshAuthoring>());
					break;

				case ItemSubComponent.Collider: {
					Logger.Warn("Cannot parent a target collider to a different object than a target!");
					break;
				}

				case ItemSubComponent.Mesh: {
					Logger.Warn("Cannot parent a target mesh to a different object than a target!");
					break;
				}

				default:
					throw new ArgumentOutOfRangeException();
			}
			obj.AddComponent<ConvertToEntity>();

			return new ConvertedItem(mainAuthoring, meshAuthoring, colliderAuthoring);
		}

		private static HitTargetColliderAuthoring AddColliderComponent(this GameObject obj, HitTarget hitTarget)
		{
			return hitTarget.Data.IsCollidable ? obj.AddComponent<HitTargetColliderAuthoring>() : null;
		}
	}
}
