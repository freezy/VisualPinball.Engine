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
using VisualPinball.Engine.VPT.Flipper;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	internal static class FlipperExtensions
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public static ConvertedItem SetupGameObject(this Flipper flipper, GameObject obj)
		{
			var mainAuthoring = obj.AddComponent<FlipperAuthoring>().SetItem(flipper);
			var meshAuthoring = new List<IItemMeshAuthoring>();
			FlipperColliderAuthoring colliderAuthoring = null;

			switch (flipper.SubComponent) {
				case ItemSubComponent.None:
					colliderAuthoring = obj.AddComponent<FlipperColliderAuthoring>();

					// if invisible in main component, we skip creation entirely, because we think users won't dynamically toggle visibility.
					if (flipper.Data.IsVisible) {
						meshAuthoring.Add(ConvertedItem.CreateChild<FlipperBaseMeshAuthoring>(obj, FlipperMeshGenerator.Base));
						meshAuthoring.Add(ConvertedItem.CreateChild<FlipperRubberMeshAuthoring>(obj, FlipperMeshGenerator.Rubber));
					}
					break;

				case ItemSubComponent.Collider: {
					Logger.Warn("Cannot parent a flipper collider to a different object than a flipper!");
					break;
				}

				case ItemSubComponent.Mesh: {
					Logger.Warn("Cannot parent a flipper mesh to a different object than a flipper!");
					break;
				}

				default:
					throw new ArgumentOutOfRangeException();
			}
			obj.AddComponent<ConvertToEntity>();
			return new ConvertedItem(mainAuthoring, meshAuthoring, colliderAuthoring);
		}
	}
}
