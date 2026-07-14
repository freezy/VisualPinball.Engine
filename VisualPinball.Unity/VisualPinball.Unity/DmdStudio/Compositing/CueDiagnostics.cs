// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Collections.Generic;

namespace VisualPinball.Unity
{
	public readonly struct CueDiagnostic
	{
		public string Code { get; }
		public string Message { get; }

		public CueDiagnostic(string code, string message)
		{
			Code = code;
			Message = message;
		}
	}

	/// <summary>
	/// Per-cue-instance diagnostics. Identical logical faults are reported once.
	/// </summary>
	public sealed class CueDiagnostics
	{
		private readonly List<CueDiagnostic> _diagnostics = new List<CueDiagnostic>();
		private readonly HashSet<DiagnosticKey> _keys = new HashSet<DiagnosticKey>();

		public IReadOnlyList<CueDiagnostic> Diagnostics => _diagnostics;
		public int Count => _diagnostics.Count;

		public void Clear()
		{
			_diagnostics.Clear();
			_keys.Clear();
		}

		internal void MissingParameter(string name)
		{
			if (_keys.Add(new DiagnosticKey(DiagnosticKind.MissingParameter, name))) {
				_diagnostics.Add(new CueDiagnostic("binding.missing", $"Parameter '{name}' is not bound."));
			}
		}

		internal void InvalidFormat(string name, string format)
		{
			if (_keys.Add(new DiagnosticKey(DiagnosticKind.InvalidFormat, name, format))) {
				_diagnostics.Add(new CueDiagnostic("binding.format",
					$"Format '{format}' is invalid for parameter '{name}'."));
			}
		}

		internal void TextTruncated()
		{
			if (_keys.Add(new DiagnosticKey(DiagnosticKind.TextTruncated))) {
				_diagnostics.Add(new CueDiagnostic("binding.truncated",
					$"Resolved text exceeded {DmdValidation.MaxResolvedTextLength} characters and was truncated."));
			}
		}

		internal void MissingGlyph(int codepoint)
		{
			if (_keys.Add(new DiagnosticKey(DiagnosticKind.MissingGlyph, number: codepoint))) {
				_diagnostics.Add(new CueDiagnostic("font.glyph.missing", $"Font has no glyph for U+{codepoint:X4}."));
			}
		}

		internal void MalformedFont(string detail)
		{
			detail = detail ?? "Font data is malformed.";
			if (_keys.Add(new DiagnosticKey(DiagnosticKind.MalformedFont, detail))) {
				_diagnostics.Add(new CueDiagnostic("font.malformed", detail));
			}
		}

		internal void MalformedGlyph(int codepoint)
		{
			if (_keys.Add(new DiagnosticKey(DiagnosticKind.MalformedGlyph, number: codepoint))) {
				_diagnostics.Add(new CueDiagnostic("font.malformed",
					$"Glyph U+{codepoint:X4} lies outside the font atlas."));
			}
		}

		private enum DiagnosticKind
		{
			MissingParameter,
			InvalidFormat,
			TextTruncated,
			MissingGlyph,
			MalformedFont,
			MalformedGlyph,
		}

		private readonly struct DiagnosticKey : IEquatable<DiagnosticKey>
		{
			private readonly DiagnosticKind _kind;
			private readonly string _first;
			private readonly string _second;
			private readonly int _number;

			public DiagnosticKey(DiagnosticKind kind, string first = null, string second = null, int number = 0)
			{
				_kind = kind;
				_first = first;
				_second = second;
				_number = number;
			}

			public bool Equals(DiagnosticKey other)
			{
				return _kind == other._kind && _number == other._number &&
				       string.Equals(_first, other._first, StringComparison.Ordinal) &&
				       string.Equals(_second, other._second, StringComparison.Ordinal);
			}

			public override bool Equals(object obj) => obj is DiagnosticKey other && Equals(other);

			public override int GetHashCode()
			{
				unchecked {
					var hash = ((int)_kind * 397) ^ _number;
					hash = (hash * 397) ^ (_first != null ? StringComparer.Ordinal.GetHashCode(_first) : 0);
					hash = (hash * 397) ^ (_second != null ? StringComparer.Ordinal.GetHashCode(_second) : 0);
					return hash;
				}
			}
		}
	}
}
