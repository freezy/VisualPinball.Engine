﻿using System;
using UnityEngine;
using VisualPinball.Engine.VPT;
using VisualPinball.Unity.Importer;

namespace VisualPinball.Unity.Extensions
{
	public static class Texture
	{
		public static Texture2D ToUnityTexture(this Engine.VPT.Texture vpTex)
		{
			Texture2D unityTex;
			if (vpTex.Data.Bitmap != null) {
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
				unityTex = new Texture2D(bmp.Width, bmp.Height);
				unityTex.SetPixels32(colArr);
				unityTex.Apply();

			} else {
				unityTex = new Texture2D(vpTex.Width, vpTex.Height);
				unityTex.LoadImage(vpTex.FileContent);
			}
			return unityTex;
		}

		public static string GetUnityFilename(this Engine.VPT.Texture vpTex, string folderName = null)
		{
			return folderName != null
				? $"{folderName}/{AssetUtility.StringToFilename(vpTex.Name)}.asset"
				: $"{AssetUtility.StringToFilename(vpTex.Name)}.asset";
		}
	}
}
