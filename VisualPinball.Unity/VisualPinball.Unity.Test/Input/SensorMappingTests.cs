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

using NUnit.Framework;

namespace VisualPinball.Unity.Test
{
	public class SensorMappingTests
	{
		[Test]
		public void MappingRoundTripsUsingVpinballFormat()
		{
			var mapping = new SensorMapping {
				DeviceId = "HID\\VID_1209&PID_EAEA",
				AxisId = 3,
				Kind = SensorMappingKind.Velocity,
				DeadZone = 0.05f,
				Scale = -2f,
				Limit = 0.75f
			};

			Assert.That(SensorMapping.TryParse(mapping.ToString(), out var parsed), Is.True);
			Assert.That(parsed.DeviceId, Is.EqualTo(mapping.DeviceId));
			Assert.That(parsed.AxisId, Is.EqualTo(mapping.AxisId));
			Assert.That(parsed.Kind, Is.EqualTo(mapping.Kind));
			Assert.That(parsed.DeadZone, Is.EqualTo(mapping.DeadZone).Within(0.0001f));
			Assert.That(parsed.Scale, Is.EqualTo(mapping.Scale).Within(0.0001f));
			Assert.That(parsed.Limit, Is.EqualTo(mapping.Limit).Within(0.0001f));
		}

		[Test]
		public void MappingRoundTripsOptionalRawCenter()
		{
			var mapping = new SensorMapping {
				DeviceId = "HID\\VID_1209&PID_EAEA",
				AxisId = 1,
				Kind = SensorMappingKind.Acceleration,
				DeadZone = 0.02f,
				Scale = 9.81f,
				Limit = 1f,
				RawCenter = -0.125f
			};

			Assert.That(SensorMapping.TryParse(mapping.ToString(), out var parsed), Is.True);
			Assert.That(parsed.RawCenter, Is.EqualTo(mapping.RawCenter).Within(0.0001f));
		}

		[Test]
		public void MappingRoundTripsWithSemicolonInDeviceId()
		{
			var mapping = new SensorMapping {
				DeviceId = @"\\?\hid#vid_1209&pid_eaea;col02#8&2f&0&1",
				AxisId = 0,
				Kind = SensorMappingKind.Acceleration,
				DeadZone = 0f,
				Scale = 9.81f,
				Limit = 1f
			};

			Assert.That(SensorMapping.TryParse(mapping.ToString(), out var parsed), Is.True);
			Assert.That(parsed.DeviceId, Is.EqualTo(mapping.DeviceId));
			Assert.That(parsed.AxisId, Is.EqualTo(mapping.AxisId));
		}

		[Test]
		public void ProcessingAppliesDeadZoneLimitAndScale()
		{
			var mapping = new SensorMapping {
				DeviceId = "device",
				AxisId = 1,
				Kind = SensorMappingKind.Acceleration,
				DeadZone = 0.1f,
				Scale = 10f,
				Limit = 0.5f
			};

			Assert.That(mapping.ProcessRawValue(0.05f, 100), Is.EqualTo(0f));
			Assert.That(mapping.ProcessRawValue(1f, 200), Is.EqualTo(5f));
			Assert.That(mapping.RawTimestampUsec, Is.EqualTo(200));
		}

		[Test]
		public void ProcessingSubtractsRawCenterBeforeDeadZone()
		{
			var mapping = new SensorMapping {
				DeviceId = "device",
				AxisId = 1,
				Kind = SensorMappingKind.Acceleration,
				DeadZone = 0.05f,
				Scale = 10f,
				Limit = 1f,
				RawCenter = 0.2f
			};

			Assert.That(mapping.ProcessRawValue(0.22f, 100), Is.EqualTo(0f));
			Assert.That(mapping.ProcessRawValue(0.65f, 200), Is.GreaterThan(0f));
		}
	}
}
