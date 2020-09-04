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
using VisualPinball.Engine.VPT.Collection;

namespace VisualPinball.Engine.Test.VPT.Collection
{
	public class CollectionDataTests
	{
		[Test]
		public void ShouldReadCollectionData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Collection);
			var data = table.Collections["flippers"].Data;
			ValidateTableData(data);
		}

		[Test]
		public void ShouldWriteCollectionData()
		{
			const string tmpFileName = "ShouldWriteCollectionData.vpx";
			var table = Engine.VPT.Table.Table.Load(VpxPath.Collection);
			table.Save(tmpFileName);
			var writtenTable = Engine.VPT.Table.Table.Load(tmpFileName);
			ValidateTableData(writtenTable.Collections["flippers"].Data);
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
