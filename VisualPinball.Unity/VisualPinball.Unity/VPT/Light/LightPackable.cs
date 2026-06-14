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

// ReSharper disable MemberCanBePrivate.Global

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity
{
	public struct LightPackable
	{
		public float BulbSize;
		public LampStatus State;
		public string BlinkPattern;
		public int BlinkInterval;
		public float FadeSpeedUp;
		public float FadeSpeedDown;
		public List<LightSourcePackable> LightSources;

		public static byte[] Pack(LightComponent comp)
		{
			return PackageApi.Packer.Pack(new LightPackable {
				BulbSize = comp.BulbSize,
				State = comp.State,
				BlinkPattern = comp.BlinkPattern,
				BlinkInterval = comp.BlinkInterval,
				FadeSpeedUp = comp.FadeSpeedUp,
				FadeSpeedDown = comp.FadeSpeedDown,
				LightSources = PackLightSources(comp),
			});
		}

		public static void Unpack(byte[] bytes, LightComponent comp)
		{
			var data = PackageApi.Packer.Unpack<LightPackable>(bytes);
			comp.BulbSize = data.BulbSize;
			comp.State = data.State;
			comp.BlinkPattern = data.BlinkPattern;
			comp.BlinkInterval = data.BlinkInterval;
			comp.FadeSpeedUp = data.FadeSpeedUp;
			comp.FadeSpeedDown = data.FadeSpeedDown;
			UnpackLightSources(data.LightSources, comp);
		}

		private static List<LightSourcePackable> PackLightSources(LightComponent comp)
		{
			return comp.GetComponentsInChildren<Light>(true)
				.Select(light => LightSourcePackable.From(comp.transform, light))
				.ToList();
		}

		private static void UnpackLightSources(IReadOnlyList<LightSourcePackable> lightSources, LightComponent comp)
		{
			if (lightSources == null || lightSources.Count == 0) {
				return;
			}

			var lights = comp.GetComponentsInChildren<Light>(true);
			for (var i = 0; i < lightSources.Count; i++) {
				var lightSource = lightSources[i];
				var target = comp.transform.FindByPath(lightSource.Path)?.GetComponent<Light>();
				if (!target && i < lights.Length) {
					target = lights[i];
				}
				if (target) {
					lightSource.Apply(target);
				}
			}
		}
	}

	public struct LightSourcePackable
	{
		/// <summary>
		/// Stable node id. Set in the table-level lights payload (meta/lights.json); takes
		/// precedence over <see cref="Path"/>.
		/// </summary>
		public string NodeId;
		/// <summary>
		/// Component-relative sibling-index path. Used by <see cref="LightPackable"/> for the
		/// light sources within one component (with an index fallback); null in meta/lights.json.
		/// </summary>
		public string Path;
		public bool Enabled;
		public int Type;
		public int Shape;
		public PackableColor Color;
		public float Intensity;
		public float Range;
		public float SpotAngle;
		public float InnerSpotAngle;
		public float CookieSize;
		public PackableFloat2 AreaSize;
		public float BounceIntensity;
		public float ColorTemperature;
		public bool UseColorTemperature;
		public float ShadowRadius;
		public float ShadowAngle;
		public int LightUnit;
		public int LightShadowCasterMode;
		public HdrpLightSourcePackable Hdrp;

		public static LightSourcePackable From(Transform root, Light light, string nodeId = null)
		{
			return new LightSourcePackable {
				NodeId = nodeId,
				Path = nodeId == null ? light.transform.GetPath(root) : null,
				Enabled = light.enabled,
				Type = (int)light.type,
				Shape = (int)light.shape,
				Color = light.color,
				Intensity = light.intensity,
				Range = light.range,
				SpotAngle = light.spotAngle,
				InnerSpotAngle = light.innerSpotAngle,
				CookieSize = light.cookieSize,
				AreaSize = new PackableFloat2(light.areaSize.x, light.areaSize.y),
				BounceIntensity = light.bounceIntensity,
				ColorTemperature = light.colorTemperature,
				UseColorTemperature = light.useColorTemperature,
				ShadowRadius = LightPropertyHelper.GetFloat(light, "shadowRadius", 0f),
				ShadowAngle = LightPropertyHelper.GetFloat(light, "shadowAngle", 0f),
				LightUnit = (int)light.lightUnit,
				LightShadowCasterMode = (int)light.lightShadowCasterMode,
				Hdrp = HdrpLightSourcePackable.From(light),
			};
		}

		public void Apply(Light light)
		{
			light.type = (LightType)Type;
			light.shape = (LightShape)Shape;
			light.color = Color;
			light.range = Range;
			light.spotAngle = SpotAngle;
			light.innerSpotAngle = InnerSpotAngle;
			light.cookieSize = CookieSize;
			light.areaSize = new Vector2(AreaSize.X, AreaSize.Y);
			light.bounceIntensity = BounceIntensity;
			light.colorTemperature = ColorTemperature;
			light.useColorTemperature = UseColorTemperature;
			LightPropertyHelper.SetFloat(light, "shadowRadius", ShadowRadius);
			LightPropertyHelper.SetFloat(light, "shadowAngle", ShadowAngle);
			light.lightUnit = (UnityEngine.Rendering.LightUnit)LightUnit;
			light.lightShadowCasterMode = (LightShadowCasterMode)LightShadowCasterMode;
			light.intensity = Intensity;
			light.enabled = Enabled;
			Hdrp.Apply(light);
		}
	}

	public struct HdrpLightSourcePackable
	{
		public bool HasData;
		public float VolumetricDimmer;
		public float ShapeRadius;
		public float LightDimmer;
		public bool AffectDiffuse;
		public bool AffectSpecular;
		public bool AffectsVolumetric;
		public bool IncludeForRayTracing;
		public bool UseRayTracedShadows;
		public bool SemiTransparentShadow;
		public float ShadowNearPlane;
		public bool UseCustomSpotLightShadowCone;
		public float CustomSpotLightShadowCone;

		public static HdrpLightSourcePackable From(Light light)
		{
			var hdrp = GetHdrpLight(light);
			return hdrp
				? new HdrpLightSourcePackable {
					HasData = true,
					VolumetricDimmer = GetFloat(hdrp, "volumetricDimmer", 1f),
					ShapeRadius = GetFloat(hdrp, "shapeRadius", LightPropertyHelper.GetFloat(light, "shadowRadius", 0f)),
					LightDimmer = GetFloat(hdrp, "lightDimmer", 1f),
					AffectDiffuse = GetBool(hdrp, "affectDiffuse", true),
					AffectSpecular = GetBool(hdrp, "affectSpecular", true),
					AffectsVolumetric = GetBool(hdrp, "affectsVolumetric", true),
					IncludeForRayTracing = GetBool(hdrp, "includeForRayTracing", true),
					UseRayTracedShadows = GetBool(hdrp, "useRayTracedShadows", false),
					SemiTransparentShadow = GetBool(hdrp, "semiTransparentShadow", false),
					ShadowNearPlane = GetFloat(hdrp, "shadowNearPlane", 0.1f),
					UseCustomSpotLightShadowCone = GetBool(hdrp, "useCustomSpotLightShadowCone", false),
					CustomSpotLightShadowCone = GetFloat(hdrp, "customSpotLightShadowCone", light.innerSpotAngle),
				}
				: new HdrpLightSourcePackable();
		}

		public void Apply(Light light)
		{
			if (!HasData) {
				return;
			}

			var hdrp = GetHdrpLight(light);
			if (!hdrp) {
				return;
			}

			SetFloat(hdrp, "volumetricDimmer", VolumetricDimmer);
			SetFloat(hdrp, "shapeRadius", ShapeRadius);
			SetFloat(hdrp, "lightDimmer", LightDimmer);
			SetBool(hdrp, "affectDiffuse", AffectDiffuse);
			SetBool(hdrp, "affectSpecular", AffectSpecular);
			SetBool(hdrp, "affectsVolumetric", AffectsVolumetric);
			SetBool(hdrp, "includeForRayTracing", IncludeForRayTracing);
			SetBool(hdrp, "useRayTracedShadows", UseRayTracedShadows);
			SetBool(hdrp, "semiTransparentShadow", SemiTransparentShadow);
			SetFloat(hdrp, "shadowNearPlane", ShadowNearPlane);
			SetBool(hdrp, "useCustomSpotLightShadowCone", UseCustomSpotLightShadowCone);
			SetFloat(hdrp, "customSpotLightShadowCone", CustomSpotLightShadowCone);
		}

		private static Component GetHdrpLight(Light light)
		{
			return light.GetComponents<Component>()
				.FirstOrDefault(component => component && component.GetType().FullName == "UnityEngine.Rendering.HighDefinition.HDAdditionalLightData");
		}

		private static float GetFloat(Object target, string propertyName, float fallback)
		{
			var property = target.GetType().GetProperty(propertyName);
			return property?.CanRead == true && property.GetValue(target) is float value ? value : fallback;
		}

		private static bool GetBool(Object target, string propertyName, bool fallback)
		{
			var property = target.GetType().GetProperty(propertyName);
			return property?.CanRead == true && property.GetValue(target) is bool value ? value : fallback;
		}

		private static void SetFloat(Object target, string propertyName, float value)
		{
			var property = target.GetType().GetProperty(propertyName);
			if (property?.CanWrite == true) {
				property.SetValue(target, value);
			}
		}

		private static void SetBool(Object target, string propertyName, bool value)
		{
			var property = target.GetType().GetProperty(propertyName);
			if (property?.CanWrite == true) {
				property.SetValue(target, value);
			}
		}
	}

	public static class LightPropertyHelper
	{
		public static float GetFloat(Object target, string propertyName, float fallback)
		{
			var property = target.GetType().GetProperty(propertyName);
			return property?.CanRead == true && property.GetValue(target) is float value ? value : fallback;
		}

		public static void SetFloat(Object target, string propertyName, float value)
		{
			var property = target.GetType().GetProperty(propertyName);
			if (property?.CanWrite == true) {
				property.SetValue(target, value);
			}
		}
	}
}
