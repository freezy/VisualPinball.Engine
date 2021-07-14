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

using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity.Editor
{
	class SoundListData : IManagerListData
	{
		[ManagerListColumn(Order = 0, Width = 200)]
		public string Name => LegacySound.Name;
		[ManagerListColumn(Order = 1, Width = 200)]
		public string Path => LegacySound.IsSet ? UnityEditor.AssetDatabase.GetAssetPath(LegacySound.AudioClip) : string.Empty;
		[ManagerListColumn(Order = 2, HeaderName = "Output Target", Width = 100)]
		public string Output => LegacySound == null ? "" : $"{(LegacySound.OutputTarget == SoundOutTypes.Table ? "Table" : "BackGlass")}";
		[ManagerListColumn(Order = 3, HeaderName = "Volume", Width = 100)]
		public string Volume => LegacySound == null ? "" : $"{LegacySound.Volume.PercentageToRatio()}";
		[ManagerListColumn(Order = 4, HeaderName = "Balance", Width = 100)]
		public string Balance => LegacySound == null ? "" : $"{LegacySound.Balance.PercentageToRatio()}";
		[ManagerListColumn(Order = 5, HeaderName = "Fade", Width = 100)]
		public string Fade => LegacySound == null ? "" : $"{LegacySound.Fade.PercentageToRatio()}";

		public readonly LegacySound LegacySound;

		public SoundListData(LegacySound legacySound)
		{
			LegacySound = legacySound;
		}
	}
}
