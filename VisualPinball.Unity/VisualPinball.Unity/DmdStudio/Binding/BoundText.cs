// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace VisualPinball.Unity
{
	internal readonly struct BoundText
	{
		private readonly Segment[] _segments;

		private BoundText(Segment[] segments, bool isDynamic)
		{
			_segments = segments;
			IsDynamic = isDynamic;
		}

		public bool IsDynamic { get; }

		public static BoundText Parse(string template)
		{
			template = template ?? string.Empty;
			var segments = new List<Segment>();
			var literal = new StringBuilder();
			var isDynamic = false;
			for (var index = 0; index < template.Length;) {
				var character = template[index];
				if (character == '{' && index + 1 < template.Length && template[index + 1] == '{') {
					literal.Append('{');
					index += 2;
					continue;
				}
				if (character == '}' && index + 1 < template.Length && template[index + 1] == '}') {
					literal.Append('}');
					index += 2;
					continue;
				}
				if (character != '{') {
					literal.Append(character);
					index++;
					continue;
				}

				var end = template.IndexOf('}', index + 1);
				if (end < 0) {
					literal.Append(character);
					index++;
					continue;
				}
				var token = template.Substring(index + 1, end - index - 1);
				var separator = token.IndexOf(':');
				var name = separator < 0 ? token : token.Substring(0, separator);
				if (!DmdValidation.IsValidParameterName(name)) {
					literal.Append(template, index, end - index + 1);
					index = end + 1;
					continue;
				}

				FlushLiteral(segments, literal);
				var format = separator < 0 ? null : token.Substring(separator + 1);
				segments.Add(Segment.Parameter(name, format));
				isDynamic = true;
				index = end + 1;
			}
			FlushLiteral(segments, literal);
			return new BoundText(segments.ToArray(), isDynamic);
		}

		public string Resolve(DmdParams parameters, CueDiagnostics diagnostics)
		{
			if (_segments == null || _segments.Length == 0) {
				return string.Empty;
			}
			if (!IsDynamic && _segments.Length == 1) {
				return Truncate(_segments[0].Literal, diagnostics);
			}

			var output = new StringBuilder();
			var truncated = false;
			foreach (var segment in _segments) {
				if (!segment.IsParameter) {
					truncated = !AppendCapped(output, segment.Literal);
				} else if (parameters == null || !parameters.TryGet(segment.Name, out var value)) {
					diagnostics?.MissingParameter(segment.Name);
				} else {
					truncated = !AppendCapped(output, Format(value, segment, diagnostics));
				}
				if (truncated) {
					break;
				}
			}
			if (truncated) {
				diagnostics?.TextTruncated();
			}
			return output.ToString();
		}

		public int Version(DmdParams parameters)
		{
			if (!IsDynamic) {
				return 0;
			}
			unchecked {
				var version = 17;
				foreach (var segment in _segments) {
					if (!segment.IsParameter) {
						continue;
					}
					version = version * 31 + segment.Name.GetHashCode();
					version = version * 31 + (parameters != null && parameters.TryGet(segment.Name, out var value)
						? value.GetHashCode()
						: 0);
				}
				return version;
			}
		}

		private static string Format(DmdParamValue value, Segment segment, CueDiagnostics diagnostics)
		{
			if (segment.Format == null) {
				return value.ToInvariantString();
			}
			try {
				object raw;
				switch (value.Type) {
					case DmdParamType.Integer:
						raw = value.IntValue;
						break;
					case DmdParamType.Float:
						raw = value.FloatValue;
						break;
					case DmdParamType.String:
						raw = value.StringValue ?? string.Empty;
						break;
					case DmdParamType.Boolean:
						raw = value.BoolValue;
						break;
					default:
						throw new ArgumentOutOfRangeException();
				}
				return string.Format(CultureInfo.InvariantCulture, segment.CompositeFormat, raw);
			} catch (FormatException) {
				diagnostics?.InvalidFormat(segment.Name, segment.Format);
				return value.ToInvariantString();
			}
		}

		private static string Truncate(string value, CueDiagnostics diagnostics)
		{
			if (value.Length <= DmdValidation.MaxResolvedTextLength) {
				return value;
			}
			diagnostics?.TextTruncated();
			return value.Substring(0, DmdValidation.MaxResolvedTextLength);
		}

		private static bool AppendCapped(StringBuilder output, string value)
		{
			var remaining = DmdValidation.MaxResolvedTextLength - output.Length;
			if (value.Length <= remaining) {
				output.Append(value);
				return true;
			}
			if (remaining > 0) {
				output.Append(value, 0, remaining);
			}
			return false;
		}

		private static void FlushLiteral(List<Segment> segments, StringBuilder literal)
		{
			if (literal.Length == 0) {
				return;
			}
			segments.Add(Segment.Text(literal.ToString()));
			literal.Clear();
		}

		private readonly struct Segment
		{
			public readonly string Literal;
			public readonly string Name;
			public readonly string Format;
			public readonly string CompositeFormat;
			public bool IsParameter => Name != null;

			private Segment(string literal, string name, string format)
			{
				Literal = literal;
				Name = name;
				Format = format;
				CompositeFormat = format == null ? null : $"{{0:{format}}}";
			}

			public static Segment Text(string value) => new Segment(value, null, null);
			public static Segment Parameter(string name, string format) => new Segment(null, name, format);
		}
	}
}
