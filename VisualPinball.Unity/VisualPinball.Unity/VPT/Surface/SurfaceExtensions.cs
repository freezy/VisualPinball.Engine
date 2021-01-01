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
using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Surface;

namespace VisualPinball.Unity
{
	internal static class SurfaceExtensions
	{
		public static ConvertedItem SetupGameObject(this Surface surface, GameObject obj)
		{
			var mainAuthoring = obj.AddComponent<SurfaceAuthoring>().SetItem(surface);
			var meshAuthoring = new List<IItemMeshAuthoring>();
			SurfaceColliderAuthoring colliderAuthoring = null;

			switch (surface.SubComponent) {
				case ItemSubComponent.None:
					colliderAuthoring = obj.AddColliderComponent(surface);
					meshAuthoring.Add(ConvertedItem.CreateChild<SurfaceSideMeshAuthoring>(obj, SurfaceMeshGenerator.Side));
					meshAuthoring.Add(ConvertedItem.CreateChild<SurfaceTopMeshAuthoring>(obj, SurfaceMeshGenerator.Top));
					break;

				case ItemSubComponent.Collider: {
					colliderAuthoring = obj.AddColliderComponent(surface);
					break;
				}

				case ItemSubComponent.Mesh: {
					meshAuthoring.Add(ConvertedItem.CreateChild<SurfaceSideMeshAuthoring>(obj, SurfaceMeshGenerator.Side));
					meshAuthoring.Add(ConvertedItem.CreateChild<SurfaceTopMeshAuthoring>(obj, SurfaceMeshGenerator.Top));
					break;
				}

				default:
					throw new ArgumentOutOfRangeException();
			}
			obj.AddComponent<ConvertToEntity>();
			return new ConvertedItem(mainAuthoring, meshAuthoring, colliderAuthoring);
		}

		private static SurfaceColliderAuthoring AddColliderComponent(this GameObject obj, Surface surface)
		{
			return surface.Data.IsCollidable ? obj.AddComponent<SurfaceColliderAuthoring>() : null;
		}
	}
}
