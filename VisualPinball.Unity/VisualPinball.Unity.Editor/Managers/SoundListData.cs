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
