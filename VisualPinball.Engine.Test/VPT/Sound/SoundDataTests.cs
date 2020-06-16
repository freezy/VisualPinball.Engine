using FluentAssertions;
using NUnit.Framework;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Sound;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.Test.VPT.Sound
{
	public class SoundDataTests
	{
		[Test]
		public void ShouldReadSoundData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Sound);
			ValidateSoundData(table.Sounds["fx_bumper3"].Data);
		}

		[Test]
		public void ShouldWriteSoundData()
		{
			const string tmpFileName = "ShouldWriteSoundData.vpx";
			var table = Engine.VPT.Table.Table.Load(VpxPath.Sound);
			new TableWriter(table).WriteTable(tmpFileName);
			var writtenTable = Engine.VPT.Table.Table.Load(tmpFileName);
			ValidateSoundData(writtenTable.Sounds["fx_bumper3"].Data);
		}

		private static void ValidateSoundData(SoundData data)
		{
			data.Balance.Should().Be(29);
			data.Data.Length.Should().Be(11940);
			data.Fade.Should().Be(28);
			data.InternalName.Should().Be("fx_bumper3");
			data.OutputTarget.Should().Be(SoundOutTypes.Backglass);
			data.Path.Should().Be(@"C:\VPX\sounds\fx_bumper3.wav");
			data.Volume.Should().Be(-37);

			data.Wfx.AvgBytesPerSec.Should().Be(44100U);
			data.Wfx.BitsPerSample.Should().Be(16);
			data.Wfx.BlockAlign.Should().Be(2);
			data.Wfx.CbSize.Should().Be(0);
			data.Wfx.Channels.Should().Be(1);
			data.Wfx.FormatTag.Should().Be(1);
			data.Wfx.SamplesPerSec.Should().Be(22050U);
		}
	}
}
