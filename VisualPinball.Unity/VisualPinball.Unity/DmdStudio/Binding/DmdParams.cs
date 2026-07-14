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
	/// <summary>
	/// Mutable, ordinal-keyed values supplied to a cue instance.
	/// </summary>
	public sealed class DmdParams
	{
		private readonly Dictionary<string, DmdParamValue> _values =
			new Dictionary<string, DmdParamValue>(StringComparer.Ordinal);

		public int Count => _values.Count;
		internal int Version { get; private set; }

		public DmdParams Set(string name, int value) => Set(name, (long)value);

		public DmdParams Set(string name, long value) => SetValue(name, DmdParamValue.From(name, value));

		public DmdParams Set(string name, double value) => SetValue(name, DmdParamValue.From(name, value));

		public DmdParams Set(string name, string value)
		{
			return SetValue(name, DmdParamValue.From(name, value ?? string.Empty));
		}

		public DmdParams Set(string name, bool value) => SetValue(name, DmdParamValue.From(name, value));

		public bool TryGet(string name, out DmdParamValue value)
		{
			if (name == null) {
				throw new ArgumentNullException(nameof(name));
			}
			return _values.TryGetValue(name, out value);
		}

		internal IEnumerable<KeyValuePair<string, DmdParamValue>> Values => _values;

		internal DmdParams Clone()
		{
			var clone = new DmdParams();
			clone.MergeFrom(this);
			return clone;
		}

		internal void MergeFrom(DmdParams other)
		{
			if (other == null) {
				return;
			}

			foreach (var pair in other._values) {
				SetValue(pair.Key, pair.Value);
			}
		}

		private DmdParams SetValue(string name, DmdParamValue value)
		{
			DmdValidation.ValidateParameterName(name, nameof(name));
			if (!_values.ContainsKey(name) && _values.Count >= DmdValidation.MaxBoundParams) {
				throw new ArgumentException($"A cue cannot bind more than {DmdValidation.MaxBoundParams} parameters.", nameof(name));
			}

			if (!_values.TryGetValue(name, out var previous) || !previous.Equals(value)) {
				_values[name] = value;
				unchecked {
					Version++;
				}
			}
			return this;
		}
	}
}
