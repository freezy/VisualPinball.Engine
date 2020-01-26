using System;
using System.IO;
using System.Net;
using System.Reflection;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT;
using Xunit;
using Xunit.Abstractions;

namespace VisualPinball.Engine.Test.VPT
{
	public class MaterialTextureTests
	{
		private readonly Engine.VPT.Table.Table _table;

		public MaterialTextureTests()
		{
			_table = Engine.VPT.Table.Table.Load(VpxPath.MaterialTexture);
		}

		[Fact]
		public void ShouldLoadCorrectArgb()
		{
			var texture = _table.Textures["mat_cutout"];
		}
	}
}
