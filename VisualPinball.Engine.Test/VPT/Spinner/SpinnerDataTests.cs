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
using VisualPinball.Engine.VPT.Spinner;

namespace VisualPinball.Engine.Test.VPT.Spinner
{
	public class SpinnerDataTests
	{
		[Test]
		public void ShouldReadSpinnerData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Spinner);
			ValidateSpinnerData(table.Spinner("Data").Data);
		}

		[Test]
		public void ShouldWriteSpinnerData()
		{
			const string tmpFileName = "ShouldWriteSpinnerData.vpx";
			var table = Engine.VPT.Table.Table.Load(VpxPath.Spinner);
			table.Save(tmpFileName);
			var writtenTable = Engine.VPT.Table.Table.Load(tmpFileName);
			ValidateSpinnerData(writtenTable.Spinner("Data").Data);
		}

		private static void ValidateSpinnerData(SpinnerData data)
		{
			data.AngleMax.Should().Be(50.698f);
			data.AngleMin.Should().Be(-12.87f);
			data.Center.X.Should().Be(494f);
			data.Center.Y.Should().Be(1401.62f);
			data.Elasticity.Should().Be(0.6824f);
			data.Height.Should().Be(13.532f);
			data.Image.Should().Be("");
			data.IsVisible.Should().Be(true);
			data.Length.Should().Be(124.31f);
			data.Material.Should().Be("Red");
			data.Rotation.Should().Be(47.98f);
			data.ShowBracket.Should().Be(true);
			data.Surface.Should().Be("");
		}
	}
}
