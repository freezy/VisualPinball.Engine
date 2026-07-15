// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Test
{
	public class DmdTextRendererTests
	{
		[Test]
		public void MeasuresKerningTrackingAndTabularDigits()
		{
			var font = DmdTestFont.Create();
			try {
				Assert.That(DmdTextRenderer.Measure(font, "AB"), Is.EqualTo(8));
				Assert.That(DmdTextRenderer.Measure(font, "00"), Is.EqualTo(11));
			} finally {
				Object.DestroyImmediate(font);
			}
		}

		[Test]
		public void DrawsTopOriginGlyphAtCenterAnchor()
		{
			var font = DmdTestFont.Create();
			try {
				var surface = new DmdSurface(8, 7, DmdPixelFormat.I8);
				DmdTextRenderer.Draw(surface, font, "A", 4, 1, DmdAnchor.TopCenter, DmdTextEffect.None,
					DmdShade.White, DmdShade.Black, DmdBlendMode.Alpha, new CueDiagnostics());

				Assert.That(DmdSurfaceAssert.ToAscii(surface), Is.EqualTo(
					"        \n" +
					"   @    \n" +
					"  @ @   \n" +
					"  @@@   \n" +
					"  @ @   \n" +
					"  @ @   \n" +
					"        "));
			} finally {
				Object.DestroyImmediate(font);
			}
		}

		[TestCase(DmdTextEffect.None, 194383655u)]
		[TestCase(DmdTextEffect.Outline, 1190555245u)]
		[TestCase(DmdTextEffect.Shadow, 3866497374u)]
		[TestCase(DmdTextEffect.Inverse, 1978379769u)]
		public void TextEffectsHaveStableGoldenHashes(DmdTextEffect effect, uint expected)
		{
			var font = DmdTestFont.Create();
			try {
				var surface = new DmdSurface(16, 8, DmdPixelFormat.I8);
				surface.Clear(21);
				DmdTextRenderer.Draw(surface, font, "AB", 8, 4, DmdAnchor.MiddleCenter, effect,
					new DmdShade { Intensity = 230 }, new DmdShade { Intensity = 64 },
					DmdBlendMode.Alpha, new CueDiagnostics());

				var hash = DmdSurfaceAssert.Hash(surface.Data);
				if (DmdSurfaceAssert.RegenerateGoldenHashes) {
					Assert.Fail($"Replace the {effect} text golden hash with {hash}u.");
				}
				Assert.That(hash, Is.EqualTo(expected),
					$"{effect}\n{DmdSurfaceAssert.ToAscii(surface)}");
			} finally {
				Object.DestroyImmediate(font);
			}
		}

		[Test]
		public void MissingGlyphUsesNotdefAndDiagnosesOnce()
		{
			var font = DmdTestFont.Create();
			try {
				var expected = new DmdSurface(4, 5, DmdPixelFormat.I8);
				var actual = new DmdSurface(4, 5, DmdPixelFormat.I8);
				var diagnostics = new CueDiagnostics();
				DmdTextRenderer.Draw(expected, font, "\0", 0, 0, DmdAnchor.TopLeft, DmdTextEffect.None,
					DmdShade.White, DmdShade.Black, DmdBlendMode.Alpha, new CueDiagnostics());
				DmdTextRenderer.Draw(actual, font, "X", 0, 0, DmdAnchor.TopLeft, DmdTextEffect.None,
					DmdShade.White, DmdShade.Black, DmdBlendMode.Alpha, diagnostics);
				DmdTextRenderer.Draw(actual, font, "X", 0, 0, DmdAnchor.TopLeft, DmdTextEffect.None,
					DmdShade.White, DmdShade.Black, DmdBlendMode.Alpha, diagnostics);

				Assert.That(actual.Data, Is.EqualTo(expected.Data));
				Assert.That(diagnostics.Diagnostics.Count(item => item.Code == "font.glyph.missing"), Is.EqualTo(1));
			} finally {
				Object.DestroyImmediate(font);
			}
		}

		[Test]
		public void RgbTextUsesShadeColorAndAlpha()
		{
			var font = DmdTestFont.Create();
			try {
				var surface = new DmdSurface(3, 5, DmdPixelFormat.Rgb24);
				var shade = new DmdShade { Color = new Color32(100, 50, 25, 128) };
				DmdTextRenderer.Draw(surface, font, "A", 0, 0, DmdAnchor.TopLeft, DmdTextEffect.None,
					shade, DmdShade.Black, DmdBlendMode.Alpha, new CueDiagnostics());

				Assert.That(surface.Data[3], Is.EqualTo(50));
				Assert.That(surface.Data[4], Is.EqualTo(25));
				Assert.That(surface.Data[5], Is.EqualTo(13));
				Assert.That(surface.Data[0], Is.Zero);
			} finally {
				Object.DestroyImmediate(font);
			}
		}

		[Test]
		public void MalformedAtlasNeverThrowsAndDiagnosesOnce()
		{
			var font = DmdTestFont.Create();
			try {
				font.Atlas.Pixels = new byte[1];
				var diagnostics = new CueDiagnostics();
				var surface = new DmdSurface(8, 5, DmdPixelFormat.I8);

				Assert.DoesNotThrow(() => DmdTextRenderer.Draw(surface, font, "A", 0, 0,
					DmdAnchor.TopLeft, DmdTextEffect.None, DmdShade.White, DmdShade.Black,
					DmdBlendMode.Alpha, diagnostics));
				Assert.That(diagnostics.Diagnostics.Count(item => item.Code == "font.malformed"), Is.EqualTo(1));
			} finally {
				Object.DestroyImmediate(font);
			}
		}
	}
}
