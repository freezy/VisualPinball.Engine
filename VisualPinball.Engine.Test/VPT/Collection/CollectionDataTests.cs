using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT.Collection;
using Xunit;

namespace VisualPinball.Engine.Test.VPT.Collection
{
	public class CollectionDataTests
	{
		[Fact]
		public void ShouldLoadCorrectData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Collection);
			var data = table.Collections["flippers"].Data;
			ValidateTableData(data);
		}

		[Fact]
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
			Assert.Equal("Flippers", data.Name);
			Assert.Equal(false, data.FireEvents);
			Assert.Equal(true, data.GroupElements);
			Assert.Equal("Flipper001", data.ItemNames[0]);
			Assert.Equal("Flipper002", data.ItemNames[1]);
			Assert.Equal(false, data.StopSingleEvents);
		}
	}
}
