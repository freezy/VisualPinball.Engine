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
using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity
{
	internal static class SurfaceExtensions
	{
		public static MonoBehaviour SetupGameObject(this Engine.VPT.Surface.Surface surface, GameObject obj,
			RenderObjectGroup rog, MonoBehaviour mainMb)
		{
			MonoBehaviour mb = obj.AddComponent<SurfaceAuthoring>().SetItem(surface);
			switch (rog.SubComponent) {
				case RenderObjectGroup.ItemSubComponent.None:
					obj.AddComponent<SurfaceColliderAuthoring>();
					//obj.AddComponent<SurfaceMeshAuthoring>();
					break;

				case RenderObjectGroup.ItemSubComponent.Collider: {
					obj.AddComponent<SurfaceColliderAuthoring>();
					if (mainMb != null && mainMb is IHittableAuthoring hittableAuthoring) {
						hittableAuthoring.RemoveHittableComponent();
					}
					break;
				}

				case RenderObjectGroup.ItemSubComponent.Mesh: {
					//obj.AddComponent<SurfaceMeshAuthoring>();
					if (mainMb != null && mainMb is IMeshAuthoring meshAuthoring) {
						meshAuthoring.RemoveMeshComponent();
					}
					break;
				}

				default:
					throw new ArgumentOutOfRangeException();
			}
			obj.AddComponent<ConvertToEntity>();
			return mb;
		}
	}
}
