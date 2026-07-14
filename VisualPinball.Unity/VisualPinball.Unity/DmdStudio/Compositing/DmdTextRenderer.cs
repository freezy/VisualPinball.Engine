// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using Unity.Mathematics;

namespace VisualPinball.Unity
{
	public static class DmdTextRenderer
	{
		private const int ReplacementCodepoint = 0xfffd;

		public static int Measure(DmdFontAsset font, string text)
		{
			if (font == null) {
				throw new ArgumentNullException(nameof(font));
			}
			return Measure(font, text.AsSpan());
		}

		private static int Measure(DmdFontAsset font, ReadOnlySpan<char> text)
		{
			if (text.Length == 0) {
				return 0;
			}
			var width = 0;
			var previous = -1;
			var index = 0;
			var glyphCount = 0;
			while (TryReadCodepoint(text, ref index, out var codepoint)) {
				if (glyphCount > 0) {
					width += font.Tracking + FindKerning(font, previous, codepoint);
				}
				if (TryResolveGlyph(font, codepoint, out var glyph)) {
					width += Advance(font, codepoint, glyph);
				} else {
					width += MissingGlyphWidth(font);
				}
				previous = codepoint;
				glyphCount++;
			}
			return math.max(0, width);
		}

		public static void Draw(DmdSurface dst, DmdFontAsset font, string text,
			int x, int y, DmdAnchor anchor, DmdTextEffect effect,
			in DmdShade shade, in DmdShade outlineShade, DmdBlendMode mode, CueDiagnostics diagnostics)
		{
			Draw(dst, font, text, x, y, anchor, effect, shade, outlineShade, mode, byte.MaxValue, diagnostics);
		}

		internal static void Draw(DmdSurface dst, DmdFontAsset font, string text,
			int x, int y, DmdAnchor anchor, DmdTextEffect effect,
			in DmdShade shade, in DmdShade outlineShade, DmdBlendMode mode, byte opacity,
			CueDiagnostics diagnostics)
		{
			Draw(dst, font, text.AsSpan(), x, y, anchor, effect, shade, outlineShade, mode, opacity, diagnostics);
		}

		internal static void Draw(DmdSurface dst, DmdFontAsset font, char[] text, int textLength,
			int x, int y, DmdAnchor anchor, DmdTextEffect effect,
			in DmdShade shade, in DmdShade outlineShade, DmdBlendMode mode, byte opacity,
			CueDiagnostics diagnostics)
		{
			if (text == null) {
				throw new ArgumentNullException(nameof(text));
			}
			if (textLength < 0 || textLength > text.Length) {
				throw new ArgumentOutOfRangeException(nameof(textLength));
			}
			Draw(dst, font, new ReadOnlySpan<char>(text, 0, textLength), x, y, anchor, effect, shade,
				outlineShade, mode, opacity, diagnostics);
		}

		private static void Draw(DmdSurface dst, DmdFontAsset font, ReadOnlySpan<char> text,
			int x, int y, DmdAnchor anchor, DmdTextEffect effect,
			in DmdShade shade, in DmdShade outlineShade, DmdBlendMode mode, byte opacity,
			CueDiagnostics diagnostics)
		{
			if (dst == null) {
				throw new ArgumentNullException(nameof(dst));
			}
			if (font == null) {
				diagnostics?.MalformedFont("Font asset is missing.");
				return;
			}
			if (text.Length == 0) {
				return;
			}
			if (!HasReadableAtlas(font, diagnostics)) {
				return;
			}

			var lineHeight = math.max(1, font.LineHeight);
			var originX = IsLeftAnchor(anchor) ? x : ResolveHorizontalOrigin(x, Measure(font, text), anchor);
			var originY = ResolveVerticalOrigin(y, lineHeight, math.clamp(font.Baseline, 0, lineHeight), anchor);
			var pen = 0;
			var previous = -1;
			var glyphCount = 0;
			var index = 0;
			while (TryReadCodepoint(text, ref index, out var codepoint)) {
				if (glyphCount > 0) {
					pen += font.Tracking + FindKerning(font, previous, codepoint);
				}

				if (!TryFindGlyph(font, codepoint, out var glyph)) {
					diagnostics?.MissingGlyph(codepoint);
					if (!TryResolveGlyph(font, codepoint, out glyph)) {
						DrawMissingBox(dst, originX + pen, originY, MissingGlyphWidth(font), lineHeight,
							shade, outlineShade, effect, mode, opacity);
						pen += MissingGlyphWidth(font);
						previous = codepoint;
						glyphCount++;
						continue;
					}
				}

				if (!GlyphIsReadable(font.Atlas, glyph)) {
					diagnostics?.MalformedGlyph(glyph.Codepoint);
					pen += Advance(font, codepoint, glyph);
					previous = codepoint;
					glyphCount++;
					continue;
				}

				var digitOffset = IsDigit(codepoint) && font.DigitWidth > 0
					? (font.DigitWidth - glyph.Advance) / 2
					: 0;
				var glyphX = originX + pen + digitOffset + glyph.OffsetX;
				var glyphY = originY + glyph.OffsetY;
				DrawGlyphWithEffect(dst, font.Atlas, glyph, glyphX, glyphY, effect, shade, outlineShade, mode, opacity);
				pen += Advance(font, codepoint, glyph);
				previous = codepoint;
				glyphCount++;
			}
		}

