// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Globalization;

namespace VisualPinball.Unity
{
	public enum DmdParamType
	{
		Integer,
		Float,
		String,
		Boolean,
	}

	[Serializable]
	public struct DmdParamValue : IEquatable<DmdParamValue>
	{
		public string Name;
		public DmdParamType Type;
		public long IntValue;
		public double FloatValue;
		public string StringValue;
		public bool BoolValue;

		public static DmdParamValue From(string name, long value) => new DmdParamValue {
			Name = name,
			Type = DmdParamType.Integer,
			IntValue = value
		};

		public static DmdParamValue From(string name, double value) => new DmdParamValue {
			Name = name,
			Type = DmdParamType.Float,
			FloatValue = value
		};

		public static DmdParamValue From(string name, string value) => new DmdParamValue {
			Name = name,
			Type = DmdParamType.String,
			StringValue = value
		};

		public static DmdParamValue From(string name, bool value) => new DmdParamValue {
			Name = name,
			Type = DmdParamType.Boolean,
			BoolValue = value
		};

		public string ToInvariantString()
		{
			switch (Type) {
				case DmdParamType.Integer:
					return IntValue.ToString(CultureInfo.InvariantCulture);
				case DmdParamType.Float:
					return FloatValue.ToString(CultureInfo.InvariantCulture);
				case DmdParamType.String:
					return StringValue ?? string.Empty;
				case DmdParamType.Boolean:
					return BoolValue.ToString(CultureInfo.InvariantCulture);
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		public bool Equals(DmdParamValue other)
		{
			return Name == other.Name && Type == other.Type && IntValue == other.IntValue &&
			       FloatValue.Equals(other.FloatValue) && StringValue == other.StringValue &&
			       BoolValue == other.BoolValue;
		}

		public override bool Equals(object obj) => obj is DmdParamValue other && Equals(other);

		public override int GetHashCode()
		{
			unchecked {
				var hashCode = Name != null ? Name.GetHashCode() : 0;
				hashCode = (hashCode * 397) ^ (int)Type;
				hashCode = (hashCode * 397) ^ IntValue.GetHashCode();
				hashCode = (hashCode * 397) ^ FloatValue.GetHashCode();
				hashCode = (hashCode * 397) ^ (StringValue != null ? StringValue.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ BoolValue.GetHashCode();
				return hashCode;
			}
		}
	}
}
