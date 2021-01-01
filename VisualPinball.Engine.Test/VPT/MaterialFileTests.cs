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

using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Engine.Test.VPT
{
	public class MaterialFileTests
	{
		[Test]
		public void ShouldReadMaterialFile()
		{
			var materials = MaterialReader.Load(MaterialPath.Mat).ToArray();
			materials.Length.Should().Be(3);

			ValidateMaterial0(materials[0]);
			ValidateMaterial2(materials[2]);
		}

		private void ValidateMaterial0(Material material)
		{
			material.Name.Should().Be("a_physics_Rubbers");
			material.BaseColor.Red.Should().Be(244);
			material.BaseColor.Green.Should().Be(241);
			material.BaseColor.Blue.Should().Be(234);
			material.ClearCoat.Red.Should().Be(0);
			material.ClearCoat.Green.Should().Be(0);
			material.ClearCoat.Blue.Should().Be(0);
			material.Edge.Should().Be(0f);
			material.EdgeAlpha.Should().BeApproximately(1f, 0.00001f);
			material.Elasticity.Should().Be(0.8f);
			material.ElasticityFalloff.Should().Be(0.4f);
			material.Friction.Should().Be(0.6f);
			material.Glossiness.Red.Should().Be(7);
			material.Glossiness.Green.Should().Be(7);
			material.Glossiness.Blue.Should().Be(7);
			material.GlossyImageLerp.Should().BeApproximately(0f, 0.00001f);
			material.IsMetal.Should().Be(false);
			material.IsOpacityActive.Should().Be(false);
			material.Opacity.Should().Be(0f);
			material.Roughness.Should().Be(0.05f);
			material.ScatterAngle.Should().Be(5f);
			material.Thickness.Should().BeApproximately(0.04705882f, 0.00001f);
			material.WrapLighting.Should().Be(0.25f);
		}

		private void ValidateMaterial1(Material material)
		{
			material.Name.Should().Be("BallShadow");
			material.BaseColor.Red.Should().Be(0);
			material.BaseColor.Green.Should().Be(0);
			material.BaseColor.Blue.Should().Be(0);
			material.ClearCoat.Red.Should().Be(0);
			material.ClearCoat.Green.Should().Be(0);
			material.ClearCoat.Blue.Should().Be(0);
			material.Edge.Should().Be(0f);
			material.EdgeAlpha.Should().BeApproximately(0f, 0.00001f);
			material.Elasticity.Should().Be(0f);
			material.ElasticityFalloff.Should().Be(0f);
			material.Friction.Should().Be(0f);
			material.Glossiness.Red.Should().Be(0);
			material.Glossiness.Green.Should().Be(0);
			material.Glossiness.Blue.Should().Be(0);
			material.GlossyImageLerp.Should().BeApproximately(0f, 0.00001f);
			material.IsMetal.Should().Be(false);
			material.IsOpacityActive.Should().Be(true);
			material.Opacity.Should().Be(0.9f);
			material.Roughness.Should().Be(0f);
			material.ScatterAngle.Should().Be(0f);
			material.Thickness.Should().BeApproximately(0.04705882f, 0.00001f);
			material.WrapLighting.Should().Be(1f);
		}

		private void ValidateMaterial2(Material material)
		{
			material.Name.Should().Be("Default");
			material.BaseColor.Red.Should().Be(0);
			material.BaseColor.Green.Should().Be(0);
			material.BaseColor.Blue.Should().Be(40);
			material.ClearCoat.Red.Should().Be(0);
			material.ClearCoat.Green.Should().Be(0);
			material.ClearCoat.Blue.Should().Be(0);
			material.Edge.Should().Be(0f);
			material.EdgeAlpha.Should().BeApproximately(1f, 0.00001f);
			material.Elasticity.Should().Be(0f);
			material.ElasticityFalloff.Should().Be(0f);
			material.Friction.Should().Be(0f);
			material.Glossiness.Red.Should().Be(0);
			material.Glossiness.Green.Should().Be(0);
			material.Glossiness.Blue.Should().Be(0);
			material.GlossyImageLerp.Should().BeApproximately(0.8f, 0.00001f);
			material.IsMetal.Should().Be(true);
			material.IsOpacityActive.Should().Be(true);
			material.Opacity.Should().Be(0.9f);
			material.Roughness.Should().Be(0.3f);
			material.ScatterAngle.Should().Be(0f);
			material.Thickness.Should().BeApproximately(0.5f, 0.01f);
			material.WrapLighting.Should().Be(1f);
		}
	}
}
