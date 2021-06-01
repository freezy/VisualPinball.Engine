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
using VisualPinball.Engine.VPT.Light;
using Light = VisualPinball.Engine.VPT.Light.Light;

namespace VisualPinball.Unity
{
	public static class LightExtensions
	{
		public static IConvertedItem SetupGameObject(this Light light, GameObject obj)
		{
			var convertedItem = new ConvertedItem<Light, LightData, LightAuthoring>(obj, light);

			if (!light.Data.ShowBulbMesh) {
				return convertedItem;
			}

			convertedItem.AddMeshAuthoring<LightBulbMeshAuthoring>(LightMeshGenerator.Bulb);
			convertedItem.AddMeshAuthoring<LightSocketMeshAuthoring>(LightMeshGenerator.Socket);

			return convertedItem;
		}
	}
}
