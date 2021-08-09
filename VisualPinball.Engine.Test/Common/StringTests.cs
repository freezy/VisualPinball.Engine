// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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
using VisualPinball.Engine.Common;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Engine.Test.Common
{
	public class StringTests
	{
		[Test]
		public void ShouldCorrectlyMakeAStringFilesystemCompatible()
		{
			"^ !#$%&'()+,.0123456789;=@ABCDEFGHIJKLMNOPQRSTUVWXYZ[]^_`abcdefghijklmnopqrstuvwxyz{}~-".ToFilename()
				.Should().Be("^ !#$%&'()+,.0123456789;=@ABCDEFGHIJKLMNOPQRSTUVWXYZ[]^_`abcdefghijklmnopqrstuvwxyz{}~-");
			"äöüéàèŒ".ToFilename().Should().Be("aeoeueeaeOE");
			"a/b/c".ToFilename().Should().Be("a_b_c");
			"a\\b".ToFilename().Should().Be("a_b");
			"a>b".ToFilename().Should().Be("a_b");
			"a<b".ToFilename().Should().Be("a_b");
			"(a)".ToFilename().Should().Be("(a)");
			"a>>b".ToFilename().Should().Be("a_b");
			"a<<b>>".ToFilename().Should().Be("a_b");
			"a\"b".ToFilename().Should().Be("a_b");
			"\"".ToFilename().Should().Be("_");
		}

		[Test]
		public void ShouldCorrectlyNormalizeAString()
		{
			"AbC".ToNormalizedName().Should().Be("abc");
			"AbC ".ToNormalizedName().Should().Be("abc");
			"Ab C".ToNormalizedName().Should().Be("ab_c");
			"übr".ToNormalizedName().Should().Be("uebr");
			"a\"b".ToNormalizedName().Should().Be("a_b");
			">".ToNormalizedName().Should().Be("_");
			"(a)".ToNormalizedName().Should().Be("a");
		}

		[Test]
		public void ShouldCorrectlyParseMaterialNames()
		{
			var test1 = PbrMaterial.ParseId("__std_", new[] { "" }, new[] {"Töxture1", "Töxture"});
			test1[0].Should().BeEmpty();
			test1[1].Should().BeEmpty();
			test1[2].Should().BeEmpty();
			test1[3].Should().BeEmpty();

			var test2 = PbrMaterial.ParseId("__std_ toexture1", new string[] { }, new[] {"Töxture1", "Töxture"});
			test2[0].Should().BeEmpty();
			test2[1].Should().Be("Töxture1");
			test2[2].Should().BeEmpty();
			test2[3].Should().BeEmpty();

			var test3 = PbrMaterial.ParseId("mat tex normal env", new [] { "Mat" }, new[] { "Tex", "Normal", "Env"});
			test3[0].Should().Be("Mat");
			test3[1].Should().Be("Tex");
			test3[2].Should().Be("Normal");
			test3[3].Should().Be("Env");

			var test4 = PbrMaterial.ParseId("mat tex __no_normal_map env", new [] { "Mat" }, new[] { "Tex", "Normal", "Env"});
			test4[0].Should().Be("Mat");
			test4[1].Should().Be("Tex");
			test4[2].Should().BeEmpty();
			test4[3].Should().Be("Env");
		}
	}
}
