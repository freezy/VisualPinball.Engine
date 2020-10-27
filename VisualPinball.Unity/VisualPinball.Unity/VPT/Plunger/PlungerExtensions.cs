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
using VisualPinball.Engine.VPT.Plunger;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	internal static class PlungerExtensions
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public static ConvertedItem SetupGameObject(this Plunger plunger, GameObject obj)
		{
			var mainAuthoring = obj.AddComponent<PlungerAuthoring>().SetItem(plunger);
			var meshAuthoring = new List<IItemMeshAuthoring>();
			PlungerColliderAuthoring colliderAuthoring = null;

			switch (plunger.SubComponent) {
				case ItemSubComponent.None: {
					colliderAuthoring = obj.AddComponent<PlungerColliderAuthoring>();
					switch (plunger.Data.Type) {
						case PlungerType.PlungerTypeFlat:
							meshAuthoring.Add(ConvertedItem.CreateChild<PlungerFlatMeshAuthoring>(obj, PlungerMeshGenerator.Flat));
							break;

						case PlungerType.PlungerTypeCustom:
							meshAuthoring.Add(ConvertedItem.CreateChild<PlungerSpringMeshAuthoring>(obj, PlungerMeshGenerator.Spring));
							meshAuthoring.Add(ConvertedItem.CreateChild<PlungerRodMeshAuthoring>(obj, PlungerMeshGenerator.Rod));
							break;

						case PlungerType.PlungerTypeModern:
							meshAuthoring.Add(ConvertedItem.CreateChild<PlungerRodMeshAuthoring>(obj, PlungerMeshGenerator.Rod));
							break;

					}
					break;
				}

				case ItemSubComponent.Collider: {
					Logger.Warn("Cannot parent a plunger collider to a different object than a plunger!");
					break;
				}

				case ItemSubComponent.Mesh: {
					Logger.Warn("Cannot parent a plunger mesh to a different object than a plunger!");
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
