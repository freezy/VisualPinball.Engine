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

using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT.Collection;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.Test.VPT.Collection
{
	public class CollectionDataTests
	{
		[Test]
		public void ShouldReadCollectionData()
		{
			var th = FileTableContainer.Load(VpxPath.Collection);
			var data = th.Collections.First(c => c.Name == "flippers");
			ValidateTableData(data);
		}

		[Test]
		public void ShouldWriteCollectionData()
		{
			const string tmpFileName = "ShouldWriteCollectionData.vpx";
			var th = FileTableContainer.Load(VpxPath.Collection);
			th.Save(tmpFileName);
			var writtenTable = FileTableContainer.Load(tmpFileName);
			ValidateTableData(writtenTable.Collections.First(c => c.Name == "flippers"));
		}

		private static void ValidateTableData(CollectionData data)
		{
			data.Name.Should().Be("Flippers");
			data.FireEvents.Should().Be(false);
			data.GroupElements.Should().Be(true);
			data.ItemNames[0].Should().Be("Flipper001");
			data.ItemNames[1].Should().Be("Flipper002");
			data.StopSingleEvents.Should().Be(false);
		}
	}
}
