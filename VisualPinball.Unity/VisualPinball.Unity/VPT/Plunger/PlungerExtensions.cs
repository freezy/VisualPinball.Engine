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

		public static (IItemMainAuthoring, IEnumerable<IItemMeshAuthoring>) SetupGameObject(this Plunger plunger, GameObject obj, IItemMainAuthoring parentAuthoring)
		{
			var meshAuthoring = new List<IItemMeshAuthoring>();
			var mainAuthoring = obj.AddComponent<PlungerAuthoring>().SetItem(plunger);

			switch (plunger.SubComponent) {
				case ItemSubComponent.None: {
					switch (plunger.Data.Type) {
						case PlungerType.PlungerTypeFlat:
							meshAuthoring.Add(CreateChild<PlungerFlatMeshAuthoring>(obj, PlungerMeshGenerator.Flat));
							break;

						case PlungerType.PlungerTypeCustom:
							meshAuthoring.Add(CreateChild<PlungerSpringMeshAuthoring>(obj, PlungerMeshGenerator.Spring));
							meshAuthoring.Add(CreateChild<PlungerRodMeshAuthoring>(obj, PlungerMeshGenerator.Rod));
							break;

						case PlungerType.PlungerTypeModern:
							meshAuthoring.Add(CreateChild<PlungerRodMeshAuthoring>(obj, PlungerMeshGenerator.Rod));
							break;

					}
					obj.AddComponent<PlungerColliderAuthoring>();
					break;
				}

				case ItemSubComponent.Collider: {
					Logger.Error("Cannot parent a plunger collider to a different object than a plunger!");
					break;
				}

				case ItemSubComponent.Mesh: {
					Logger.Error("Cannot parent a plunger mesh to a different object than a plunger!");
					break;
				}

				default:
					throw new ArgumentOutOfRangeException();
			}
			obj.AddComponent<ConvertToEntity>();
			return (mainAuthoring, meshAuthoring);
		}

		public static T CreateChild<T>(GameObject obj, string name) where T : MonoBehaviour, IItemMeshAuthoring
		{
			var subObj = new GameObject(name);
			subObj.transform.SetParent(obj.transform, false);
			var comp = subObj.AddComponent<T>();
			//subObj.layer = ChildObjectsLayer;
			return comp;
		}
	}
}
