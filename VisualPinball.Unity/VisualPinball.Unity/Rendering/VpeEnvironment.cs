// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
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

namespace VisualPinball.Unity
{
	/// <summary>
	/// A light around the table (a room or studio light), in physical units, as the host describes it to the
	/// active render pipeline.
	/// </summary>
	public struct VpeEnvironmentLight
	{
		/// <summary>A directional light (intensity in lux) instead of a spot (intensity in candela).</summary>
		public bool Directional;
		public float Intensity;
		public Color Color;
		/// <summary>The spot's outer cone angle in degrees.</summary>
		public float SpotAngle;
		/// <summary>The spot's inner cone, in percent of the outer one: the smaller, the softer its edge.</summary>
		public float InnerSpotPercent;
		/// <summary>How far the spot reaches, in meters.</summary>
		public float Range;
		public bool Shadows;
	}

	/// <summary>Implemented by the active render pipeline to light the table's surroundings.</summary>
	public interface IVpeEnvironmentApplier
	{
		/// <summary>Scales the environment map (what it shows, lights and reflects) against how the scene set it up.</summary>
		void SetSkyIntensity(float multiplier);

		/// <summary>Sets a light up as <paramref name="spec"/> describes it, in the pipeline's own units.</summary>
		void ConfigureLight(Light light, in VpeEnvironmentLight spec);
	}

	/// <summary>
	/// Bridges the host's environment lighting to the render pipeline, the same pattern as
	/// <see cref="VpeGraphics"/>: the SRP assembly registers its applier before any scene loads, so the host
	/// never references pipeline types.
	/// </summary>
	public static class VpeEnvironment
	{
		private static IVpeEnvironmentApplier _applier;

		public static bool HasApplier => _applier != null;

		public static void Register(IVpeEnvironmentApplier applier) => _applier = applier;

		/// <summary>Scales the environment map against how the scene set it up (1 = as set up).</summary>
		public static void SetSkyIntensity(float multiplier) => _applier?.SetSkyIntensity(Mathf.Max(0f, multiplier));

		/// <summary>
		/// Sets a light up as <paramref name="spec"/> describes it. Without a pipeline applier the values go
		/// onto the light as they are.
		/// </summary>
		public static void ConfigureLight(Light light, in VpeEnvironmentLight spec)
		{
			if (light == null) {
				return;
			}
			if (_applier != null) {
				_applier.ConfigureLight(light, spec);
				return;
			}
			light.type = spec.Directional ? LightType.Directional : LightType.Spot;
			light.color = spec.Color;
			light.intensity = spec.Intensity;
			light.shadows = spec.Shadows ? LightShadows.Soft : LightShadows.None;
			if (!spec.Directional) {
				light.spotAngle = spec.SpotAngle;
				light.innerSpotAngle = spec.SpotAngle * spec.InnerSpotPercent / 100f;
				light.range = spec.Range;
			}
		}
	}
}
