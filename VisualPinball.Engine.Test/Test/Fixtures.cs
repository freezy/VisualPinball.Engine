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

using System.IO;
using System.Reflection;

namespace VisualPinball.Engine.Test.Test
{
	public static class VpxPath
	{
		public static readonly string Bumper = PathHelper.GetFixturePath("BumperTest.vpx");
		public static readonly string BumperVPX1070 = PathHelper.GetFixturePath("BumperTestVPX1070.vpx");
		public static readonly string Collection = PathHelper.GetFixturePath("CollectionTest.vpx");
		public static readonly string MappingConfig = PathHelper.GetFixturePath("MappingConfigTest.vpx");
		public static readonly string Decal = PathHelper.GetFixturePath("DecalTest.vpx");
		public static readonly string DispReel = PathHelper.GetFixturePath("DispReelTest.vpx");
		public static readonly string Flasher = PathHelper.GetFixturePath("FlasherTest.vpx");
		public static readonly string Flipper = PathHelper.GetFixturePath("FlipperTest.vpx");
		public static readonly string Gate = PathHelper.GetFixturePath("GateTest.vpx");
		public static readonly string HitTarget = PathHelper.GetFixturePath("HitTargetTest.vpx");
		public static readonly string Kicker = PathHelper.GetFixturePath("KickerTest.vpx");
		public static readonly string Light = PathHelper.GetFixturePath("LightTest.vpx");
		public static readonly string LightSeq = PathHelper.GetFixturePath("LightSeqTest.vpx");
		public static readonly string Material = PathHelper.GetFixturePath("MaterialTest.vpx");
		public static readonly string MaterialTexture = PathHelper.GetFixturePath("MaterialTextureTest.vpx");
		public static readonly string Plunger = PathHelper.GetFixturePath("PlungerTest.vpx");
		public static readonly string Primitive = PathHelper.GetFixturePath("PrimitiveTest.vpx");
		public static readonly string PrimitiveAnimated = PathHelper.GetFixturePath("PrimitiveAnimated.vpx");
		public static readonly string PrimitiveCompressed = PathHelper.GetFixturePath("PrimitiveCompressed.vpx");
		public static readonly string Ramp = PathHelper.GetFixturePath("RampTest.vpx");
		public static readonly string Rubber = PathHelper.GetFixturePath("RubberTest.vpx");
		public static readonly string Sound = PathHelper.GetFixturePath("SoundTest.vpx");
		public static readonly string Spinner = PathHelper.GetFixturePath("SpinnerTest.vpx");
		public static readonly string Surface = PathHelper.GetFixturePath("SurfaceTest.vpx");
		public static readonly string Table = PathHelper.GetFixturePath("TableTest.vpx");
		public static readonly string TableChecksum = PathHelper.GetFixturePath("TableChecksumTest.vpx");
		public static readonly string TextBox = PathHelper.GetFixturePath("TextboxTest.vpx");
		public static readonly string Texture = PathHelper.GetFixturePath("TextureTest.vpx");
		public static readonly string Timer = PathHelper.GetFixturePath("TimerTest.vpx");
		public static readonly string Trigger = PathHelper.GetFixturePath("TriggerTest.vpx");
	}

	public static class ObjPath
	{
		public static readonly string Bumper = PathHelper.GetFixturePath("BumperTest.obj");
		public static readonly string Flipper = PathHelper.GetFixturePath("FlipperTest.obj");
		public static readonly string Gate = PathHelper.GetFixturePath("GateTest.obj");
		public static readonly string HitTarget = PathHelper.GetFixturePath("HitTargetTest.obj");
		public static readonly string Kicker = PathHelper.GetFixturePath("KickerTest.obj");
		public static readonly string Primitive = PathHelper.GetFixturePath("PrimitiveTest.obj");
		public static readonly string[] PrimitiveAnimated = {
			PathHelper.GetFixturePath("PrimitiveAnimated_00000.obj"),
			PathHelper.GetFixturePath("PrimitiveAnimated_00001.obj"),
			PathHelper.GetFixturePath("PrimitiveAnimated_00002.obj"),
			PathHelper.GetFixturePath("PrimitiveAnimated_00003.obj"),
			PathHelper.GetFixturePath("PrimitiveAnimated_00004.obj"),
			PathHelper.GetFixturePath("PrimitiveAnimated_00005.obj"),
			PathHelper.GetFixturePath("PrimitiveAnimated_00006.obj")
		};
		public static readonly string PrimitiveCompressed = PathHelper.GetFixturePath("PrimitiveCompressed.obj");
		public static readonly string Ramp = PathHelper.GetFixturePath("RampTest.obj");
		public static readonly string Rubber = PathHelper.GetFixturePath("RubberTest.obj");
		public static readonly string Spinner = PathHelper.GetFixturePath("SpinnerTest.obj");
		public static readonly string Surface = PathHelper.GetFixturePath("SurfaceTest.obj");
		public static readonly string Table = PathHelper.GetFixturePath("TableTest.obj");
		public static readonly string Trigger = PathHelper.GetFixturePath("TriggerTest.obj");
	}

	public static class TexturePath
	{
		public static readonly string Exr = PathHelper.GetFixturePath("comp_piz.exr");
		public static readonly string Bmp = PathHelper.GetFixturePath("test_pattern.bmp");
		public static readonly string BmpArgb = PathHelper.GetFixturePath("test_pattern_argb.bmp");
		public static readonly string BmpXrgb = PathHelper.GetFixturePath("test_pattern_xrgb.bmp");
		public static readonly string Jpg = PathHelper.GetFixturePath("test_pattern.jpg");
		public static readonly string Png = PathHelper.GetFixturePath("test_pattern.png");
		public static readonly string PngTransparent = PathHelper.GetFixturePath("test_pattern_transparent.png");
		public static readonly string Hdr = PathHelper.GetFixturePath("test_pattern_hdr.hdr");
	}

	public static class PathHelper
	{
		public static string GetFixturePath(string filename)
		{
			return Path.GetFullPath(Path.Combine(GetTestPath(),
				"Fixtures~" + Path.DirectorySeparatorChar,
				filename));
		}

		private static string GetTestPath()
		{
			var codeBase = new System.Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath;

			if (codeBase.Contains("/Library/ScriptAssemblies/"))
			{
				return Path.GetFullPath(
					"Packages/org.visualpinball.engine.unity/VisualPinball.Engine.Test");
			}
			else if (codeBase.Contains("VisualPinball.Unity.Test"))
			{
				return Path.GetFullPath(
					Path.Combine(
						Path.GetDirectoryName(codeBase),
						"..",
						"..",
						"..",
						"..",
						"..",
						"VisualPinball.Engine.Test")
					);
			}
			else
			{
				return Path.GetFullPath(
					Path.Combine(
						Path.GetDirectoryName(codeBase),
						"..",
						"..",
						"..")
					);
			}
		}
	}
}
