// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Test
{
	public class DmdCompositorSmokeTests
	{
		[Test]
		public void BitmapAndBoundTextCompositionMatchesGoldenFrame()
		{
			var font = DmdTestFont.Create();
			try {
				var surface = new DmdSurface(16, 8, DmdPixelFormat.I8);
				var bitmap = new DmdBitmapData {
					Width = 8,
					Height = 4,
					Pixels = new byte[] {
						20, 40, 60, 80, 100, 120, 140, 160,
						40, 60, 80, 100, 120, 140, 160, 180,
						60, 80, 100, 120, 140, 160, 180, 200,
						80, 100, 120, 140, 160, 180, 200, 220,
					}
				};
				DmdBlitter.Blit(surface, bitmap, 4, 2, DmdBlendMode.Opaque, byte.MaxValue,
					(Color32)Color.white);
				var text = BoundText.Parse("A{score}").Resolve(new DmdParams().Set("score", 0),
					new CueDiagnostics());
				DmdTextRenderer.Draw(surface, font, text, 8, 4, DmdAnchor.MiddleCenter,
					DmdTextEffect.Shadow, DmdShade.White, new DmdShade { Intensity = 48 },
					DmdBlendMode.Alpha, new CueDiagnostics());

				var hash = DmdSurfaceAssert.Hash(surface.Data);
				if (DmdSurfaceAssert.RegenerateGoldenHashes) {
					Assert.Fail($"Replace the compositor smoke golden hash with {hash}u.");
				}
				Assert.That(hash, Is.EqualTo(455540081u),
					DmdSurfaceAssert.ToAscii(surface));
			} finally {
				Object.DestroyImmediate(font);
			}
		}
	}
}
