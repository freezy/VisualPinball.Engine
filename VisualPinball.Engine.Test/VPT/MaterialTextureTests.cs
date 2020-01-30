using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Mime;
using System.Reflection;
using NetVips;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT;
using Xunit;
using Xunit.Abstractions;

namespace VisualPinball.Engine.Test.VPT
{
	public class MaterialTextureTests
	{
		private readonly ITestOutputHelper _testOutputHelper;
		private readonly Engine.VPT.Table.Table _table;

		public MaterialTextureTests(ITestOutputHelper testOutputHelper)
		{
			_testOutputHelper = testOutputHelper;
			//_table = Engine.VPT.Table.Table.Load(VpxPath.MaterialTexture);
		}

		[Fact]
		public void ShouldLoadCorrectArgb()
		{
			// var texture = _table.Textures["mat_cutout"];
			// texture.Analyze();

			var image = Image.NewFromFile(@"..\..\VPT\target-yellow.png");

			var sw = new Stopwatch();
			sw.Start();
			var hist = image[3].HistFind();
			var total = image.Width * image.Height;
			var alpha0 = BandStats(hist, 0);
			var alpha1 = BandStats(hist, 254);
			sw.Stop();


			// var mask = (Image.Identity() > 128) / 255;
			// var alpha = (image[3].HistFind() * mask).Avg() * 256;
			_testOutputHelper.WriteLine("With mask (" + sw.ElapsedMilliseconds  + "ms)");
			_testOutputHelper.WriteLine("transparent: " + (total - alpha0));
			_testOutputHelper.WriteLine("translucent: " + (alpha0 - alpha1));
			_testOutputHelper.WriteLine("transparent: " + alpha1);

			sw.Reset();
			sw.Start();
			var opaque = 0;
			var translucent = 0;
			var transparent = 0;
			for (var i = 0; i < hist.Width; i++) {
				if (i < 1) {
					transparent += (int)hist[i, 0][0];

				} else if (i <= 254) {
					translucent += (int)hist[i, 0][0];

				} else {
					opaque += (int)hist[i, 0][0];
				}
			}
			sw.Stop();
			_testOutputHelper.WriteLine("With loop (" + sw.ElapsedMilliseconds  + "ms)");
			_testOutputHelper.WriteLine("transparent: " + transparent);
			_testOutputHelper.WriteLine("translucent: " + translucent);
			_testOutputHelper.WriteLine("opaque: " + opaque);
		}

		private static double BandStats(Image hist, int val)
		{
			var mask = (Image.Identity() > val) / 255;
			return (hist * mask).Avg() * 256;
		}
	}
}
