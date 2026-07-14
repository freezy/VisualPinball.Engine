// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using UnityEngine;

namespace VisualPinball.Unity.Test
{
	internal static class DmdTestFont
	{
		private static readonly string[] Patterns = {
			"111101101101111", // .notdef
			"010101111101101", // A
			"110101110101110", // B
			"111101101101111", // 0
		};

		public static DmdFontAsset Create()
		{
			var font = ScriptableObject.CreateInstance<DmdFontAsset>();
			font.LineHeight = 5;
			font.Baseline = 4;
			font.Tracking = 1;
			font.DigitWidth = 5;
			font.Atlas = new DmdBitmapData {
				Width = 12,
				Height = 5,
				Format = DmdPixelFormat.I8,
				Pixels = new byte[60]
			};
			for (var glyphIndex = 0; glyphIndex < Patterns.Length; glyphIndex++) {
				for (var y = 0; y < 5; y++) {
					for (var x = 0; x < 3; x++) {
						font.Atlas.Pixels[y * font.Atlas.Width + glyphIndex * 3 + x] =
							Patterns[glyphIndex][y * 3 + x] == '1' ? byte.MaxValue : (byte)0;
					}
				}
			}
			AddGlyph(font, 0, 0);
			AddGlyph(font, 'A', 3);
			AddGlyph(font, 'B', 6);
			AddGlyph(font, '0', 9);
			font.Kerning.Add(new DmdKerningPair { LeftCodepoint = 'A', RightCodepoint = 'B', Adjustment = -1 });
			return font;
		}

		private static void AddGlyph(DmdFontAsset font, int codepoint, int x)
		{
			font.Glyphs.Add(new DmdGlyph {
				Codepoint = codepoint,
				X = x,
				Y = 0,
				W = 3,
				H = 5,
				Advance = 4
			});
		}
	}
}
