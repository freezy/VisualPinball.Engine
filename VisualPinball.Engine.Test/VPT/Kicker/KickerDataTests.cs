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

using System.IO;
using FluentAssertions;
using NUnit.Framework;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Kicker;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.Test.VPT.Kicker
{
	public class KickerDataTests
	{
		[Test]
		public void ShouldReadKickerData()
		{
			var table = FileTableContainer.Load(VpxPath.Kicker);
			ValidateKickerData(table.Kicker("Data").Data);
		}

		[Test]
		public void ShouldWriteKickerData()
		{
			const string tmpFileName = "ShouldWriteKickerData.vpx";
			var table = FileTableContainer.Load(VpxPath.Kicker);
			table.Save(tmpFileName);
			var writtenTable = FileTableContainer.Load(tmpFileName);
			ValidateKickerData(writtenTable.Kicker("Data").Data);
			File.Delete(tmpFileName);
		}

		public static void ValidateKickerData(KickerData data)
		{
			data.Center.X.Should().Be(781.6662f);
			data.Center.Y.Should().Be(1585f);
			data.FallThrough.Should().Be(true);
			data.HitAccuracy.Should().Be(0.6428f);
			data.HitHeight.Should().Be(36.684f);
			data.IsEnabled.Should().Be(false);
			data.KickerType.Should().Be(KickerType.KickerHoleSimple);
			data.LegacyMode.Should().Be(true);
			data.Material.Should().Be("Red");
			data.Orientation.Should().Be(65.988f);
			data.Radius.Should().Be(25.98f);
			data.Scatter.Should().Be(4.98f);
			data.Surface.Should().Be("");

			data.Angle.Should().Be(65.5f);
			data.Speed.Should().Be(5.8f);
		}
	}
}
