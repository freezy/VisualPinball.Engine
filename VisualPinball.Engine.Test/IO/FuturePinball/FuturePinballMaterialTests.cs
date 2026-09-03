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

using NUnit.Framework;

using VisualPinball.Engine.IO.FuturePinball;

namespace VisualPinball.Engine.Test.IO.FuturePinball
{
	[TestFixture]
	public class FuturePinballMaterialTests
	{
		[Test]
		public void ConvertsMetalPresetToVpeMaterial()
		{
			var source = FuturePinballMaterialConverter.FromValues(
				"metal", 0, 0xff332211, 7, sphereMapped: true, twoSided: true, texture: "metal.jpg"
			);
			var material = source.ToVpeMaterial();

			Assert.That(source.Category, Is.EqualTo(FuturePinballMaterialCategory.Metal));
			Assert.That(source.IsSphereMapped, Is.True);
			Assert.That(source.IsTwoSided, Is.True);
			Assert.That(source.Texture, Is.EqualTo("metal.jpg"));
			Assert.That(material.IsMetal, Is.True);
			Assert.That(material.BaseColor.Red, Is.EqualTo(0x11));
			Assert.That(material.BaseColor.Green, Is.EqualTo(0x22));
			Assert.That(material.BaseColor.Blue, Is.EqualTo(0x33));
			Assert.That(material.Opacity, Is.EqualTo(0.7f).Within(0.0001f));
			Assert.That(material.IsOpacityActive, Is.True);
		}

		[Test]
		public void AppliesCrystalFallbackOpacityWithoutLosingSourceValue()
		{
			var source = FuturePinballMaterialConverter.FromValues("crystal", 2, 0xffffffff, 10, crystal: true);

			Assert.That(source.IsCrystal, Is.True);
			Assert.That(source.SourceTransparency, Is.EqualTo(10));
			Assert.That(source.Opacity, Is.EqualTo(0.35f));
			Assert.That(source.ToVpeMaterial().Thickness, Is.EqualTo(0.15f));
		}

		[Test]
		public void ConvertsMilkShapeDiffuseTransparencyAndShininess()
		{
			var source = FuturePinballMaterialConverter.FromMilkShape(
				"ms3d", new[] { 1f, 0.5f, 0.25f, 0.5f }, 128f, 0.8f, "diffuse.png"
			);

			Assert.That(source.SourceColor, Is.EqualTo(0xff4080ff));
			Assert.That(source.Opacity, Is.EqualTo(0.4f).Within(0.0001f));
			Assert.That(source.Roughness, Is.Zero);
			Assert.That(source.Texture, Is.EqualTo("diffuse.png"));
		}

		[Test]
		public void PreservesUnknownMaterialCategory()
		{
			var source = FuturePinballMaterialConverter.FromValues("future", 42, 0xffffffff);

			Assert.That(source.Category, Is.EqualTo(FuturePinballMaterialCategory.Unknown));
			Assert.That(source.SourceMaterialType, Is.EqualTo(42));
		}
	}
}
