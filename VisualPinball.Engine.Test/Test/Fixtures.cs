using System.IO;

namespace VisualPinball.Engine.Test.Test
{
	public static class FixturesPath
	{
		public static readonly string Base = $"..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}Fixtures~{Path.DirectorySeparatorChar}";
	}

	public static class VpxPath
	{
		public static readonly string Bumper = FixturesPath.Base + "BumperTest.vpx";
		public static readonly string Collection = FixturesPath.Base + "CollectionTest.vpx";
		public static readonly string Decal = FixturesPath.Base + "DecalTest.vpx";
		public static readonly string DispReel = FixturesPath.Base + "DispReelTest.vpx";
		public static readonly string Flasher = FixturesPath.Base + "FlasherTest.vpx";
		public static readonly string Flipper = FixturesPath.Base + "FlipperTest.vpx";
		public static readonly string Gate = FixturesPath.Base + "GateTest.vpx";
		public static readonly string HitTarget = FixturesPath.Base + "HitTargetTest.vpx";
		public static readonly string Kicker = FixturesPath.Base + "KickerTest.vpx";
		public static readonly string Light = FixturesPath.Base + "LightTest.vpx";
		public static readonly string LightSeq = FixturesPath.Base + "LightSeqTest.vpx";
		public static readonly string Material = FixturesPath.Base + "MaterialTest.vpx";
		public static readonly string MaterialTexture = FixturesPath.Base + "MaterialTextureTest.vpx";
		public static readonly string Plunger = FixturesPath.Base + "PlungerTest.vpx";
		public static readonly string Primitive = FixturesPath.Base + "PrimitiveTest.vpx";
		public static readonly string PrimitiveCompressed = FixturesPath.Base + "PrimitiveCompressed.vpx";
		public static readonly string Ramp = FixturesPath.Base + "RampTest.vpx";
		public static readonly string Rubber = FixturesPath.Base + "RubberTest.vpx";
		public static readonly string Sound = FixturesPath.Base + "SoundTest.vpx";
		public static readonly string Spinner = FixturesPath.Base + "SpinnerTest.vpx";
		public static readonly string Surface = FixturesPath.Base + "SurfaceTest.vpx";
		public static readonly string Table = FixturesPath.Base + "TableTest.vpx";
		public static readonly string TableChecksum = FixturesPath.Base + "TableChecksumTest.vpx";
		public static readonly string TextBox = FixturesPath.Base + "TextboxTest.vpx";
		public static readonly string Texture = FixturesPath.Base + "TextureTest.vpx";
		public static readonly string Timer = FixturesPath.Base + "TimerTest.vpx";
		public static readonly string Trigger = FixturesPath.Base + "TriggerTest.vpx";
	}

	public static class ObjPath
	{
		public static readonly string Bumper = FixturesPath.Base + "BumperTest.obj";
		public static readonly string Flipper = FixturesPath.Base + "FlipperTest.obj";
		public static readonly string Gate = FixturesPath.Base + "GateTest.obj";
		public static readonly string HitTarget = FixturesPath.Base + "HitTargetTest.obj";
		public static readonly string Kicker = FixturesPath.Base + "KickerTest.obj";
		public static readonly string Primitive = FixturesPath.Base + "PrimitiveTest.obj";
		public static readonly string PrimitiveCompressed = FixturesPath.Base + "PrimitiveCompressed.obj";
		public static readonly string Ramp = FixturesPath.Base + "RampTest.obj";
		public static readonly string Rubber = FixturesPath.Base + "RubberTest.obj";
		public static readonly string Spinner = FixturesPath.Base + "SpinnerTest.obj";
		public static readonly string Surface = FixturesPath.Base + "SurfaceTest.obj";
		public static readonly string Table = FixturesPath.Base + "TableTest.obj";
		public static readonly string Trigger = FixturesPath.Base + "TriggerTest.obj";
	}

	public static class TexturePath
	{
		public static readonly string Exr = FixturesPath.Base + "comp_piz.exr";
		public static readonly string Bmp = FixturesPath.Base + "test_pattern.bmp";
		public static readonly string BmpArgb = FixturesPath.Base + "test_pattern_argb.bmp";
		public static readonly string BmpXrgb = FixturesPath.Base + "test_pattern_xrgb.bmp";
		public static readonly string Jpg = FixturesPath.Base + "test_pattern.jpg";
		public static readonly string Png = FixturesPath.Base + "test_pattern.png";
		public static readonly string PngTransparent = FixturesPath.Base + "test_pattern_transparent.png";
		public static readonly string Hdr = FixturesPath.Base + "test_pattern_hdr.hdr";
	}
}
