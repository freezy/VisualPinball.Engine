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
		public float RawValue { get; private set; }
		public float MappedValue { get; private set; }
		public long RawTimestampUsec { get; private set; }

		public bool IsMapped => !string.IsNullOrEmpty(DeviceId) && AxisId >= 0;

		public float ProcessRawValue(float rawValue, long timestampUsec)
		{
			RawValue = math.clamp(rawValue, -1f, 1f);
			RawTimestampUsec = timestampUsec;

			var deadZone = math.clamp(DeadZone, 0f, 0.999f);
			var value = RawValue;
			var absValue = math.abs(value);
			if (absValue <= deadZone) {
				value = 0f;
			} else {
				value = math.sign(value) * ((absValue - deadZone) / (1f - deadZone));
			}

			var limit = math.max(0f, Limit);
			if (limit > 0f) {
				value = math.clamp(value, -limit, limit);
			}
			MappedValue = value * Scale;
			return MappedValue;
		}

		public override string ToString()
		{
			if (!IsMapped) {
				return string.Empty;
			}

			return string.Join(";",
				DeviceId,
				AxisId.ToString(CultureInfo.InvariantCulture),
				KindToCode(Kind),
				DeadZone.ToString(CultureInfo.InvariantCulture),
				Scale.ToString(CultureInfo.InvariantCulture),
				Limit.ToString(CultureInfo.InvariantCulture)
			);
		}

		public static bool TryParse(string value, out SensorMapping mapping)
		{
			mapping = new SensorMapping();
			if (string.IsNullOrWhiteSpace(value)) {
				return false;
			}

			var parts = value.Split(';');
			if (parts.Length != 6) {
				return false;
			}

			if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var axisId)) {
				return false;
			}
			if (!TryCodeToKind(parts[2], out var kind)) {
				return false;
			}
			if (!float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var deadZone)) {
				return false;
			}
			if (!float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var scale)) {
				return false;
			}
			if (!float.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out var limit)) {
				return false;
			}

			mapping.DeviceId = parts[0];
			mapping.AxisId = axisId;
			mapping.Kind = kind;
			mapping.DeadZone = math.clamp(deadZone, 0f, 0.999f);
			mapping.Scale = scale;
			mapping.Limit = math.max(0f, limit);
			return mapping.IsMapped;
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
