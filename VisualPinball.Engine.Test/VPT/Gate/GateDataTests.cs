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
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Gate;

namespace VisualPinball.Engine.Test.VPT.Gate
{
	public class GateDataTests
	{
		[Test]
		public void ShouldReadGateData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Gate);
			ValidateGateData(table.Gate("Data").Data);
		}

		[Test]
		public void ShouldWriteGateData()
		{
			const string tmpFileName = "ShouldWriteGateData.vpx";
			var table = Engine.VPT.Table.Table.Load(VpxPath.Gate);
			table.Save(tmpFileName);
			var writtenTable = Engine.VPT.Table.Table.Load(tmpFileName);
			ValidateGateData(writtenTable.Gate("Data").Data);
		}

		private static void ValidateGateData(GateData data)
		{
			MathF.RadToDeg(data.AngleMax).Should().Be(90f);
			MathF.RadToDeg(data.AngleMin).Should().Be(0f);
			data.Center.X.Should().Be(769f);
			data.Center.Y.Should().Be(1019f);
			data.Damping.Should().Be(0.92958f);
			data.Elasticity.Should().Be(0.1348f);
			data.Friction.Should().Be(0.1983f);
			data.GateType.Should().Be(GateType.GatePlate);
			data.GravityFactor.Should().Be(0.2198f);
			data.Height.Should().Be(123.42f);
			data.IsCollidable.Should().Be(true);
			data.IsReflectionEnabled.Should().Be(true);
			data.IsVisible.Should().Be(true);
			data.Length.Should().Be(40.12f);
			data.Material.Should().Be("Red");
			data.Rotation.Should().Be(-72.212f);
			data.ShowBracket.Should().Be(true);
			data.Surface.Should().Be("");
			data.TwoWay.Should().Be(true);
		}
	}
}
