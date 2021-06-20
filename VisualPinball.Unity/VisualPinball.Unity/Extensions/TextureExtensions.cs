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

using System;
using System.IO;
using UnityEngine;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity
{
	public static class TextureExtensions
	{
		public static Texture2D ToUnityTexture(this Engine.VPT.Texture vpTex)
		{
			if (vpTex.Data.HasBitmap) {
				return FromBitmap(vpTex);
			}
			return vpTex.IsHdr ? FromHdrBinary(vpTex) : FromBinary(vpTex);
		}

		public static string GetUnityFilename(this Engine.VPT.Texture vpTex, string folderName = null)
		{
			var fileName = vpTex.Name.ToNormalizedName() + vpTex.FileExtension;
			return folderName != null
				? Path.Combine(folderName, fileName)
				: fileName;
		}

		private static Texture2D FromBinary(Engine.VPT.Texture vpTex)
		{
			var unityTex = new Texture2D(vpTex.Width, vpTex.Height, TextureFormat.RGBA32, true) {
				name = vpTex.Name
			};
			unityTex.LoadImage(vpTex.FileContent);
			return unityTex;
		}

		private static Texture2D FromHdrBinary(Engine.VPT.Texture vpTex)
		{
			var unityTex = new Texture2D(vpTex.Width, vpTex.Height, TextureFormat.Alpha8, false) {
				name = vpTex.Name
			};
			unityTex.LoadRawTextureData(vpTex.FileContent);
			return unityTex;
		}

		private static Texture2D FromBitmap(Engine.VPT.Texture vpTex)
		{
			var bmp = vpTex.Data.Bitmap;
			var data = bmp.Bytes;
			var pitch = bmp.Pitch();
			var colArr = new Color32[bmp.Width * bmp.Height];
			for (var y = 0; y < bmp.Height; y++) {
				for (var x = 0; x < bmp.Width; x++) {
					if (bmp.Format == Bitmap.RGBA) {
						colArr[y * vpTex.Width + x] = new Color32(
							data[(bmp.Height - y - 1) * pitch + 4 * x],
							data[(bmp.Height - y - 1) * pitch + 4 * x + 1],
							data[(bmp.Height - y - 1) * pitch + 4 * x + 2],
							data[(bmp.Height - y - 1) * pitch + 4 * x + 3]
						);
					} else {
						throw new NotImplementedException();
					}
				}
			}

			var unityTex = new Texture2D(bmp.Width, bmp.Height, TextureFormat.RGBA32, true) {
				name = vpTex.Name
			};
			unityTex.SetPixels32(colArr);
			unityTex.Apply();
			return unityTex;
		}
	}
}
