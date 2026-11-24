// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Test
{
	public class NormalRepackComputeTests
	{
		[Test]
		public void LargerSourceIsShrunkNotCropped()
		{
			// 4x4 normal map: red is set in the right half, green in the top half (texel rows run bottom-up)
			var result = Repack(4, 4, (x, y) => new Color32((byte)(x >= 2 ? 255 : 0), (byte)(y >= 2 ? 255 : 0), 0, 255), 2, 2);
			// the AG layout keeps x in alpha and y in green; each texel is the whole 2x2 block it covers
			Assert.That(result[1].a, Is.EqualTo(255), "bottom right holds the right half's x");
			Assert.That(result[1].g, Is.EqualTo(0), "bottom right holds the bottom half's y");
			Assert.That(result[2].a, Is.EqualTo(0), "top left holds the left half's x");
			Assert.That(result[2].g, Is.EqualTo(255), "top left holds the top half's y");
		}

		[Test]
		public void TheEdgeIsKeptWhenTheSizesAreNotAWholeRatioApart()
		{
			// 3x1 normal map with red only in its last column, shrunk to 2x1
			var result = Repack(3, 1, (x, _) => new Color32((byte)(x == 2 ? 255 : 0), 0, 0, 255), 2, 1);
			Assert.That(result[0].a, Is.EqualTo(0), "the left texel covers the first column only");
			Assert.That(result[1].a, Is.InRange(120, 135), "the right texel averages the last two columns");
		}

		private static Color32[] Repack(int width, int height, Func<int, int, Color32> pixel, int cookedWidth, int cookedHeight)
		{
			var repack = new VpeNormalRepackCompute();
			if (!repack.Initialize()) Assert.Ignore("Compute shaders are not available.");
			var source = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
			var dest = new RenderTexture(cookedWidth, cookedHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear) { enableRandomWrite = true };
			var read = new Texture2D(cookedWidth, cookedHeight, TextureFormat.RGBA32, false, true);
			var active = RenderTexture.active;
			try {
				var pixels = new Color32[width * height];
				for (var y = 0; y < height; y++) {
					for (var x = 0; x < width; x++) pixels[y * width + x] = pixel(x, y);
				}
				source.SetPixels32(pixels);
				source.Apply(false);
				dest.Create();
				repack.Repack(source, dest, cookedWidth, cookedHeight);
				RenderTexture.active = dest;
				read.ReadPixels(new Rect(0, 0, cookedWidth, cookedHeight), 0, 0, false);
				read.Apply(false);
				return read.GetPixels32();
			} finally {
				RenderTexture.active = active;
				Object.DestroyImmediate(source);
				Object.DestroyImmediate(dest);
				Object.DestroyImmediate(read);
			}
		}
	}
}
