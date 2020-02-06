using System.IO;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT.Table;
using Xunit;
using Xunit.Abstractions;

namespace VisualPinball.Engine.Test.VPT
{
	public class DebugTests : BaseTests
	{
		public DebugTests(ITestOutputHelper output) : base(output) { }

		// [Fact]
		// public void ShouldWriteChecksum()
		// {
		// 	var table = Engine.VPT.Table.Table.Load(@"..\..\VPT\checksum.vpx");
		// 	table.Save( @"..\..\VPT\checksum_written.vpx");
		// }

	}
}
