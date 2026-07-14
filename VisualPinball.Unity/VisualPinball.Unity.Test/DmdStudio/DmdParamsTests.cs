// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Globalization;
using NUnit.Framework;

namespace VisualPinball.Unity.Test
{
	public class DmdParamsTests
	{
		[Test]
		public void StoresEverySupportedValueTypeByOrdinalName()
		{
			var values = new DmdParams()
				.Set("score", 42)
				.Set("player.total", 1234567890123L)
				.Set("timer", 1.25)
				.Set("label", "BALL 1")
				.Set("active", true);

			Assert.That(values.TryGet("score", out var score), Is.True);
			Assert.That(score.Type, Is.EqualTo(DmdParamType.Integer));
			Assert.That(score.IntValue, Is.EqualTo(42));
			Assert.That(values.TryGet("player.total", out var total), Is.True);
			Assert.That(total.IntValue, Is.EqualTo(1234567890123L));
			Assert.That(values.TryGet("timer", out var timer), Is.True);
			Assert.That(timer.ToInvariantString(), Is.EqualTo(1.25.ToString(CultureInfo.InvariantCulture)));
			Assert.That(values.TryGet("label", out var label), Is.True);
			Assert.That(label.StringValue, Is.EqualTo("BALL 1"));
			Assert.That(values.TryGet("active", out var active), Is.True);
			Assert.That(active.BoolValue, Is.True);
			Assert.That(values.TryGet("Score", out _), Is.False);
		}

		[TestCase("")]
		[TestCase("player..score")]
		[TestCase("9score")]
		[TestCase("player score")]
		[TestCase("score:value")]
		public void RejectsInvalidParameterNames(string name)
		{
			Assert.Throws<ArgumentException>(() => new DmdParams().Set(name, 1));
		}

		[Test]
		public void RejectsNullNamesAndNormalizesNullStrings()
		{
			Assert.Throws<ArgumentNullException>(() => new DmdParams().Set(null, 1));
			var values = new DmdParams().Set("label", (string)null);
			Assert.That(values.TryGet("label", out var label), Is.True);
			Assert.That(label.StringValue, Is.EqualTo(string.Empty));
		}

		[Test]
		public void CapsDistinctParametersButAllowsUpdates()
		{
			var values = new DmdParams();
			for (var index = 0; index < DmdValidation.MaxBoundParams; index++) {
				values.Set($"p{index}", index);
			}

			Assert.That(values.Count, Is.EqualTo(DmdValidation.MaxBoundParams));
			Assert.DoesNotThrow(() => values.Set("p0", 100));
			Assert.Throws<ArgumentException>(() => values.Set("overflow", 1));
		}

		[Test]
		public void ParameterNameLengthCapIsInclusive()
		{
			var maximum = new string('p', DmdValidation.MaxParameterNameLength);
			Assert.DoesNotThrow(() => new DmdParams().Set(maximum, 1));
			Assert.Throws<ArgumentException>(() => new DmdParams().Set(maximum + "p", 1));
		}
	}
}
