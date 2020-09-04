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

namespace VisualPinball.Unity.Editor
{
	public class ImageListData : IManagerListData
	{
		[ManagerListColumn(Order = 0, Width = 200)]
		public string Name => TextureData?.Name ?? "";
		[ManagerListColumn(Order = 1, Width = 200)]
		public string Path => TextureData?.Path ?? "";
		[ManagerListColumn(Order = 2, HeaderName = "Image Size", Width = 100)]
		public string ImageSize => TextureData == null ? "" : $"{TextureData.Width}x{TextureData.Height}";
		[ManagerListColumn(Order = 3, HeaderName = "In Use", Width = 50)]
		public bool InUse;
		[ManagerListColumn(Order = 4, HeaderName = "Raw Size", Width = 100)]
		public int RawSize { get {
				if (TextureData == null) { return 0; }
				if (TextureData.HasBitmap) {
					return TextureData.Bitmap.Data.Length;
				}
				return TextureData.Binary.Bytes?.Length ?? 0;
			} }

		public Engine.VPT.TextureData TextureData;
	}
}
