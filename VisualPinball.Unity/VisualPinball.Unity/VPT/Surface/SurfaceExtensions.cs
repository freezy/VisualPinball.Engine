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

			MonoBehaviour mb = null;
			switch (rog.SubComponent) {
				case RenderObjectGroup.ItemSubComponent.None:

					mb = obj.AddComponent<SurfaceAuthoring>().SetItem(surface);
					obj.AddComponent<SurfaceColliderAuthoring>().SetMainItem(surface);;
					break;

				case RenderObjectGroup.ItemSubComponent.Collider:
					var ia = obj.AddComponent<SurfaceColliderAuthoring>().SetItem(surface, rog);
					if (mainMb != null && mainMb is IHittableAuthoring hittableAuthoring) {
						hittableAuthoring.RemoveHittableComponent();
						hittableAuthoring.LinkChild(ia);
					}
					break;

				case RenderObjectGroup.ItemSubComponent.Mesh:
					break;

				default:
					throw new ArgumentOutOfRangeException();
			}
			obj.AddComponent<ConvertToEntity>();
			return mb;
		}
	}
}
