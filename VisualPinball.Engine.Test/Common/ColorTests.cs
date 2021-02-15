using System.IO;
using FluentAssertions;
using NUnit.Framework;
using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.Test.Common
{
	public class ColorTests
	{
		[Test]
		public void TestColor()
		{
			using (var memStream = new MemoryStream(new byte[] {0xff, 0xa0, 0xb4, 0x80 })) {
				using (var reader = new BinaryReader(memStream)) {
					var intCol = reader.ReadUInt32();
					var color = new Color(intCol, ColorFormat.Bgr);
					color.Red.Should().Be(0xff);
					color.Green.Should().Be(0xa0);
					color.Blue.Should().Be(0xb4);
					color.Alpha.Should().Be(0x80);
				}
			}
		}
	}
}
