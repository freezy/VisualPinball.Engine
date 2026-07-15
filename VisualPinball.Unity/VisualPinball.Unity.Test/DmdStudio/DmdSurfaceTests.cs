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
	public class DmdSurfaceTests
	{
		[Test]
		public void AllocatesClearsAndCopiesCheckedFormats()
		{
			var source = new DmdSurface(4, 2, DmdPixelFormat.Rgb24);
			var copy = new DmdSurface(4, 2, DmdPixelFormat.Rgb24);
			source.Clear(37);

			copy.CopyFrom(source);

			Assert.That(source.Data, Has.Length.EqualTo(24));
			Assert.That(copy.Data, Is.EqualTo(source.Data));
			Assert.Throws<ArgumentException>(() => copy.CopyFrom(new DmdSurface(4, 2, DmdPixelFormat.I8)));
			Assert.Throws<ArgumentOutOfRangeException>(() => new DmdSurface(0, 1, DmdPixelFormat.I8));
			Assert.Throws<ArgumentOutOfRangeException>(() =>
				new DmdSurface(DmdValidation.MaxWidth + 1, 1, DmdPixelFormat.I8));
		}
	}
}
