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
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Surface;

namespace VisualPinball.Unity
{
	public static class SurfaceExtensions
	{
		public static IConvertedItem InstantiateGameObject(this Surface surface, IItem item, IMaterialProvider materialProvider)
		{
			var prefab = UnityEngine.Resources.Load<GameObject>("Prefabs/Surface");
			var obj = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
			obj!.name = item.Name;

			return new ConvertedItem<Surface, SurfaceData, SurfaceAuthoring>(obj);

			// var convertedItem = new ConvertedItem<Surface, SurfaceData, SurfaceAuthoring>(obj, surface);
			// switch (surface.SubComponent) {
			// 	case ItemSubComponent.None:
			// 		convertedItem.SetColliderAuthoring<SurfaceColliderAuthoring>(materialProvider);
			// 		convertedItem.AddMeshAuthoring<SurfaceSideMeshAuthoring>(SurfaceMeshGenerator.Side);
			// 		convertedItem.AddMeshAuthoring<SurfaceTopMeshAuthoring>(SurfaceMeshGenerator.Top);
			// 		break;
			//
			// 	case ItemSubComponent.Collider: {
			// 		convertedItem.SetColliderAuthoring<SurfaceColliderAuthoring>(materialProvider);
			// 		break;
			// 	}
			//
			// 	case ItemSubComponent.Mesh: {
			// 		convertedItem.AddMeshAuthoring<SurfaceSideMeshAuthoring>(SurfaceMeshGenerator.Side);
			// 		convertedItem.AddMeshAuthoring<SurfaceTopMeshAuthoring>(SurfaceMeshGenerator.Top);
			// 		break;
			// 	}
			//
			// 	default:
			// 		throw new ArgumentOutOfRangeException();
			// }
			//
			// return convertedItem.AddConvertToEntity();
		}
	}
}
