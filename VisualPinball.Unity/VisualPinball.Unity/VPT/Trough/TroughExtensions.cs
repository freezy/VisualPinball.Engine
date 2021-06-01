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

using UnityEngine;
using VisualPinball.Engine.VPT.Trough;

namespace VisualPinball.Unity
{
	public static class TroughExtensions
	{
		// public static IConvertedItem SetupGameObject(this Trough surface, GameObject obj, IMaterialProvider materialProvider)
		// {
		// 	var convertedItem = new ConvertedItem<Trough, TroughData, TroughAuthoring>(obj, surface);
		// 	switch (surface.SubComponent) {
		// 		case ItemSubComponent.None:
		// 			convertedItem.SetColliderAuthoring<SurfaceColliderAuthoring>(materialProvider);
		// 			convertedItem.AddMeshAuthoring<SurfaceSideMeshAuthoring>(SurfaceMeshGenerator.Side);
		// 			convertedItem.AddMeshAuthoring<SurfaceTopMeshAuthoring>(SurfaceMeshGenerator.Top);
		// 			break;
		//
		// 		case ItemSubComponent.Collider: {
		// 			convertedItem.SetColliderAuthoring<SurfaceColliderAuthoring>(materialProvider);
		// 			break;
		// 		}
		//
		// 		case ItemSubComponent.Mesh: {
		// 			convertedItem.AddMeshAuthoring<SurfaceSideMeshAuthoring>(SurfaceMeshGenerator.Side);
		// 			convertedItem.AddMeshAuthoring<SurfaceTopMeshAuthoring>(SurfaceMeshGenerator.Top);
		// 			break;
		// 		}
		//
		// 		default:
		// 			throw new ArgumentOutOfRangeException();
		// 	}
		//
		// 	return convertedItem.AddConvertToEntity();
		// }

		public static IConvertedItem SetupGameObject(this Trough trough, GameObject obj)
		{
			var mainAuthoring = obj.AddComponent<TroughAuthoring>();
			mainAuthoring.SetItem(trough);
			mainAuthoring.UpdatePosition();
			return null;
		}
	}
}
