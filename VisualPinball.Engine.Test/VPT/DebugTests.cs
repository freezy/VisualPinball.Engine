using VisualPinball.Engine.Test.Test;
using Xunit;
using Xunit.Abstractions;

namespace VisualPinball.Engine.Test.VPT
{
	public class DebugTests : BaseTests
	{
		public DebugTests(ITestOutputHelper output) : base(output) { }

		[Fact]
		public void ShouldWriteChecksum()
		{
			var table = Engine.VPT.Table.Table.Load(@"..\..\VPT\blank-table.vpx");
			table.Save( @"..\..\VPT\blank-table_written.vpx");
		}
	}
}
