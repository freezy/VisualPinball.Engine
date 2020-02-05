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

		private static double BandStats(Image hist, int val)
		{
			var mask = (Image.Identity() > val) / 255;
			return (hist * mask).Avg() * 256;
		}
	}
}
