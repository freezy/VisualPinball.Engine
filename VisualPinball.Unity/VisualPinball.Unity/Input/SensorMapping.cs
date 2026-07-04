// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Globalization;
using Unity.Mathematics;

namespace VisualPinball.Unity
{
	public enum SensorMappingKind
	{
		Position,
		Velocity,
		Acceleration
	}

	public sealed class SensorMapping
	{
		public string DeviceId { get; set; } = string.Empty;
		public int AxisId { get; set; } = -1;
		public SensorMappingKind Kind { get; set; } = SensorMappingKind.Position;
		public float DeadZone { get; set; }
		public float Scale { get; set; } = 1f;
		public float Limit { get; set; } = 1f;
		public float RawCenter { get; set; }
		public float RawValue { get; private set; }
		public float MappedValue { get; private set; }
		public long RawTimestampUsec { get; private set; }

		public bool IsMapped => !string.IsNullOrEmpty(DeviceId) && AxisId >= 0;

		public float ProcessRawValue(float rawValue, long timestampUsec)
		{
			RawValue = rawValue;
			RawTimestampUsec = timestampUsec;

			var deadZone = math.clamp(DeadZone, 0f, 0.999f);
			var value = RawValue - RawCenter;
			var absValue = math.abs(value);
			if (absValue <= deadZone) {
				value = 0f;
			} else {
				value = math.sign(value) * ((absValue - deadZone) / (1f - deadZone));
			}

			// Always clamp, like the reference: a limit of 0 zeroes the output rather
			// than meaning "unlimited".
			var limit = math.max(0f, Limit);
			value = math.clamp(value, -limit, limit);
			MappedValue = value * Scale;
			return MappedValue;
		}

		public override string ToString()
		{
			if (!IsMapped) {
				return string.Empty;
			}

			var value = string.Join(";",
				DeviceId,
				AxisId.ToString(CultureInfo.InvariantCulture),
				KindToCode(Kind),
				DeadZone.ToString(CultureInfo.InvariantCulture),
				Scale.ToString(CultureInfo.InvariantCulture),
				Limit.ToString(CultureInfo.InvariantCulture)
			);
			if (math.abs(RawCenter) > 1.0e-6f) {
				value += ";" + RawCenter.ToString(CultureInfo.InvariantCulture);
			}
			return value;
		}

		public static bool TryParse(string value, out SensorMapping mapping)
		{
			mapping = new SensorMapping();
			if (string.IsNullOrWhiteSpace(value)) {
				return false;
			}

			var parts = value.Split(';');
			if (parts.Length < 6) {
				return false;
			}

			if (TryParseParts(parts, 6, out mapping)) {
				return mapping.IsMapped;
			}
			if (TryParseParts(parts, 5, out mapping)) {
				return mapping.IsMapped;
			}
			mapping = new SensorMapping();
			return false;
		}

		private static bool TryParseParts(string[] parts, int trailingValueCount, out SensorMapping mapping)
		{
			mapping = new SensorMapping();

			// The device id is a free-form native path that may itself contain ';';
			// the trailing fields never do, so parse from the end and re-join whatever precedes them.
			var p = parts.Length - trailingValueCount;
			if (p <= 0) {
				return false;
			}
			var deviceId = p == 1 ? parts[0] : string.Join(";", parts, 0, p);

			if (!int.TryParse(parts[p], NumberStyles.Integer, CultureInfo.InvariantCulture, out var axisId)) {
				return false;
			}
			if (!TryCodeToKind(parts[p + 1], out var kind)) {
				return false;
			}
			if (!float.TryParse(parts[p + 2], NumberStyles.Float, CultureInfo.InvariantCulture, out var deadZone)) {
				return false;
			}
			if (!float.TryParse(parts[p + 3], NumberStyles.Float, CultureInfo.InvariantCulture, out var scale)) {
				return false;
			}
			if (!float.TryParse(parts[p + 4], NumberStyles.Float, CultureInfo.InvariantCulture, out var limit)) {
				return false;
			}
			var rawCenter = 0f;
			if (trailingValueCount > 5 &&
			    !float.TryParse(parts[p + 5], NumberStyles.Float, CultureInfo.InvariantCulture, out rawCenter)) {
				return false;
			}

			mapping.DeviceId = deviceId;
			mapping.AxisId = axisId;
			mapping.Kind = kind;
			mapping.DeadZone = math.clamp(deadZone, 0f, 0.999f);
			mapping.Scale = scale;
			mapping.Limit = math.max(0f, limit);
			mapping.RawCenter = rawCenter;
			return true;
		}

		private static string KindToCode(SensorMappingKind kind)
		{
			return kind switch {
				SensorMappingKind.Velocity => "V",
				SensorMappingKind.Acceleration => "A",
				_ => "P"
			};
		}

		private static bool TryCodeToKind(string code, out SensorMappingKind kind)
		{
			switch (code) {
				case "P":
					kind = SensorMappingKind.Position;
					return true;
				case "V":
					kind = SensorMappingKind.Velocity;
					return true;
				case "A":
					kind = SensorMappingKind.Acceleration;
					return true;
				default:
					kind = default;
					return false;
			}
		}
	}
}