		private static void DrawGlyphWithEffect(DmdSurface dst, DmdBitmapData atlas, DmdGlyph glyph,
			int x, int y, DmdTextEffect effect, in DmdShade shade, in DmdShade outlineShade, DmdBlendMode mode,
			byte opacity)
		{
			switch (effect) {
				case DmdTextEffect.None:
					DrawGlyph(dst, atlas, glyph, x, y, shade, mode, opacity);
					break;
				case DmdTextEffect.Outline:
					DrawOutline(dst, atlas, glyph, x, y, outlineShade, mode, opacity);
					DrawGlyph(dst, atlas, glyph, x, y, shade, mode, opacity);
					break;
				case DmdTextEffect.Shadow:
					DrawGlyph(dst, atlas, glyph, x + 1, y + 1, outlineShade, mode, opacity);
					DrawGlyph(dst, atlas, glyph, x, y, shade, mode, opacity);
					break;
				case DmdTextEffect.Inverse:
					DrawInverseGlyph(dst, atlas, glyph, x, y, shade, outlineShade, mode, opacity);
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(effect));
			}
		}

		private static void DrawGlyph(DmdSurface dst, DmdBitmapData atlas, DmdGlyph glyph,
			int x, int y, in DmdShade shade, DmdBlendMode mode, byte opacity)
		{
			for (var glyphY = 0; glyphY < glyph.H; glyphY++) {
				var destinationY = y + glyphY;
				if (destinationY < 0 || destinationY >= dst.Height) {
					continue;
				}
				for (var glyphX = 0; glyphX < glyph.W; glyphX++) {
					var destinationX = x + glyphX;
					if (destinationX < 0 || destinationX >= dst.Width) {
						continue;
					}
					var mask = GlyphMask(atlas, glyph, glyphX, glyphY);
					DrawShadedPixel(dst, destinationX, destinationY, shade, mask, mode, opacity);
				}
			}
		}

		private static void DrawOutline(DmdSurface dst, DmdBitmapData atlas, DmdGlyph glyph,
			int x, int y, in DmdShade shade, DmdBlendMode mode, byte opacity)
		{
			for (var glyphY = -1; glyphY <= glyph.H; glyphY++) {
				for (var glyphX = -1; glyphX <= glyph.W; glyphX++) {
					if (glyphX >= 0 && glyphX < glyph.W && glyphY >= 0 && glyphY < glyph.H &&
					    GlyphMask(atlas, glyph, glyphX, glyphY) != 0) {
						continue;
					}
					byte mask = 0;
					for (var neighborY = -1; neighborY <= 1; neighborY++) {
						for (var neighborX = -1; neighborX <= 1; neighborX++) {
							var sampleX = glyphX + neighborX;
							var sampleY = glyphY + neighborY;
							if (sampleX >= 0 && sampleX < glyph.W && sampleY >= 0 && sampleY < glyph.H) {
								var sample = GlyphMask(atlas, glyph, sampleX, sampleY);
								mask = sample > mask ? sample : mask;
							}
						}
					}
					if (mask != 0) {
						DrawShadedPixel(dst, x + glyphX, y + glyphY, shade, mask, mode, opacity);
					}
				}
			}
		}

