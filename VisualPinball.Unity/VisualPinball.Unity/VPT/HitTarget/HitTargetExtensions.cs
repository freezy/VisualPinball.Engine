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
using NLog;
using UnityEngine;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.HitTarget;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	public static class HitTargetExtensions
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public static IConvertedItem InstantiateGameObject(this HitTarget hitTarget, IItem item, IMaterialProvider materialProvider)
		{
			var obj = new GameObject(item.Name);
			var convertedItem = new ConvertedItem<HitTarget, HitTargetData, HitTargetAuthoring>(obj, hitTarget);
			switch (hitTarget.SubComponent) {
				case ItemSubComponent.None:
					convertedItem.SetColliderAuthoring<HitTargetColliderAuthoring>(materialProvider);
					//convertedItem.SetMeshAuthoring<HitTargetMeshAuthoring>();
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

			return convertedItem.AddConvertToEntity();
		}
	}
}
