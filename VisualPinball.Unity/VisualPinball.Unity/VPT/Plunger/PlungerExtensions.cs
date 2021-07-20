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
using VisualPinball.Engine.VPT.Plunger;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	public static class PlungerExtensions
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public static IConvertedItem SetupGameObject(this Plunger plunger, GameObject obj, IMaterialProvider materialProvider, bool componentsAdded)
		{
			var convertedItem = new ConvertedItem<Plunger, PlungerData, PlungerAuthoring>(obj, plunger, componentsAdded);
			switch (plunger.SubComponent) {
				case ItemSubComponent.None:

					convertedItem.SetColliderAuthoring<PlungerColliderAuthoring>(materialProvider, componentsAdded);
					switch (plunger.Data.Type) {
						case PlungerType.PlungerTypeFlat:
							convertedItem.AddMeshAuthoring<PlungerFlatMeshAuthoring>(PlungerMeshGenerator.Flat, componentsAdded);
							break;

						case PlungerType.PlungerTypeCustom:
							convertedItem.AddMeshAuthoring<PlungerSpringMeshAuthoring>(PlungerMeshGenerator.Spring, componentsAdded);
							convertedItem.AddMeshAuthoring<PlungerRodMeshAuthoring>(PlungerMeshGenerator.Rod, componentsAdded);
							break;

						case PlungerType.PlungerTypeModern:
							convertedItem.AddMeshAuthoring<PlungerRodMeshAuthoring>(PlungerMeshGenerator.Rod, componentsAdded);
							break;
					}
					break;

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

			return convertedItem.AddConvertToEntity(componentsAdded);
		}
	}
}
