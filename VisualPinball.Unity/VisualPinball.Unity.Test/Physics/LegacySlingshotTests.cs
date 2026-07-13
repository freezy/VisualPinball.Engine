// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using NUnit.Framework;

namespace VisualPinball.Unity.Test
{
	public class LegacySlingshotTests
	{
		[TestCase(0f, 0f)]
		[TestCase(0.25f, 0.375f)]
		[TestCase(0.5f, 0.5f)]
		[TestCase(0.75f, 0.375f)]
		[TestCase(1f, 0f)]
		public void ShouldKeepHistoricalParabolicForceDistribution(float position01,
			float expected)
		{
			Assert.That(LineSlingshotCollider.LegacyForceFactor(position01 * 100f, 100f),
				Is.EqualTo(expected).Within(1e-6f));
		}
	}
}