		private static void DrawInverseGlyph(DmdSurface dst, DmdBitmapData atlas, DmdGlyph glyph,
			int x, int y, in DmdShade background, in DmdShade foreground, DmdBlendMode mode, byte opacity)
		{
			for (var glyphY = 0; glyphY < glyph.H; glyphY++) {
				for (var glyphX = 0; glyphX < glyph.W; glyphX++) {
					var mask = GlyphMask(atlas, glyph, glyphX, glyphY);
					var inverseShade = Interpolate(background, foreground, mask);
					DrawShadedPixel(dst, x + glyphX, y + glyphY, inverseShade, byte.MaxValue, mode, opacity);
				}
			}
		}

		private static void DrawMissingBox(DmdSurface dst, int x, int y, int width, int height,
			in DmdShade shade, in DmdShade outlineShade, DmdTextEffect effect, DmdBlendMode mode, byte opacity)
		{
			var boxShade = effect == DmdTextEffect.Inverse ? outlineShade : shade;
			for (var boxY = 0; boxY < height; boxY++) {
				for (var boxX = 0; boxX < width; boxX++) {
					var border = boxX == 0 || boxX == width - 1 || boxY == 0 || boxY == height - 1;
					DrawShadedPixel(dst, x + boxX, y + boxY, boxShade, border ? byte.MaxValue : (byte)0, mode,
						opacity);
				}
			}
		}

		private static void DrawShadedPixel(DmdSurface dst, int x, int y, in DmdShade shade,
			byte mask, DmdBlendMode mode, byte opacity)
		{
			if (x < 0 || x >= dst.Width || y < 0 || y >= dst.Height) {
				return;
			}
			var pixel = y * dst.Width + x;
			if (dst.Format == DmdPixelFormat.I8) {
				var source = mode == DmdBlendMode.Opaque ? DmdBlitter.Multiply(shade.Intensity, mask) : shade.Intensity;
				dst.Data[pixel] = DmdBlitter.Blend(dst.Data[pixel], source, mask, opacity, mode);
			} else {
				var alpha = DmdBlitter.Multiply(mask, shade.Color.a);
				var offset = pixel * 3;
				var red = mode == DmdBlendMode.Opaque ? DmdBlitter.Multiply(shade.Color.r, mask) : shade.Color.r;
				var green = mode == DmdBlendMode.Opaque ? DmdBlitter.Multiply(shade.Color.g, mask) : shade.Color.g;
				var blue = mode == DmdBlendMode.Opaque ? DmdBlitter.Multiply(shade.Color.b, mask) : shade.Color.b;
				dst.Data[offset] = DmdBlitter.Blend(dst.Data[offset], red, alpha, opacity, mode);
				dst.Data[offset + 1] = DmdBlitter.Blend(dst.Data[offset + 1], green, alpha, opacity, mode);
				dst.Data[offset + 2] = DmdBlitter.Blend(dst.Data[offset + 2], blue, alpha, opacity, mode);
			}
		}

		private static DmdShade Interpolate(in DmdShade from, in DmdShade to, byte amount)
		{
			return new DmdShade {
				Intensity = (byte)((from.Intensity * (255 - amount) + to.Intensity * amount + 127) / 255),
				Color = new UnityEngine.Color32(
					(byte)((from.Color.r * (255 - amount) + to.Color.r * amount + 127) / 255),
					(byte)((from.Color.g * (255 - amount) + to.Color.g * amount + 127) / 255),
					(byte)((from.Color.b * (255 - amount) + to.Color.b * amount + 127) / 255),
					(byte)((from.Color.a * (255 - amount) + to.Color.a * amount + 127) / 255))
			};
		}

		private static bool HasReadableAtlas(DmdFontAsset font, CueDiagnostics diagnostics)
		{
			var atlas = font.Atlas;
			if (atlas == null || atlas.Width < 1 || atlas.Height < 1 || atlas.Format != DmdPixelFormat.I8 ||
			    atlas.Pixels == null || atlas.Pixels.LongLength != (long)atlas.Width * atlas.Height ||
			    atlas.Alpha != null && atlas.Alpha.Length != 0 && atlas.Alpha.LongLength != (long)atlas.Width * atlas.Height) {
				diagnostics?.MalformedFont("Font atlas dimensions or buffers are invalid.");
				return false;
			}
			return true;
		}

