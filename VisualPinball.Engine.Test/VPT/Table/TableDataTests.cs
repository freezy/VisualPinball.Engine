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
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.Test.VPT.Table
{
	public class TableDataTests : BaseTests
	{
		[Test]
		public void ShouldReadTableData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Table);
			ValidateTableData(table.Data);
		}

		[Test]
		public void ShouldReadTableInfo()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Table);

			table.InfoAuthorEmail.Should().Be("test@vpdb.io");
			table.InfoAuthorName.Should().Be("Table Author");
			table.InfoAuthorWebsite.Should().Be("https://vpdb.io");
			table.InfoReleaseDate.Should().Be("2019-04-14");
			table.InfoBlurb.Should().Be("Short Blurb");
			table.InfoDescription.Should().Be("Description");
			table.InfoName.Should().Be("Table Name");
			table.InfoRules.Should().Be("Rules");
			table.InfoVersion.Should().Be("Version");
			table.TableInfo["customdata1"].Should().Be("customvalue1");
		}

		[Test]
		public void ShouldWriteTableData()
		{
			const string tmpFileName = "ShouldWriteTable.vpx";
			var table = Engine.VPT.Table.Table.Load(VpxPath.Table);
			new TableWriter(table).WriteTable(tmpFileName);
			var writtenTable = Engine.VPT.Table.Table.Load(tmpFileName);
			ValidateTableData(writtenTable.Data);
		}

		[Test]
		public void ShouldWriteCorrectHash()
		{
			const string tmpFileName = "ShouldWriteCorrectHash.vpx";
			var table = Engine.VPT.Table.Table.Load(VpxPath.TableChecksum);
			new TableWriter(table).WriteTable(tmpFileName);
			var writtenTable = Engine.VPT.Table.Table.Load(tmpFileName);

			writtenTable.FileHash.Should().Equal(table.FileHash);
		}

		[Test]
		public void ShouldReadCustomInfoTags()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Table);
			table.CustomInfoTags.TagNames[0].Should().Be("customdata1");
			table.CustomInfoTags.TagNames[1].Should().Be("foo");
		}

		private static void ValidateTableData(TableData data)
		{
			data.AngleTiltMax.Should().Be(0.60606f);
			data.AngleTiltMin.Should().Be(0.2033f);
			data.AoScale.Should().Be(1.23f);
			data.BallDecalMode.Should().Be(true);
			data.BallImage.Should().Be("test_pattern");
			data.BallImageFront.Should().Be("");
			data.BallPlayfieldReflectionStrength.Should().Be(2.3005f);
			data.BallTrailStrength.Should().Be(0.129411772f);
			data.BgEnableFss.Should().Be(false); // ???
			data.BgFov[BackglassIndex.Desktop].Should().Be(62f);
			data.BgFov[BackglassIndex.Fullscreen].Should().Be(17.1f);
			data.BgFov[BackglassIndex.FullSingleScreen].Should().Be(65.2f);
			data.BgImage[BackglassIndex.Desktop].Should().Be("test_pattern");
			data.BgImage[BackglassIndex.Fullscreen].Should().Be("");
			data.BgImage[BackglassIndex.FullSingleScreen].Should().Be("test_pattern");
			data.BgInclination[BackglassIndex.Desktop].Should().Be(46f);
			data.BgInclination[BackglassIndex.Fullscreen].Should().Be(15.1f);
			data.BgInclination[BackglassIndex.FullSingleScreen].Should().Be(45.2f);
			data.BgLayback[BackglassIndex.Desktop].Should().Be(2.1f);
			data.BgLayback[BackglassIndex.Fullscreen].Should().Be(36.1f);
			data.BgLayback[BackglassIndex.FullSingleScreen].Should().Be(0.2f);
			data.BgRotation[BackglassIndex.Desktop].Should().Be(12f);
			data.BgRotation[BackglassIndex.Fullscreen].Should().Be(270.1f);
			data.BgRotation[BackglassIndex.FullSingleScreen].Should().Be(0.3f);
			data.BgScaleX[BackglassIndex.Desktop].Should().Be(1.2023f);
			data.BgScaleX[BackglassIndex.Fullscreen].Should().Be(1.21f);
			data.BgScaleX[BackglassIndex.FullSingleScreen].Should().Be(1.22f);
			data.BgScaleY[BackglassIndex.Desktop].Should().Be(1.1812f);
			data.BgScaleY[BackglassIndex.Fullscreen].Should().Be(1.11f);
			data.BgScaleY[BackglassIndex.FullSingleScreen].Should().Be(1.12f);
			data.BgScaleZ[BackglassIndex.Desktop].Should().Be(1.3211f);
			data.BgScaleZ[BackglassIndex.Fullscreen].Should().Be(1.41f);
			data.BgScaleZ[BackglassIndex.FullSingleScreen].Should().Be(12f);
			data.BgOffsetX[BackglassIndex.Desktop].Should().Be(12.33f);
			data.BgOffsetX[BackglassIndex.Fullscreen].Should().Be(0.1f);
			data.BgOffsetX[BackglassIndex.FullSingleScreen].Should().Be(0.2f);
			data.BgOffsetY[BackglassIndex.Desktop].Should().Be(100.43f);
			data.BgOffsetY[BackglassIndex.Fullscreen].Should().Be(90.1f);
			data.BgOffsetY[BackglassIndex.FullSingleScreen].Should().Be(0.2332f);
			data.BgOffsetZ[BackglassIndex.Desktop].Should().Be(-50.092f);
			data.BgOffsetZ[BackglassIndex.Fullscreen].Should().Be(400.1f);
			data.BgOffsetZ[BackglassIndex.FullSingleScreen].Should().Be(-50.223f);
			data.BloomStrength.Should().Be(1.5055f);
			data.Bottom.Should().Be(2224);
			data.Code.Should().Be("Option Explicit\r\n");
			data.ColorBackdrop.Red.Should().Be(31);
			data.ColorBackdrop.Green.Should().Be(32);
			data.ColorBackdrop.Blue.Should().Be(33);
			data.CustomColors[0].Red.Should().Be(255);
			data.CustomColors[0].Green.Should().Be(197);
			data.CustomColors[0].Blue.Should().Be(143);
			data.DefaultBulbIntensityScaleOnBall.Should().Be(3.3028389f);
			data.DefaultScatter.Should().Be(3.2908f);
			data.DisplayBackdrop.Should().Be(true);
			data.Elasticity.Should().Be(0.23f);
			data.ElasticityFalloff.Should().Be(1.3098f);
			data.EnvEmissionScale.Should().Be(2.29841f);
			data.EnvImage.Should().Be("");
			data.Friction.Should().Be(0.076f);
			data.GlassHeight.Should().Be(404f);
			data.GlobalDifficulty.Should().Be(0.03f);
			data.GlobalEmissionScale.Should().Be(0.55f);
			(data.Gravity * (float) (1.0 / Constants.Gravity)).Should().Be(0.8f);
			data.Image.Should().Be("test_pattern");
			data.ImageBackdropNightDay.Should().Be(false);
			data.ImageColorGrade.Should().Be("ColorGradeLUT256x16_ConSat");
			data.Left.Should().Be(0f); // set in debug mode
			data.Light[0].Emission.Red.Should().Be(255);
			data.Light[0].Emission.Green.Should().Be(255);
			data.Light[0].Emission.Blue.Should().Be(17);
			data.LightAmbient.Red.Should().Be(23);
			data.LightAmbient.Green.Should().Be(221);
			data.LightAmbient.Blue.Should().Be(142);
			data.LightEmissionScale.Should().Be(4020012f);
			data.LightHeight.Should().Be(5023f);
			data.LightRange.Should().Be(4040001f);
			data.Materials.Length.Should().Be(1);
			data.Materials[0].Name.Should().Be("Material1");
			data.Name.Should().Be("TestTable");
			data.NudgeTime.Should().Be(6.2931f);
			data.NumCollections.Should().Be(0);
			data.NumFonts.Should().Be(0);
			data.NumGameItems.Should().Be(1);
			data.NumMaterials.Should().Be(1);
			data.NumSounds.Should().Be(0);
			data.NumTextures.Should().Be(1);
			data.OverridePhysics.Should().Be(1);
			data.OverridePhysicsFlipper.Should().Be(true);
			data.OverwriteGlobalDayNight.Should().Be(true);
			data.OverwriteGlobalDetailLevel.Should().Be(true);
			data.OverwriteGlobalStereo3D.Should().Be(true);
			data.PhysicsMaxLoops.Should().Be(12203);
			data.PlayfieldMaterial.Should().Be("Material1");
			data.PlayfieldReflectionStrength.Should().Be(0.129411772f);
			data.PlungerFilter.Should().Be(false);
			data.PlungerNormalize.Should().Be(34);
			data.ReflectElementsOnPlayfield.Should().Be(true);
			data.RenderDecals.Should().Be(true);
			data.RenderEmReels.Should().Be(true);
			data.Right.Should().Be(1112f);
			data.Scatter.Should().Be(2.02f);
			data.ScreenShot.Should().Be("");
			data.ShowGrid.Should().Be(true);
			data.SsrScale.Should().Be(0.4123f);
			data.StereoMaxSeparation.Should().Be(0.01435f);
			data.StereoOffset.Should().Be(0.002f);
			data.StereoZeroParallaxDisplacement.Should().Be(0.53f);
			data.TableAdaptiveVSync.Should().Be(6649);
			data.TableHeight.Should().Be(2.009231f);
			data.TableMusicVolume.Should().Be(0.24f);
			data.TableSoundVolume.Should().Be(0.23f);
			data.Top.Should().Be(0f); // set in debug mode
			data.UseAA.Should().Be(0);
			data.UseAO.Should().Be(0);
			data.UseFXAA.Should().Be(1);
			data.UserDetailLevel.Should().Be(7);
			data.UseReflectionForBalls.Should().Be(0);
			data.UseSSR.Should().Be(0);
			data.UseTrailForBalls.Should().Be(0);
			data.Zoom.Should().Be(0.5f); // set in debug mode

			// these change whether the file was saved with backglass or playfield active
			data.Offset[0].Should().Be(556f);
			data.Offset[1].Should().Be(1112f);
		}
	}
}
