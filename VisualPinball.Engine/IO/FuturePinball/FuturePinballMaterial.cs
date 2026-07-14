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

using System;

using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Engine.IO.FuturePinball
{
	public enum FuturePinballMaterialCategory
	{
		Metal = 0,
		Wood = 1,
		Plastic = 2,
		Rubber = 3,
		Unknown = 255
	}

	public sealed class FuturePinballMaterialDescription
	{
		public string Name { get; internal set; }
		public FuturePinballMaterialCategory Category { get; internal set; }
		public uint SourceColor { get; internal set; }
		public float Opacity { get; internal set; }
		public float Roughness { get; internal set; }
		public bool IsMetal { get; internal set; }
		public bool IsCrystal { get; internal set; }
		public bool IsSphereMapped { get; internal set; }
		public bool IsTwoSided { get; internal set; }
		public bool IsEmissive { get; internal set; }
		public string Texture { get; internal set; }
		public int SourceMaterialType { get; internal set; }
		public int SourceTransparency { get; internal set; }

		public Material ToVpeMaterial(string name = null)
		{
			var color = new Color(SourceColor, ColorFormat.Bgr).WithAlpha(255);
			var material = new Material(name ?? Name ?? "Future Pinball") {
				BaseColor = color,
				Roughness = Roughness,
				GlossyImageLerp = IsSphereMapped ? 1f : 0f,
				Thickness = IsCrystal ? 0.15f : 0.05f,
				Edge = IsSphereMapped || IsCrystal ? 1f : 0.25f,
				Opacity = Opacity,
				IsMetal = IsMetal,
				IsOpacityActive = Opacity < 0.999f
			};
			material.UpdateData();
			return material;
		}
	}

	public static class FuturePinballMaterialConverter
	{
		private const uint NameTag = 0xA4F4D1D7;
		private const uint MaterialTypeTag = 0x99E8BED8;
		private const uint TransparencyTag = 0x9C00C0D1;
		private const uint SphereMappingTag = 0x99F4C2D2;
		private const uint CrystalTag = 0x96E8C0E2;
		private const uint DisableCullingTag = 0x9DECCFE1;

		public static FuturePinballMaterialDescription FromElement(
			FuturePinballSourceStream element,
			uint textureTag,
			uint colorTag)
		{
			if (element == null) throw new ArgumentNullException(nameof(element));
			var name = FuturePinballElementGeometry.Text(element, NameTag, element.Name);
			var category = FuturePinballElementGeometry.Integer(element, MaterialTypeTag, 2);
			var color = FuturePinballElementGeometry.Color(element, colorTag);
			var transparency = FuturePinballElementGeometry.Integer(element, TransparencyTag, 10);
			return FromValues(
				name,
				category,
				color,
				transparency,
				FuturePinballElementGeometry.Integer(element, CrystalTag) != 0,
				FuturePinballElementGeometry.Integer(element, SphereMappingTag) != 0,
				FuturePinballElementGeometry.Integer(element, DisableCullingTag) != 0,
				IsEmissive(element.ElementType),
				FuturePinballElementGeometry.Text(element, textureTag)
			);
		}

		public static FuturePinballMaterialDescription FromValues(
			string name,
			int materialType,
			uint color,
			int transparency = 10,
			bool crystal = false,
			bool sphereMapped = false,
			bool twoSided = false,
			bool emissive = false,
			string texture = "")
		{
			var category = Category(materialType);
			var opacity = Clamp01(transparency / 10f);
			if (crystal && opacity >= 0.999f) opacity = 0.35f;
			return new FuturePinballMaterialDescription {
				Name = name,
				Category = category,
				SourceColor = color,
				Opacity = opacity,
				Roughness = Roughness(category),
				IsMetal = category == FuturePinballMaterialCategory.Metal,
				IsCrystal = crystal,
				IsSphereMapped = sphereMapped,
				IsTwoSided = twoSided,
				IsEmissive = emissive,
				Texture = texture ?? string.Empty,
				SourceMaterialType = materialType,
				SourceTransparency = transparency
			};
		}

		public static FuturePinballMaterialDescription FromMilkShape(MilkShapeMaterial material)
		{
			if (material == null) throw new ArgumentNullException(nameof(material));
			return FromMilkShape(material.Name, material.Diffuse, material.Shininess, material.Transparency, material.Texture);
		}

		public static FuturePinballMaterialDescription FromMilkShape(
			string name,
			float[] diffuse,
			float shininess,
			float transparency,
			string texture = "")
		{
			diffuse ??= Array.Empty<float>();
			var red = Channel(diffuse, 0, 1f);
			var green = Channel(diffuse, 1, 1f);
			var blue = Channel(diffuse, 2, 1f);
			var alpha = Channel(diffuse, 3, 1f);
			var opacity = Clamp01(transparency * alpha);
			var normalizedShininess = Clamp01(shininess / 128f);
			return new FuturePinballMaterialDescription {
				Name = name,
				Category = FuturePinballMaterialCategory.Plastic,
				SourceColor = Pack(red, green, blue),
				Opacity = opacity,
				Roughness = Clamp01(1f - (float)System.Math.Sqrt(normalizedShininess)),
				IsMetal = false,
				Texture = texture ?? string.Empty,
				SourceMaterialType = -1,
				SourceTransparency = (int)System.Math.Round(opacity * 10f)
			};
		}

		private static FuturePinballMaterialCategory Category(int value)
		{
			return value >= 0 && value <= 3
				? (FuturePinballMaterialCategory)value
				: FuturePinballMaterialCategory.Unknown;
		}

		private static float Roughness(FuturePinballMaterialCategory category)
		{
			switch (category) {
				case FuturePinballMaterialCategory.Metal: return 0.22f;
				case FuturePinballMaterialCategory.Wood: return 0.55f;
				case FuturePinballMaterialCategory.Rubber: return 0.82f;
				default: return 0.38f;
			}
		}

		private static bool IsEmissive(FuturePinballElementType? type)
		{
			switch (type) {
				case FuturePinballElementType.RoundLight:
				case FuturePinballElementType.ShapeableLight:
				case FuturePinballElementType.Flasher:
				case FuturePinballElementType.Bulb:
				case FuturePinballElementType.LightImage:
				case FuturePinballElementType.HudLightImage:
					return true;
				default:
					return false;
			}
		}

		private static float Channel(float[] values, int index, float fallback)
		{
			return index < values.Length ? Clamp01(values[index]) : fallback;
		}

		private static uint Pack(float red, float green, float blue)
		{
			return (uint)(System.Math.Round(Clamp01(red) * 255f)
				+ ((uint)System.Math.Round(Clamp01(green) * 255f) << 8)
				+ ((uint)System.Math.Round(Clamp01(blue) * 255f) << 16)
				+ (0xffu << 24));
		}

		private static float Clamp01(float value)
		{
			return value < 0f ? 0f : value > 1f ? 1f : value;
		}
	}
}
