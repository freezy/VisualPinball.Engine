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
using VisualPinball.Engine.VPT.LightSeq;

namespace VisualPinball.Engine.Test.VPT.LightSeq
{
	public class LightSeqDataTests : BaseTests
	{
		[Test]
		public void ShouldReadLightSeqData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.LightSeq);
			ValidateLightSeqData(table.LightSeq("LightSeq001").Data);
		}

		[Test]
		public void ShouldWriteLightSeqData()
		{
			const string tmpFileName = "ShouldWriteLightSeqData.vpx";
			var table = Engine.VPT.Table.Table.Load(VpxPath.LightSeq);
			table.Save(tmpFileName);
			var writtenTable = Engine.VPT.Table.Table.Load(tmpFileName);
			ValidateLightSeqData(writtenTable.LightSeq("LightSeq001").Data);
		}

		private static void ValidateLightSeqData(LightSeqData data)
		{
			data.Backglass.Should().Be(false);
			data.Center.X.Should().Be(21.23f);
			data.Center.Y.Should().Be(503.68f);
			data.Collection.Should().Be("Collection001");
			data.EditorLayer.Should().Be(0);
			data.EditorLayerName.Should().Be(string.Empty);
			data.EditorLayerVisibility.Should().Be(true);
			data.IsLocked.Should().Be(false);
			data.IsTimerEnabled.Should().Be(true);
			data.TimerInterval.Should().Be(112);
			data.UpdateInterval.Should().Be(256);
		}
	}
}
