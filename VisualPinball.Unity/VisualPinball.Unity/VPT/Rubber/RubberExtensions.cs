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
using UnityEngine;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Rubber;

namespace VisualPinball.Unity
{
	public static class RubberExtensions
	{
		public static IConvertedItem SetupGameObject(this Rubber rubber, GameObject obj, IMaterialProvider materialProvider)
		{
			var convertedItem = new ConvertedItem<Rubber, RubberData, RubberAuthoring>(obj, rubber);
			switch (rubber.SubComponent) {
				case ItemSubComponent.None:
					convertedItem.SetColliderAuthoring<RubberColliderAuthoring>(materialProvider);
					convertedItem.SetMeshAuthoring<RubberMeshAuthoring>();
					break;

				case ItemSubComponent.Collider: {
					convertedItem.SetColliderAuthoring<RubberColliderAuthoring>(materialProvider);
					break;
				}

				case ItemSubComponent.Mesh: {
					convertedItem.SetMeshAuthoring<RubberMeshAuthoring>();
					break;
				}

				default:
					throw new ArgumentOutOfRangeException();
			}

			return convertedItem.AddConvertToEntity();
		}

		private static RubberColliderAuthoring AddColliderComponent(this GameObject obj, Rubber rubber)
		{
			return rubber.Data.IsCollidable ? obj.AddComponent<RubberColliderAuthoring>() : null;
		}
	}
}
