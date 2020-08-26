using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity.Editor
{
	class SoundListData : IManagerListData
	{
		[ManagerListColumn(Order = 0, Width = 200)]
		public string Name => SoundData?.Name ?? "";
		[ManagerListColumn(Order = 1, Width = 200)]
		public string Path => SoundData?.Path ?? "";
		[ManagerListColumn(Order = 2, HeaderName = "Output Target", Width = 100)]
		public string Output => SoundData == null ? "" : $"{(SoundData.OutputTarget == SoundOutTypes.Table ? "Table" : "BackGlass")}";
		[ManagerListColumn(Order = 3, HeaderName = "Volume", Width = 100)]
		public string Volume => SoundData == null ? "" : $"{SoundData.Volume.PercentageToRatio()}";
		[ManagerListColumn(Order = 4, HeaderName = "Balance", Width = 100)]
		public string Balance => SoundData == null ? "" : $"{SoundData.Balance.PercentageToRatio()}";
		[ManagerListColumn(Order = 5, HeaderName = "Fade", Width = 100)]
		public string Fade => SoundData == null ? "" : $"{SoundData.Fade.PercentageToRatio()}";

		public Engine.VPT.Sound.SoundData SoundData;
	}
}
