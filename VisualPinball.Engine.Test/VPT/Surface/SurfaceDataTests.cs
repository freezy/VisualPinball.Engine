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

using FluentAssertions;
using NUnit.Framework;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT.Surface;

namespace VisualPinball.Engine.Test.VPT.Surface
{
	public class SurfaceDataTests
	{
		[Test]
		public void ShouldReadSurfaceData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Surface);
			ValidateSurfaceData(table.Surface("TopInvisible").Data);
		}

		[Test]
		public void ShouldWriteSurfaceData()
		{
			const string tmpFileName = "ShouldWriteSurfaceData.vpx";
			var table = Engine.VPT.Table.Table.Load(VpxPath.Surface);
			table.Save(tmpFileName);
			var writtenTable = Engine.VPT.Table.Table.Load(tmpFileName);
			ValidateSurfaceData(writtenTable.Surface("TopInvisible").Data);
		}

		private static void ValidateSurfaceData(SurfaceData data)
		{
			data.DisableLightingBelow.Should().Be(0.6985f);
			data.DisableLightingTop.Should().BeInRange(0.129f, 0.13f);
			data.DisplayTexture.Should().Be(false); // editor only
			data.DragPoints.Length.Should().Be(10);
			data.Elasticity.Should().Be(0.368f);
			data.Friction.Should().Be(0.381f);
			data.HeightBottom.Should().Be(2.7f);
			data.HeightTop.Should().Be(49.6f);
			data.HitEvent.Should().Be(false);
			data.Image.Should().Be("");
			data.Inner.Should().Be(true); // ???
			data.IsBottomSolid.Should().Be(true); // ???
			data.IsCollidable.Should().Be(true);
			data.IsDroppable.Should().Be(false);
			data.IsFlipbook.Should().Be(false);
			data.IsReflectionEnabled.Should().Be(true);
			data.IsSideVisible.Should().Be(true);
			data.IsTopBottomVisible.Should().Be(false);
			data.OverwritePhysics.Should().Be(true);
			data.PhysicsMaterial.Should().Be("");
			data.Scatter.Should().Be(0f);
			data.SideImage.Should().Be("test_pattern");
			data.SideMaterial.Should().Be("Playfield");
			data.SlingshotAnimation.Should().Be(true);
			data.SlingshotForce.Should().BeInRange(80.22f, 80.24f);
			data.SlingShotMaterial.Should().Be("");
			data.SlingshotThreshold.Should().Be(0.029f);
			data.Threshold.Should().Be(2f);
			data.TopMaterial.Should().Be("");
		}
	}
}
