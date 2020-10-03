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
using VisualPinball.Engine.VPT.Flipper;

namespace VisualPinball.Engine.Test.VPT.Flipper
{
	public class FlipperDataTests : BaseTests
	{
		[Test]
		public void ShouldReadFlipperData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Flipper);
			ValidateFlipper(table.Flipper("FatFlipper").Data);
		}

		[Test]
		public void ShouldWriteFlipperData()
		{
			const string tmpFileName = "ShouldWriteFlipperData.vpx";
			var table = Engine.VPT.Table.Table.Load(VpxPath.Flipper);
			table.Save(tmpFileName);
			var writtenTable = Engine.VPT.Table.Table.Load(tmpFileName);
			ValidateFlipper(writtenTable.Flipper("FatFlipper").Data);
		}

		private static void ValidateFlipper(FlipperData data)
		{
			data.BaseRadius.Should().Be(30.0303f);
			data.Center.X.Should().Be(269.287f);
			data.Center.Y.Should().Be(1301.21f);
			data.Elasticity.Should().Be(0.823f);
			data.ElasticityFalloff.Should().Be(0.4321f);
			data.EndAngle.Should().Be(70.701f);
			data.EndRadius.Should().Be(20.1762f);
			data.FlipperRadius.Should().Be(150.987f);
			data.FlipperRadiusMax.Should().Be(150.987f);
			data.FlipperRadiusMin.Should().Be(0.332f);
			data.Friction.Should().Be(0.6187f);
			data.Height.Should().Be(70.1627f);
			data.Image.Should().Be("ldr");
			data.IsEnabled.Should().Be(true);
			data.IsReflectionEnabled.Should().Be(true);
			data.IsVisible.Should().Be(true);
			data.Mass.Should().Be(1.1992f);
			data.Material.Should().Be("Playfield");
			data.OverridePhysics.Should().Be(0);
			data.RampUp.Should().Be(3.109f);
			data.Return.Should().Be(0.05813f);
			data.RubberHeight.Should().Be(19.912f);
			data.RubberMaterial.Should().Be("");
			data.RubberWidth.Should().Be(24.1872f);
			data.Scatter.Should().Be(0.192f);
			data.StartAngle.Should().Be(121.163f);
			data.Strength.Should().Be(2200.1832f);
			data.Surface.Should().Be("");
			data.TorqueDamping.Should().Be(0.7532f);
			data.TorqueDampingAngle.Should().Be(6.209f);

			data.TimerInterval.Should().Be(100);
			data.IsTimerEnabled.Should().Be(false);

			data.EditorLayer.Should().Be(0);
			data.EditorLayerName.Should().Be(string.Empty);
			data.EditorLayerVisibility.Should().Be(true);
			data.IsLocked.Should().Be(false);
		}
	}
}
