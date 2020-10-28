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
using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Primitive;

namespace VisualPinball.Unity
{
	internal static class PrimitiveExtensions
	{
		public static ConvertedItem SetupGameObject(this Primitive primitive, GameObject obj)
		{
			var mainAuthoring = obj.AddComponent<PrimitiveAuthoring>().SetItem(primitive);
			var meshAuthoring = new List<IItemMeshAuthoring>();
			PrimitiveColliderAuthoring colliderAuthoring = null;

			switch (primitive.SubComponent) {
				case ItemSubComponent.None: {
					colliderAuthoring = obj.AddColliderComponent(primitive);
					meshAuthoring.Add(obj.AddComponent<PrimitiveMeshAuthoring>());
					break;
				}

				case ItemSubComponent.Collider: {
					colliderAuthoring = obj.AddColliderComponent(primitive);
					break;
				}

				case ItemSubComponent.Mesh: {
					meshAuthoring.Add(obj.AddComponent<PrimitiveMeshAuthoring>());
					break;
				}

				default:
					throw new ArgumentOutOfRangeException();
			}
			obj.AddComponent<ConvertToEntity>();

			return new ConvertedItem(mainAuthoring, meshAuthoring, colliderAuthoring);
		}

		private static PrimitiveColliderAuthoring AddColliderComponent(this GameObject obj, Primitive primitive)
		{
			// todo handle dynamic collision
			if (!primitive.Data.IsToy && primitive.IsCollidable) {
				return obj.AddComponent<PrimitiveColliderAuthoring>();
			}
			return null;
		}
	}
}
