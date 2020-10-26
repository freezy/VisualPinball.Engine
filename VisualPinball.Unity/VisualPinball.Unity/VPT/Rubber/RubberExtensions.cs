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
using VisualPinball.Engine.VPT.Rubber;

namespace VisualPinball.Unity
{
	internal static class RubberExtensions
	{
		public static (IItemMainAuthoring, IEnumerable<IItemMeshAuthoring>) SetupGameObject(this Rubber rubber, GameObject obj, IItemMainAuthoring parentAuthoring)
		{
			var meshAuthoring = new List<IItemMeshAuthoring>();
			var mainAuthoring = obj.AddComponent<RubberAuthoring>().SetItem(rubber);

			switch (rubber.SubComponent) {
				case ItemSubComponent.None: {
					obj.AddColliderComponent(rubber);
					meshAuthoring.Add(obj.AddComponent<RubberMeshAuthoring>());
					break;
				}

				case ItemSubComponent.Collider: {
					obj.AddColliderComponent(rubber);
					if (parentAuthoring != null && parentAuthoring is IItemMainAuthoring parentMainAuthoring) {
						parentMainAuthoring.DestroyColliderComponent();
					}
					break;
				}

				case ItemSubComponent.Mesh: {
					meshAuthoring.Add(obj.AddComponent<RubberMeshAuthoring>());
					if (parentAuthoring != null && parentAuthoring is IItemMainAuthoring parentMainAuthoring) {
						parentMainAuthoring.DestroyMeshComponent();
					}
					break;
				}

				default:
					throw new ArgumentOutOfRangeException();
			}

			obj.AddComponent<ConvertToEntity>();
			return (mainAuthoring, meshAuthoring);
		}

		private static void AddColliderComponent(this GameObject obj, Rubber rubber)
		{
			if (rubber.Data.IsCollidable) {
				obj.AddComponent<RubberColliderAuthoring>();
			}
		}
	}
}
