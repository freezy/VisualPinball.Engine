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
using VisualPinball.Resources;

namespace VisualPinball.Engine.Test.VPT
{
	public class TextureBitmapTests
	{
		private readonly Engine.VPT.Table.Table _table;

		public TextureBitmapTests()
		{
			_table = Engine.VPT.Table.Table.Load(VpxPath.Texture);
		}

		[Test]
		public void ShouldAnalyzeAnOpaqueTexture()
		{
			var texture = _table.Textures["test_pattern_png"];
			var stats = texture.GetStats();

			stats.Opaque.Should().Be(1f);
			stats.Translucent.Should().Be(0f);
			stats.Transparent.Should().Be(0f);
		}

		[Test]
		public void ShouldAnalyzeAnotherOpaqueTexture()
		{
			var texture = _table.Textures["test_pattern_argb"];
			var stats = texture.GetStats();

			stats.Opaque.Should().Be(1f);
			stats.Translucent.Should().Be(0f);
			stats.Transparent.Should().Be(0f);
		}

		[Test]
		public void ShouldAnalyzeATransparentTexture()
		{
			var texture = _table.Textures["test_pattern_transparent"];
			texture.Analyze();
			var stats = texture.GetStats();

			stats.Opaque.Should().Be(0.657285035f);
			stats.Translucent.Should().Be(0.0102373762f);
			stats.Transparent.Should().Be(0.33247757f);
		}

		[Test]
		public void ShouldShipWithBallResource()
		{
			Resource.BallDebug.Data.Length.Should().BeGreaterThan(0);
		}
	}
}
