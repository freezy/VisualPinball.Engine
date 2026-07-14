// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System.Globalization;
using System.Linq;
using NUnit.Framework;

namespace VisualPinball.Unity.Test
{
	public class BoundTextTests
	{
		[Test]
		public void ResolvesEscapesDottedNamesAndFormats()
		{
			var bound = BoundText.Parse("{{{player.score:N0}}} {timer:0.0}");
			var values = new DmdParams().Set("player.score", 12345).Set("timer", 1.25);

			var resolved = bound.Resolve(values, new CueDiagnostics());

			Assert.That(bound.IsDynamic, Is.True);
			Assert.That(resolved, Is.EqualTo("{12,345} 1.3"));
		}

		[Test]
		public void UsesInvariantCultureRegardlessOfCurrentCulture()
		{
			var previous = CultureInfo.CurrentCulture;
			try {
				CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
				var resolved = BoundText.Parse("{score:N1}")
					.Resolve(new DmdParams().Set("score", 1234), new CueDiagnostics());

				Assert.That(resolved, Is.EqualTo("1,234.0"));
			} finally {
				CultureInfo.CurrentCulture = previous;
			}
		}

		[Test]
		public void MissingAndInvalidFormatsFallBackAndDiagnoseOnce()
		{
			var diagnostics = new CueDiagnostics();
			var bound = BoundText.Parse("{missing}-{score:Q9}");
			var values = new DmdParams().Set("score", 42);

			Assert.That(bound.Resolve(values, diagnostics), Is.EqualTo("-42"));
			Assert.That(bound.Resolve(values, diagnostics), Is.EqualTo("-42"));
			Assert.That(diagnostics.Diagnostics.Count(item => item.Code == "binding.missing"), Is.EqualTo(1));
			Assert.That(diagnostics.Diagnostics.Count(item => item.Code == "binding.format"), Is.EqualTo(1));
		}

		[Test]
		public void TruncatesResolvedTextAtTheCapOnce()
		{
			var diagnostics = new CueDiagnostics();
			var value = new string('x', DmdValidation.MaxResolvedTextLength + 20);
			var bound = BoundText.Parse("{value}");

			var result = bound.Resolve(new DmdParams().Set("value", value), diagnostics);

			Assert.That(result, Has.Length.EqualTo(DmdValidation.MaxResolvedTextLength));
			Assert.That(diagnostics.Diagnostics.Count(item => item.Code == "binding.truncated"), Is.EqualTo(1));
		}

		[Test]
		public void VersionTracksReferencedValuesOnly()
		{
			var bound = BoundText.Parse("{score}");
			var values = new DmdParams().Set("score", 1);
			var first = bound.Version(values);
			values.Set("unused", 10);
			var unrelated = bound.Version(values);
			values.Set("score", 2);

			Assert.That(unrelated, Is.EqualTo(first));
			Assert.That(bound.Version(values), Is.Not.EqualTo(first));
			Assert.That(BoundText.Parse("literal").Version(values), Is.Zero);
		}
	}
}
