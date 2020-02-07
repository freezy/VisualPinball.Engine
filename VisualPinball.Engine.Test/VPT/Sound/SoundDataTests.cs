using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Sound;
using VisualPinball.Engine.VPT.Table;
using Xunit;

namespace VisualPinball.Engine.Test.VPT.Sound
{
	public class SoundDataTests
	{
		[Fact]
		public void ShouldReadSoundData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Sound);
			ValidateSoundData(table.Sounds["fx_bumper3"].Data);
		}

		[Fact]
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
			Assert.Equal(29, data.Balance);
			Assert.Equal(11940, data.Data.Length);
			Assert.Equal(28, data.Fade);
			Assert.Equal("fx_bumper3", data.InternalName);
			Assert.Equal(SoundOutTypes.Backglass, data.OutputTarget);
			Assert.Equal(@"C:\VPX\sounds\fx_bumper3.wav", data.Path);
			Assert.Equal(-37, data.Volume);

			Assert.Equal(44100U, data.Wfx.AvgBytesPerSec);
			Assert.Equal(16U, data.Wfx.BitsPerSample);
			Assert.Equal(2U, data.Wfx.BlockAlign);
			Assert.Equal(0, data.Wfx.CbSize);
			Assert.Equal(1U, data.Wfx.Channels);
			Assert.Equal(1U, data.Wfx.FormatTag);
			Assert.Equal(22050U, data.Wfx.SamplesPerSec);
		}
	}
}
