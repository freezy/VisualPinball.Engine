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
