// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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
using Light = UnityEngine.Light;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Common interface for light conversion with the various render pipelines
	/// </summary>
	public interface ILightConverter
	{
		/// <summary>
		/// Updates a Unity light based on VPX's light data.
		/// </summary>
		/// <param name="light">Which light to update</param>
		/// <param name="data">VPX light data</param>
		/// <param name="isInsert">If set, light is an insert.</param>
		void UpdateLight(Light light, LightData data, bool isInsert);

		/// <summary>
		/// Sets the color of a given light.
		/// </summary>
		/// <param name="light">Which light to update</param>
		/// <param name="color">New color</param>
		void SetColor(Light light, Color color);

		void SetShadow(Light light, bool enabled, bool isDynamic, float nearPlane = 0.01f);

		void SetRange(Light light, float range);

		/// <summary>
		/// Makes the light an insert's spill: a wide spot, cheaper than a point
		/// light and aimed, sitting under the insert and pointing up through it
		/// at the ball and whatever else is above, with no shadows and no bounce.
		/// A pipeline with more to set, a source radius or volumetrics, overrides
		/// this.
		/// </summary>
		void InsertSpill(Light light)
		{
			light.type = LightType.Spot;
			light.spotAngle = 147f;
			light.innerSpotAngle = 91f;
			light.bounceIntensity = 0f;
			light.shadows = LightShadows.None;
		}

		/// <summary>
		/// Sets the intensity of a given light.
		/// </summary>
		/// <param name="light">Which light to update</param>
		/// <param name="intensityLumen">Intensity in Lumen</param>
		void SetIntensity(Light light, float intensityLumen);

		void SetTemperature(Light light, float temperature);

		/// <summary>
		/// Converts the light to a spotlight.
		/// </summary>
		/// <param name="light">Which light to update</param>
		/// <param name="outer">Outer angle</param>
		/// <param name="innerPercent">Inner angle in percent of outer angle</param>
		void SpotLight(Light light, float outer, float innerPercent);

		void PyramidAngle(Light light, float angle, float aspectRatio);
	}
}
