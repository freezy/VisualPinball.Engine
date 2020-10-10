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
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Spinner;

namespace VisualPinball.Unity
{
	internal static class SpinnerExtensions
	{
		public static IItemMainAuthoring SetupGameObject(this Spinner spinner, GameObject obj, IItemMainAuthoring parentAuthoring)
		{
			var mainAuthoring = obj.AddComponent<SpinnerAuthoring>().SetItem(spinner);

			switch (spinner.SubComponent) {
				case ItemSubComponent.None:
					obj.AddComponent<SpinnerColliderAuthoring>();
					CreateChild<SpinnerBracketMeshAuthoring>(obj, SpinnerMeshGenerator.Bracket);
					var wire = CreateChild<SpinnerPlateMeshAuthoring>(obj, SpinnerMeshGenerator.Plate);
					wire.AddComponent<SpinnerPlateAnimationAuthoring>();
					break;

				case ItemSubComponent.Collider: {
					obj.AddComponent<SpinnerColliderAuthoring>();
					if (parentAuthoring != null && parentAuthoring is IHittableAuthoring hittableAuthoring) {
						hittableAuthoring.RemoveHittableComponent();
					}
					break;
				}

				case ItemSubComponent.Mesh: {
					// todo
					if (parentAuthoring != null && parentAuthoring is IMeshAuthoring meshAuthoring) {
						meshAuthoring.RemoveMeshComponent();
					}
					break;
				}

				default:
					throw new ArgumentOutOfRangeException();
			}
			obj.AddComponent<ConvertToEntity>();
			return mainAuthoring;
		}

		private static GameObject CreateChild<T>(GameObject obj, string name) where T : MonoBehaviour, IItemMeshAuthoring
		{
			var subObj = new GameObject(name);
			subObj.transform.SetParent(obj.transform, false);
			subObj.AddComponent<T>();
			//subObj.layer = ChildObjectsLayer;
			return subObj;
		}
	}
}
