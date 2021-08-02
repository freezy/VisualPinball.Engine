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
using VisualPinball.Engine.VPT.Trigger;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	public static class TriggerExtensions
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public static IConvertedItem InstantiateGameObject(this Trigger trigger, IItem item, IMaterialProvider materialProvider)
		{
			var obj = new GameObject(item.Name);
			var convertedItem = new ConvertedItem<Trigger, TriggerData, TriggerAuthoring>(obj, trigger);
			switch (trigger.SubComponent) {
				case ItemSubComponent.None:
					convertedItem.SetColliderAuthoring<TriggerColliderAuthoring>(materialProvider);
					convertedItem.SetMeshAuthoring<TriggerMeshAuthoring>();
					break;

				case ItemSubComponent.Collider: {
					Logger.Warn("Cannot parent a trigger collider to a different object than a trigger!");
					break;
				}

				case ItemSubComponent.Mesh: {
					Logger.Warn("Cannot parent a trigger mesh to a different object than a trigger!");
					break;
				}
				default:
					throw new ArgumentOutOfRangeException();
			}

			return convertedItem.AddConvertToEntity();
		}
	}
}
