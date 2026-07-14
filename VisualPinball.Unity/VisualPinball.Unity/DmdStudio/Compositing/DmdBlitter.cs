// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity
{
	public static class DmdBlitter
	{
		public static void Blit(DmdSurface dst, DmdBitmapData src, int x, int y,
			DmdBlendMode mode, byte opacity, Color32 tint)
		{
			if (dst == null) {
				throw new ArgumentNullException(nameof(dst));
			}
			ValidateSource(src);
			if (dst.Format == DmdPixelFormat.I8 && src.Format != DmdPixelFormat.I8) {
				throw new ArgumentException("RGB24 sources cannot be blitted to an I8 surface.", nameof(src));
			}

			var sourceStartX = (int)System.Math.Min(src.Width, System.Math.Max(0L, -(long)x));
			var sourceStartY = (int)System.Math.Min(src.Height, System.Math.Max(0L, -(long)y));
			var sourceEndX = (int)System.Math.Max(0L, System.Math.Min(src.Width, (long)dst.Width - x));
			var sourceEndY = (int)System.Math.Max(0L, System.Math.Min(src.Height, (long)dst.Height - y));
			if (sourceStartX >= sourceEndX || sourceStartY >= sourceEndY) {
				return;
			}

			var hasAlpha = src.Alpha != null && src.Alpha.Length != 0;
			for (var sourceY = sourceStartY; sourceY < sourceEndY; sourceY++) {
				for (var sourceX = sourceStartX; sourceX < sourceEndX; sourceX++) {
					var sourcePixel = sourceY * src.Width + sourceX;
					var destinationPixel = (sourceY + y) * dst.Width + sourceX + x;
					var alpha = hasAlpha ? src.Alpha[sourcePixel] : byte.MaxValue;

					if (dst.Format == DmdPixelFormat.I8) {
						dst.Data[destinationPixel] = Blend(dst.Data[destinationPixel], src.Pixels[sourcePixel],
							alpha, opacity, mode);
					} else {
						var destinationOffset = destinationPixel * 3;
						if (src.Format == DmdPixelFormat.I8) {
							var intensity = src.Pixels[sourcePixel];
							BlendRgb(dst.Data, destinationOffset,
								Multiply(intensity, tint.r), Multiply(intensity, tint.g), Multiply(intensity, tint.b),
								alpha, opacity, mode);
						} else {
							var sourceOffset = sourcePixel * 3;
							BlendRgb(dst.Data, destinationOffset, src.Pixels[sourceOffset], src.Pixels[sourceOffset + 1],
								src.Pixels[sourceOffset + 2], alpha, opacity, mode);
						}
					}
				}
			}
		}

		public static void FillRect(DmdSurface dst, int x, int y, int width, int height,
			in DmdShade shade, byte opacity)
		{
			FillRect(dst, x, y, width, height, shade, DmdBlendMode.Alpha, opacity);
		}

		internal static void FillRect(DmdSurface dst, int x, int y, int width, int height,
			in DmdShade shade, DmdBlendMode mode, byte opacity)
		{
			if (dst == null) {
				throw new ArgumentNullException(nameof(dst));
			}
			if (width <= 0 || height <= 0) {
				return;
			}

			var startX = math.max(0, x);
			var startY = math.max(0, y);
			var endX = (int)System.Math.Max(0L, System.Math.Min(dst.Width, (long)x + width));
			var endY = (int)System.Math.Max(0L, System.Math.Min(dst.Height, (long)y + height));
			var sourceAlpha = dst.Format == DmdPixelFormat.Rgb24 ? shade.Color.a : byte.MaxValue;
			for (var destinationY = startY; destinationY < endY; destinationY++) {
				for (var destinationX = startX; destinationX < endX; destinationX++) {
					var destinationPixel = destinationY * dst.Width + destinationX;
					if (dst.Format == DmdPixelFormat.I8) {
						dst.Data[destinationPixel] = Blend(dst.Data[destinationPixel], shade.Intensity,
							sourceAlpha, opacity, mode);
					} else {
						BlendRgb(dst.Data, destinationPixel * 3, shade.Color.r, shade.Color.g, shade.Color.b,
							sourceAlpha, opacity, mode);
					}
				}
			}
		}

		public static void ApplyAlphaMask(DmdSurface dst, DmdBitmapData mask,
			int maskFrameOffsetX, int maskFrameOffsetY)
		{
			if (dst == null) {
				throw new ArgumentNullException(nameof(dst));
			}
			ValidateSource(mask);
			var hasAlpha = mask.Alpha != null && mask.Alpha.Length != 0;
			for (var y = 0; y < dst.Height; y++) {
				for (var x = 0; x < dst.Width; x++) {
					var maskXValue = (long)x - maskFrameOffsetX;
					var maskYValue = (long)y - maskFrameOffsetY;
					byte alpha = 0;
					if (maskXValue >= 0 && maskXValue < mask.Width && maskYValue >= 0 && maskYValue < mask.Height) {
						var maskX = (int)maskXValue;
						var maskY = (int)maskYValue;
						var maskPixel = maskY * mask.Width + maskX;
						if (hasAlpha) {
							alpha = mask.Alpha[maskPixel];
						} else if (mask.Format == DmdPixelFormat.I8) {
							alpha = mask.Pixels[maskPixel];
						} else {
							var offset = maskPixel * 3;
							alpha = (byte)((77 * mask.Pixels[offset] + 150 * mask.Pixels[offset + 1] +
							                29 * mask.Pixels[offset + 2] + 128) >> 8);
						}
					}

					var destinationOffset = (y * dst.Width + x) * (dst.Format == DmdPixelFormat.I8 ? 1 : 3);
					var channelCount = dst.Format == DmdPixelFormat.I8 ? 1 : 3;
					for (var channel = 0; channel < channelCount; channel++) {
						dst.Data[destinationOffset + channel] = Multiply(dst.Data[destinationOffset + channel], alpha);
					}
				}
			}
		}

		internal static byte Blend(byte destination, byte source, byte sourceAlpha, byte opacity,
			DmdBlendMode mode)
		{
			if (mode == DmdBlendMode.Opaque) {
				return source;
			}
			var alpha = Multiply(sourceAlpha, opacity);
			switch (mode) {
				case DmdBlendMode.Alpha:
					return (byte)((source * alpha + destination * (255 - alpha) + 127) / 255);
				case DmdBlendMode.Add:
					return (byte)math.min(255, destination + (source * alpha + 127) / 255);
				case DmdBlendMode.Invert:
					return alpha > 127 ? (byte)(255 - destination) : destination;
				default:
					throw new ArgumentOutOfRangeException(nameof(mode));
			}
		}

		internal static byte Multiply(byte value, byte multiplier)
		{
			return (byte)((value * multiplier + 127) / 255);
		}

		private static void BlendRgb(byte[] data, int offset, byte red, byte green, byte blue,
			byte sourceAlpha, byte opacity, DmdBlendMode mode)
		{
			data[offset] = Blend(data[offset], red, sourceAlpha, opacity, mode);
			data[offset + 1] = Blend(data[offset + 1], green, sourceAlpha, opacity, mode);
			data[offset + 2] = Blend(data[offset + 2], blue, sourceAlpha, opacity, mode);
		}

		private static void ValidateSource(DmdBitmapData source)
		{
			if (source == null) {
				throw new ArgumentNullException(nameof(source));
			}
			if (source.Width < 1 || source.Height < 1) {
				throw new ArgumentException("Source dimensions must be positive.", nameof(source));
			}
			var channelCount = source.Format == DmdPixelFormat.I8 ? 1 : source.Format == DmdPixelFormat.Rgb24 ? 3 : 0;
			var expectedPixels = (long)source.Width * source.Height * channelCount;
			if (channelCount == 0 || source.Pixels == null || source.Pixels.LongLength != expectedPixels) {
				throw new ArgumentException("Source pixel data does not match its dimensions and format.", nameof(source));
			}
			var expectedAlpha = (long)source.Width * source.Height;
			if (source.Alpha != null && source.Alpha.Length != 0 && source.Alpha.LongLength != expectedAlpha) {
				throw new ArgumentException("Source alpha data does not match its dimensions.", nameof(source));
			}
		}
	}
}
