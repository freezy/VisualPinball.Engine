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

// ReSharper disable UnusedType.Global
// ReSharper disable CheckNamespace

using UnityEngine;
using VisualPinball.Engine.VPT.Light;
using Light = UnityEngine.Light;

namespace VisualPinball.Unity
{
	public class StandardLightConverter : ILightConverter
	{
		public void UpdateLight(Light light, LightData data)
		{
			// Set color and position
			light.color = data.Color2.ToUnityColor();
			light.intensity = data.Intensity / 2f;
			light.range = data.Falloff * 0.001f;
			// TODO: vpe specific data for height
			light.transform.localPosition = new Vector3(0f, 0f, 25f);

			// TODO: vpe specific shadow settings
			light.shadows = LightShadows.None;
			light.shadowBias = 0f;
			light.shadowNearPlane = 0f;
		}
	}
}
