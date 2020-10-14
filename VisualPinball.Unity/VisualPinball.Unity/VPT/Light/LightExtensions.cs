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

using UnityEngine;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Light;
using Light = VisualPinball.Engine.VPT.Light.Light;

namespace VisualPinball.Unity
{
	internal static class LightExtensions
	{
		public static IItemMainAuthoring SetupGameObject(this Light light, GameObject obj, IItemMainAuthoring parentAuthoring)
		{
			var mainAuthoring = obj.AddComponent<LightAuthoring>().SetItem(light);
			if (!light.Data.ShowBulbMesh) {
				return mainAuthoring;
			}

			CreateChild<LightBulbMeshAuthoring>(obj, LightMeshGenerator.Bulb);
			CreateChild<LightSocketMeshAuthoring>(obj, LightMeshGenerator.Socket);

			if (light.SubComponent == ItemSubComponent.Mesh) {
				if (parentAuthoring != null && parentAuthoring is IMeshAuthoring meshAuthoring) {
					meshAuthoring.RemoveMeshComponent();
				}
			}

			//obj.AddComponent<ConvertToEntity>();
			return mainAuthoring;
		}

		public static GameObject CreateChild<T>(GameObject obj, string name) where T : MonoBehaviour, IItemMeshAuthoring
		{
			var subObj = new GameObject(name);
			subObj.transform.SetParent(obj.transform, false);
			subObj.AddComponent<T>();
			//subObj.layer = ChildObjectsLayer;
			return subObj;
		}
	}
}
