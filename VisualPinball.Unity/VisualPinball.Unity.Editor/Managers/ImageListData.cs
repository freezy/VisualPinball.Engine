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

namespace VisualPinball.Unity.Editor
{
	public class ImageListData : IManagerListData
	{
		[ManagerListColumn(Order = 0, Width = 200)]
		public string Name => LegacyTexture.Name;

		[ManagerListColumn(Order = 1, Width = 300)]
		public string Path => LegacyTexture.IsSet ? UnityEditor.AssetDatabase.GetAssetPath(LegacyTexture.Texture) : string.Empty;

		[ManagerListColumn(Order = 2, HeaderName = "Image Size", Width = 100)]
		public string ImageSize => LegacyTexture.IsSet ? $"{LegacyTexture.Texture.width}x{LegacyTexture.Texture.height}" : string.Empty;

		[ManagerListColumn(Order = 3, HeaderName = "In Use", Width = 50)]
		public bool InUse;

		[ManagerListColumn(Order = 4, HeaderName = "Raw Size", Width = 100)]
		public long RawSize => LegacyTexture.IsSet ? LegacyTexture.Texture.width * LegacyTexture.Texture.height * 4 : 0;

		public readonly LegacyTexture LegacyTexture;

		public ImageListData(LegacyTexture legacyTexture, bool inUse)
		{
			LegacyTexture = legacyTexture;
			InUse = inUse;
		}

	}
}
