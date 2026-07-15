// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using NUnit.Framework;

namespace VisualPinball.Unity.Test
{
	public class DmdQuantizerTests
	{
		[Test]
		public void ProducesExactTwoAndFourBitShadeRamps()
		{
			var luminance = new byte[] { 0, 15, 16, 63, 64, 127, 128, 191, 192, 239, 240, 255 };
			var dmd2 = new byte[luminance.Length];
			var dmd4 = new byte[luminance.Length];

			DmdQuantizer.I8ToDmd2(luminance, dmd2);
			DmdQuantizer.I8ToDmd4(luminance, dmd4);

			Assert.That(dmd2, Is.EqualTo(new byte[] { 0, 0, 0, 0, 1, 1, 2, 2, 3, 3, 3, 3 }));
			Assert.That(dmd4, Is.EqualTo(new byte[] { 0, 0, 1, 3, 4, 7, 8, 11, 12, 14, 15, 15 }));
			Assert.Throws<ArgumentException>(() => DmdQuantizer.I8ToDmd4(luminance, new byte[1]));
		}
	}
}
