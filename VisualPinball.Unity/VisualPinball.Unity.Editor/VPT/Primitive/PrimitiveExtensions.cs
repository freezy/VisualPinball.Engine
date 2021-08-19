﻿// Visual Pinball Engine
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

using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Primitive;

namespace VisualPinball.Unity.Editor
{
	public static class PrimitiveExtensions
	{
		public static IConvertedItem InstantiateGameObject(this Primitive primitive, IItem item, IMaterialProvider materialProvider)
		{

			var prefab = UnityEngine.Resources.Load<GameObject>("Prefabs/Primitive");
			var obj = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
			obj!.name = item.Name;

			return new ConvertedItem<Primitive, PrimitiveData, PrimitiveAuthoring>(obj);

			// return new ConvertedItem<Surface, SurfaceData, SurfaceAuthoring>(obj, true);
			//
			// var obj = new GameObject(item.Name);
			// var convertedItem = new ConvertedItem<Primitive, PrimitiveData, PrimitiveAuthoring>(obj, primitive) {
			// 	IsProceduralMesh = false
			// };
			//
			// switch (primitive.SubComponent) {
			// 	case ItemSubComponent.None:
			// 		convertedItem.SetColliderAuthoring<PrimitiveColliderAuthoring>(materialProvider);
			// 		convertedItem.SetMeshAuthoring<PrimitiveMeshAuthoring>();
			// 		break;
			//
			// 	case ItemSubComponent.Collider: {
			// 		convertedItem.SetColliderAuthoring<PrimitiveColliderAuthoring>(materialProvider);
			// 		break;
			// 	}
			//
			// 	case ItemSubComponent.Mesh: {
			// 		convertedItem.SetMeshAuthoring<PrimitiveMeshAuthoring>();
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
