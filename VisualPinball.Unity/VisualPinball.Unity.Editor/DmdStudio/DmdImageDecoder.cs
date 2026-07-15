// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	internal sealed class DmdDecodedImage
	{
		public DmdBitmapData Bitmap;
		public bool ContainsColor;
		public int DistinctIntensities;
		public int[] IntensityHistogram;
	}

	internal static class DmdImageDecoder
	{
		public static List<DmdDecodedImage> DecodePng(string path, DmdColorMode colorMode,
			int cellWidth = 0, int cellHeight = 0, byte alphaThreshold = 0)
		{
			if (string.IsNullOrWhiteSpace(path)) {
				throw new ArgumentException("A PNG path is required.", nameof(path));
			}
			if (!File.Exists(path)) {
				throw new FileNotFoundException("PNG file not found.", path);
			}

			var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
			try {
				if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path), false)) {
					throw new InvalidDataException($"Could not decode PNG \"{path}\".");
				}
				return Decode(texture.GetPixels32(), texture.width, texture.height, colorMode,
					cellWidth, cellHeight, alphaThreshold);
			} finally {
				UnityEngine.Object.DestroyImmediate(texture);
			}
		}

		private static List<DmdDecodedImage> Decode(Color32[] source, int sourceWidth, int sourceHeight,
			DmdColorMode colorMode, int cellWidth, int cellHeight, byte alphaThreshold)
		{
			if (cellWidth == 0 && cellHeight == 0) {
				cellWidth = sourceWidth;
				cellHeight = sourceHeight;
			} else if (cellWidth <= 0 || cellHeight <= 0) {
				throw new ArgumentException("Sprite-sheet cell width and height must both be positive.");
			}
			if (cellWidth > DmdValidation.MaxWidth || cellHeight > DmdValidation.MaxHeight) {
				throw new ArgumentOutOfRangeException(nameof(cellWidth),
					$"DMD images cannot exceed {DmdValidation.MaxWidth}x{DmdValidation.MaxHeight} pixels.");
			}
			if (sourceWidth % cellWidth != 0 || sourceHeight % cellHeight != 0) {
				throw new ArgumentException(
					$"PNG dimensions {sourceWidth}x{sourceHeight} are not divisible by cell size {cellWidth}x{cellHeight}.");
			}

			var columns = sourceWidth / cellWidth;
			var rows = sourceHeight / cellHeight;
			var frameCount = checked(columns * rows);
			if (frameCount > DmdValidation.MaxSpriteFrames) {
				throw new ArgumentException($"A sprite cannot exceed {DmdValidation.MaxSpriteFrames} frames.");
			}

			var decoded = new List<DmdDecodedImage>(frameCount);
			for (var row = 0; row < rows; row++) {
				for (var column = 0; column < columns; column++) {
					decoded.Add(DecodeCell(source, sourceWidth, sourceHeight, column * cellWidth,
						row * cellHeight, cellWidth, cellHeight, colorMode, alphaThreshold));
				}
			}
			return decoded;
		}

		private static DmdDecodedImage DecodeCell(Color32[] source, int sourceWidth, int sourceHeight,
			int cellX, int cellYFromTop, int width, int height, DmdColorMode colorMode, byte alphaThreshold)
		{
			var rgb = colorMode == DmdColorMode.Rgb24;
			var bitmap = new DmdBitmapData {
				Width = width,
				Height = height,
				Format = rgb ? DmdPixelFormat.Rgb24 : DmdPixelFormat.I8,
				Pixels = new byte[checked(width * height * (rgb ? 3 : 1))],
				Alpha = new byte[checked(width * height)]
			};
			var histogram = new int[256];
			var containsColor = false;
			for (var y = 0; y < height; y++) {
				var sourceY = sourceHeight - 1 - (cellYFromTop + y);
				for (var x = 0; x < width; x++) {
					var color = source[sourceY * sourceWidth + cellX + x];
					var target = y * width + x;
					containsColor |= color.r != color.g || color.g != color.b;
					var luma = Rec601(color);
				histogram[luma]++;
					if (rgb) {
						var offset = target * 3;
						bitmap.Pixels[offset] = color.r;
						bitmap.Pixels[offset + 1] = color.g;
						bitmap.Pixels[offset + 2] = color.b;
					} else {
						bitmap.Pixels[target] = luma;
					}
					bitmap.Alpha[target] = alphaThreshold == 0
						? color.a
						: color.a >= alphaThreshold ? byte.MaxValue : byte.MinValue;
				}
			}

			var distinct = 0;
			for (var intensity = 0; intensity < histogram.Length; intensity++) {
				if (histogram[intensity] > 0) {
					distinct++;
				}
			}
			return new DmdDecodedImage {
				Bitmap = bitmap,
				ContainsColor = containsColor,
				DistinctIntensities = distinct,
				IntensityHistogram = histogram
			};
		}

		private static byte Rec601(Color32 color)
		{
			return (byte)System.Math.Round(color.r * 0.299d + color.g * 0.587d + color.b * 0.114d,
				MidpointRounding.AwayFromZero);
		}
	}
}
