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
using VisualPinball.Engine.VPT.Gate;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	internal static class GateExtensions
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public static ConvertedItem SetupGameObject(this Gate gate, GameObject obj)
		{
			var mainAuthoring = obj.AddComponent<GateAuthoring>().SetItem(gate);
			var meshAuthoring = new List<IItemMeshAuthoring>();
			GateColliderAuthoring colliderAuthoring = null;

			switch (gate.SubComponent) {
				case ItemSubComponent.None:
					// collider
					colliderAuthoring = obj.AddColliderComponent(gate);

					// bracket mesh
					meshAuthoring.Add(CreateChild<GateBracketMeshAuthoring>(obj, GateMeshGenerator.Bracket));

					// wire mesh
					var wireMeshAuth = CreateChild<GateWireMeshAuthoring>(obj, GateMeshGenerator.Wire);
					wireMeshAuth.gameObject.AddComponent<GateWireAnimationAuthoring>();
					meshAuthoring.Add(wireMeshAuth);
					break;

				case ItemSubComponent.Collider: {
					Logger.Warn("Cannot parent a gate collider to a different object than a gate!");
					break;
				}

				case ItemSubComponent.Mesh: {
					Logger.Warn("Cannot parent a gate mesh to a different object than a gate!");
					break;
				}

				default:
					throw new ArgumentOutOfRangeException();
			}
			obj.AddComponent<ConvertToEntity>();
			return new ConvertedItem(mainAuthoring, meshAuthoring, colliderAuthoring);
		}

		private static GateColliderAuthoring AddColliderComponent(this GameObject obj, Gate gate)
		{
			return gate.Data.IsCollidable ? obj.AddComponent<GateColliderAuthoring>() : null;
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
