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

		public static IItemMainAuthoring SetupGameObject(this Flipper flipper, GameObject obj, IItemMainAuthoring parentAuthoring)
		{
			var mainAuthoring = obj.AddComponent<FlipperAuthoring>().SetItem(flipper);

			switch (flipper.SubComponent) {
				case ItemSubComponent.None:
					obj.AddComponent<FlipperColliderAuthoring>();

					// if invisible in main component, we skip creation entirely, because we think users won't dynamically toggle visibility.
					if (flipper.Data.IsVisible) {
						CreateChild<FlipperBaseMeshAuthoring>(obj, FlipperMeshGenerator.Base);
						CreateChild<FlipperRubberMeshAuthoring>(obj, FlipperMeshGenerator.Rubber);
					}
					break;

				case ItemSubComponent.Collider: {
					Logger.Error("Cannot parent a flipper collider to a different object than a flipper!");
					break;
				}

				case ItemSubComponent.Mesh: {
					var baseComp = CreateChild<FlipperBaseMeshAuthoring>(obj, FlipperMeshGenerator.Base);
					var rubberComp = CreateChild<FlipperRubberMeshAuthoring>(obj, FlipperMeshGenerator.Rubber);

					// if invisible in sub component, the mesh is explicitly created, so just disable it if invisible.
					baseComp.enabled = flipper.Data.IsVisible;
					rubberComp.enabled = flipper.Data.IsVisible;

					CreateChild<FlipperRubberMeshAuthoring>(obj, FlipperMeshGenerator.Rubber);
					if (parentAuthoring != null && parentAuthoring is IItemMainAuthoring parentMainAuthoring) {
						parentMainAuthoring.DestroyMeshComponent();
					}
					break;
				}

				default:
					throw new ArgumentOutOfRangeException();
			}
			obj.AddComponent<ConvertToEntity>();
			return mainAuthoring;
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
