using System.Text;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Table;
using Xunit;
using Xunit.Abstractions;

namespace VisualPinball.Engine.Test.VPT.Table
{
	public class TableDataTests : BaseTests
	{
		public TableDataTests(ITestOutputHelper output) : base(output) { }

		[Fact]
		public void ShouldLoadCorrectData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Table);
			ValidateTableData(table.Data);
		}

		[Fact]
		public void ShouldLoadCorrectTableInfo()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Table);

			Assert.Equal("test@vpdb.io", table.InfoAuthorEmail);
			Assert.Equal("Table Author", table.InfoAuthorName);
			Assert.Equal("https://vpdb.io", table.InfoAuthorWebsite);
			Assert.Equal("2019-04-14", table.InfoReleaseDate);
			Assert.Equal("Short Blurb", table.InfoBlurb);
			Assert.Equal("Description", table.InfoDescription);
			Assert.Equal("Table Name", table.InfoName);
			Assert.Equal("Rules", table.InfoRules);
			Assert.Equal("Version", table.InfoVersion);
			Assert.Equal("customvalue1", table.TableInfo["customdata1"]);
		}

		[Fact]
		public void ShouldWriteTable()
		{
			const string tmpFileName = "ShouldWriteTable.vpx";
			var table = Engine.VPT.Table.Table.Load(VpxPath.Table);
			new TableWriter(table).WriteTable(tmpFileName);
			var writtenTable = Engine.VPT.Table.Table.Load(tmpFileName);
			ValidateTableData(writtenTable.Data);
		}

		[Fact]
		public void ShouldWriteCorrectHash()
		{
			const string tmpFileName = "ShouldWriteCorrectHash.vpx";
			var table = Engine.VPT.Table.Table.Load(VpxPath.TableChecksum);
			new TableWriter(table).WriteTable(tmpFileName);
			var writtenTable = Engine.VPT.Table.Table.Load(tmpFileName);

			Assert.Equal(table.FileHash, writtenTable.FileHash);
		}

		[Fact]
		public void ShouldReadCustomInfoTags()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Table);
			Assert.Equal("customdata1", table.CustomInfoTags.TagNames[0]);
			Assert.Equal("foo", table.CustomInfoTags.TagNames[1]);
		}

		private static void ValidateTableData(TableData data)
		{
			Assert.Equal(0.60606f, data.AngleTiltMax);
			Assert.Equal(0.2033f, data.AngleTiltMin);
			Assert.Equal(1.23f, data.AoScale);
			Assert.Equal(true, data.BallDecalMode);
			Assert.Equal("test_pattern", data.BallImage);
			Assert.Equal("", data.BallImageFront);
			Assert.Equal(2.3005f, data.BallPlayfieldReflectionStrength);
			Assert.Equal(0.129411772f, data.BallTrailStrength);
			Assert.Equal(false, data.BgEnableFss); // ???
			Assert.Equal(62f, data.BgFov[BackglassIndex.Desktop]);
			Assert.Equal(17.1f, data.BgFov[BackglassIndex.Fullscreen]);
			Assert.Equal(65.2f, data.BgFov[BackglassIndex.FullSingleScreen]);
			Assert.Equal("test_pattern", data.BgImage[BackglassIndex.Desktop]);
			Assert.Equal("", data.BgImage[BackglassIndex.Fullscreen]);
			Assert.Equal("test_pattern", data.BgImage[BackglassIndex.FullSingleScreen]);
			Assert.Equal(46f, data.BgInclination[BackglassIndex.Desktop]);
			Assert.Equal(15.1f, data.BgInclination[BackglassIndex.Fullscreen]);
			Assert.Equal(45.2f, data.BgInclination[BackglassIndex.FullSingleScreen]);
			Assert.Equal(2.1f, data.BgLayback[BackglassIndex.Desktop]);
			Assert.Equal(36.1f, data.BgLayback[BackglassIndex.Fullscreen]);
			Assert.Equal(0.2f, data.BgLayback[BackglassIndex.FullSingleScreen]);
			Assert.Equal(12f, data.BgRotation[BackglassIndex.Desktop]);
			Assert.Equal(270.1f, data.BgRotation[BackglassIndex.Fullscreen]);
			Assert.Equal(0.3f, data.BgRotation[BackglassIndex.FullSingleScreen]);
			Assert.Equal(1.2023f, data.BgScaleX[BackglassIndex.Desktop]);
			Assert.Equal(1.21f, data.BgScaleX[BackglassIndex.Fullscreen]);
			Assert.Equal(1.22f, data.BgScaleX[BackglassIndex.FullSingleScreen]);
			Assert.Equal(1.1812f, data.BgScaleY[BackglassIndex.Desktop]);
			Assert.Equal(1.11f, data.BgScaleY[BackglassIndex.Fullscreen]);
			Assert.Equal(1.12f, data.BgScaleY[BackglassIndex.FullSingleScreen]);
			Assert.Equal(1.3211f, data.BgScaleZ[BackglassIndex.Desktop]);
			Assert.Equal(1.41f, data.BgScaleZ[BackglassIndex.Fullscreen]);
			Assert.Equal(12f, data.BgScaleZ[BackglassIndex.FullSingleScreen]);
			Assert.Equal(12.33f, data.BgOffsetX[BackglassIndex.Desktop]);
			Assert.Equal(0.1f, data.BgOffsetX[BackglassIndex.Fullscreen]);
			Assert.Equal(0.2f, data.BgOffsetX[BackglassIndex.FullSingleScreen]);
			Assert.Equal(100.43f, data.BgOffsetY[BackglassIndex.Desktop]);
			Assert.Equal(90.1f, data.BgOffsetY[BackglassIndex.Fullscreen]);
			Assert.Equal(0.2332f, data.BgOffsetY[BackglassIndex.FullSingleScreen]);
			Assert.Equal(-50.092f, data.BgOffsetZ[BackglassIndex.Desktop]);
			Assert.Equal(400.1f, data.BgOffsetZ[BackglassIndex.Fullscreen]);
			Assert.Equal(-50.223f, data.BgOffsetZ[BackglassIndex.FullSingleScreen]);
			Assert.Equal(1.5055f, data.BloomStrength);
			Assert.Equal(2224, data.Bottom);
			Assert.Equal("Option Explicit\r\n", data.Code);
			Assert.Equal(31, data.ColorBackdrop.Red);
			Assert.Equal(32, data.ColorBackdrop.Green);
			Assert.Equal(33, data.ColorBackdrop.Blue);
			Assert.Equal(255, data.CustomColors[0].Red);
			Assert.Equal(197, data.CustomColors[0].Green);
			Assert.Equal(143, data.CustomColors[0].Blue);
			Assert.Equal(3.3028389f, data.DefaultBulbIntensityScaleOnBall);
			Assert.Equal(3.2908f, data.DefaultScatter);
			Assert.Equal(true, data.DisplayBackdrop);
			Assert.Equal(0.23f, data.Elasticity);
			Assert.Equal(1.3098f, data.ElasticityFalloff);
			Assert.Equal(2.29841f, data.EnvEmissionScale);
			Assert.Equal("", data.EnvImage);
			Assert.Equal(0.076f, data.Friction);
			Assert.Equal(404f, data.GlassHeight);
			Assert.Equal(0.03f, data.GlobalDifficulty);
			Assert.Equal(0.55f, data.GlobalEmissionScale);
			Assert.Equal(0.8f, data.Gravity * (float) (1.0 / Constants.Gravity));
			Assert.Equal("test_pattern", data.Image);
			Assert.Equal(false, data.ImageBackdropNightDay);
			Assert.Equal("ColorGradeLUT256x16_ConSat", data.ImageColorGrade);
			Assert.Equal(0f, data.Left); // set in debug mode
			Assert.Equal(255, data.Light[0].Emission.Red);
			Assert.Equal(255, data.Light[0].Emission.Green);
			Assert.Equal(17, data.Light[0].Emission.Blue);
			Assert.Equal(23, data.LightAmbient.Red);
			Assert.Equal(221, data.LightAmbient.Green);
			Assert.Equal(142, data.LightAmbient.Blue);
			Assert.Equal(4020012f, data.LightEmissionScale);
			Assert.Equal(5023f, data.LightHeight);
			Assert.Equal(4040001f, data.LightRange);
			Assert.Equal(1, data.Materials.Length);
			Assert.Equal("Material1", data.Materials[0].Name);
			Assert.Equal("TestTable", data.Name);
			Assert.Equal(6.2931f, data.NudgeTime);
			Assert.Equal(0, data.NumCollections);
			Assert.Equal(0, data.NumFonts);
			Assert.Equal(1, data.NumGameItems);
			Assert.Equal(1, data.NumMaterials);
			Assert.Equal(0, data.NumSounds);
			Assert.Equal(1, data.NumTextures);
			Assert.Equal(true, data.OverridePhysics);
			Assert.Equal(true, data.OverridePhysicsFlipper);
			Assert.Equal(true, data.OverwriteGlobalDayNight);
			Assert.Equal(true, data.OverwriteGlobalDetailLevel);
			Assert.Equal(true, data.OverwriteGlobalStereo3D);
			Assert.Equal(12203, data.PhysicsMaxLoops);
			Assert.Equal("Material1", data.PlayfieldMaterial);
			Assert.Equal(0.129411772f, data.PlayfieldReflectionStrength);
			Assert.Equal(false, data.PlungerFilter);
			Assert.Equal(34, data.PlungerNormalize);
			Assert.Equal(true, data.ReflectElementsOnPlayfield);
			Assert.Equal(true, data.RenderDecals);
			Assert.Equal(true, data.RenderEmReels);
			Assert.Equal(1112f, data.Right);
			Assert.Equal(2.02f, data.Scatter);
			Assert.Equal("", data.ScreenShot);
			Assert.Equal(true, data.ShowGrid);
			Assert.Equal(0.4123f, data.SsrScale);
			Assert.Equal(0.01435f, data.StereoMaxSeparation);
			Assert.Equal(0.002f, data.StereoOffset);
			Assert.Equal(0.53f, data.StereoZeroParallaxDisplacement);
			Assert.Equal(6649, data.TableAdaptiveVSync);
			Assert.Equal(2.009231f, data.TableHeight);
			Assert.Equal(0.24f, data.TableMusicVolume);
			Assert.Equal(0.23f, data.TableSoundVolume);
			Assert.Equal(0f, data.Top); // set in debug mode
			Assert.Equal(0, data.UseAA);
			Assert.Equal(0, data.UseAO);
			Assert.Equal(1, data.UseFXAA);
			Assert.Equal(7, data.UserDetailLevel);
			Assert.Equal(0, data.UseReflectionForBalls);
			Assert.Equal(0, data.UseSSR);
			Assert.Equal(0, data.UseTrailForBalls);
			Assert.Equal(0.5f, data.Zoom); // set in debug mode

			// these change whether the file was saved with backglass or playfield active
			Assert.Equal(556f, data.Offset[0]);
			Assert.Equal(1112f, data.Offset[1]);
		}
	}
}