		private static bool GlyphIsReadable(DmdBitmapData atlas, DmdGlyph glyph)
		{
			return glyph.X >= 0 && glyph.Y >= 0 && glyph.W >= 0 && glyph.H >= 0 &&
			       (long)glyph.X + glyph.W <= atlas.Width && (long)glyph.Y + glyph.H <= atlas.Height;
		}

		private static byte GlyphMask(DmdBitmapData atlas, DmdGlyph glyph, int x, int y)
		{
			var pixel = (glyph.Y + y) * atlas.Width + glyph.X + x;
			return atlas.Alpha != null && atlas.Alpha.Length != 0 ? atlas.Alpha[pixel] : atlas.Pixels[pixel];
		}

		private static bool TryResolveGlyph(DmdFontAsset font, int codepoint, out DmdGlyph glyph)
		{
			return TryFindGlyph(font, codepoint, out glyph) || TryFindGlyph(font, 0, out glyph) ||
			       TryFindGlyph(font, ReplacementCodepoint, out glyph);
		}

		private static bool TryFindGlyph(DmdFontAsset font, int codepoint, out DmdGlyph glyph)
		{
			if (font.Glyphs != null) {
				for (var index = 0; index < font.Glyphs.Count; index++) {
					if (font.Glyphs[index].Codepoint == codepoint) {
						glyph = font.Glyphs[index];
						return true;
					}
				}
			}
			glyph = default;
			return false;
		}

		private static int FindKerning(DmdFontAsset font, int left, int right)
		{
			if (font.Kerning != null) {
				for (var index = 0; index < font.Kerning.Count; index++) {
					var pair = font.Kerning[index];
					if (pair.LeftCodepoint == left && pair.RightCodepoint == right) {
						return pair.Adjustment;
					}
				}
			}
			return 0;
		}

		private static int Advance(DmdFontAsset font, int codepoint, DmdGlyph glyph)
		{
			return IsDigit(codepoint) && font.DigitWidth > 0 ? font.DigitWidth : glyph.Advance;
		}

		private static bool IsDigit(int codepoint) => codepoint >= '0' && codepoint <= '9';

		private static int MissingGlyphWidth(DmdFontAsset font)
		{
			return math.max(3, math.max(1, font.LineHeight) / 2);
		}

		private static int ResolveHorizontalOrigin(int x, int width, DmdAnchor anchor)
		{
			switch (anchor) {
				case DmdAnchor.TopCenter:
				case DmdAnchor.MiddleCenter:
				case DmdAnchor.BottomCenter:
				case DmdAnchor.BaselineCenter:
					return x - width / 2;
				case DmdAnchor.TopRight:
				case DmdAnchor.MiddleRight:
				case DmdAnchor.BottomRight:
				case DmdAnchor.BaselineRight:
					return x - width;
				default:
					return x;
			}
		}

		private static bool IsLeftAnchor(DmdAnchor anchor)
		{
			return anchor == DmdAnchor.TopLeft || anchor == DmdAnchor.MiddleLeft ||
			       anchor == DmdAnchor.BottomLeft || anchor == DmdAnchor.BaselineLeft;
		}

		private static int ResolveVerticalOrigin(int y, int lineHeight, int baseline, DmdAnchor anchor)
		{
			switch (anchor) {
				case DmdAnchor.MiddleLeft:
				case DmdAnchor.MiddleCenter:
				case DmdAnchor.MiddleRight:
					return y - lineHeight / 2;
				case DmdAnchor.BottomLeft:
				case DmdAnchor.BottomCenter:
				case DmdAnchor.BottomRight:
					return y - lineHeight;
				case DmdAnchor.BaselineLeft:
				case DmdAnchor.BaselineCenter:
				case DmdAnchor.BaselineRight:
					return y - baseline;
				default:
					return y;
			}
		}

		private static bool TryReadCodepoint(ReadOnlySpan<char> text, ref int index, out int codepoint)
		{
			if (index >= text.Length) {
				codepoint = 0;
				return false;
			}
			var first = text[index++];
			if (char.IsHighSurrogate(first) && index < text.Length && char.IsLowSurrogate(text[index])) {
				codepoint = char.ConvertToUtf32(first, text[index++]);
			} else {
				codepoint = first;
			}
			return true;
		}
	}
}
