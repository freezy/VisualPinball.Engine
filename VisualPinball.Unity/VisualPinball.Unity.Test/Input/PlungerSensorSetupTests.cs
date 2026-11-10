// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: GPL-3.0-or-later

using NUnit.Framework;
using VisualPinball.Unity.Simulation;

namespace VisualPinball.Unity.Test
{
	public class PlungerSensorSetupTests
	{
		private static NativeInputDeviceInfo Opd(bool withPlunger = true)
		{
			return withPlunger
				? new NativeInputDeviceInfo(1, "pico-opd", "Pinscape Pico (OPD)", true, new[] {
					new NativeInputAxisInfo(0, "Nudge X Acceleration", 5, 2, NativeInputApi.AxisKind.Acceleration, 0f, 0),
					new NativeInputAxisInfo(4, "Plunger Position", 5, 2, NativeInputApi.AxisKind.Position, 0f, 0),
					new NativeInputAxisInfo(5, "Plunger Velocity", 5, 2, NativeInputApi.AxisKind.Velocity, 0f, 0)
				})
				: new NativeInputDeviceInfo(1, "pico-opd", "Pinscape Pico (OPD)", true, new[] {
					new NativeInputAxisInfo(0, "Nudge X Acceleration", 5, 2, NativeInputApi.AxisKind.Acceleration, 0f, 0)
				});
		}

		private static NativeInputDeviceInfo Pinscape(bool withUsages = true)
		{
			return withUsages
				? new NativeInputDeviceInfo(0, "kl25z", "Pinscape Controller", true, new[] {
					new NativeInputAxisInfo(0, "X", 1, 0x30, NativeInputApi.AxisKind.Position, 0f, 0),
					new NativeInputAxisInfo(1, "Y", 1, 0x31, NativeInputApi.AxisKind.Position, 0f, 0),
					new NativeInputAxisInfo(2, "Z", 1, 0x32, NativeInputApi.AxisKind.Position, 0f, 0),
					new NativeInputAxisInfo(5, "RZ", 1, 0x35, NativeInputApi.AxisKind.Position, 0f, 0)
				})
				: new NativeInputDeviceInfo(0, "kl25z", "Pinscape Controller", true, new[] {
					new NativeInputAxisInfo(0, "Axis 1", 0, 0, NativeInputApi.AxisKind.Position, 0f, 0),
					new NativeInputAxisInfo(1, "Axis 2", 0, 0, NativeInputApi.AxisKind.Position, 0f, 0),
					new NativeInputAxisInfo(2, "Axis 3", 0, 0, NativeInputApi.AxisKind.Position, 0f, 0)
				});
		}

		private static SensorMapping Parse(string value)
		{
			Assert.That(SensorMapping.TryParse(value, out var mapping), Is.True, value);
			return mapping;
		}

		[Test]
		public void OpdPresetMapsPlungerPositionAndVelocity()
		{
			Assert.That(PlungerSensorSetup.TryCreatePreset(Opd(), true, out var sensor), Is.True);
			var position = Parse(sensor.position);
			var velocity = Parse(sensor.velocity);
			Assert.That(position.AxisId, Is.EqualTo(4));
			Assert.That(position.Kind, Is.EqualTo(SensorMappingKind.Position));
			Assert.That(velocity.AxisId, Is.EqualTo(5));
			Assert.That(velocity.Kind, Is.EqualTo(SensorMappingKind.Velocity));
			Assert.That(velocity.Scale, Is.EqualTo(PlungerSensorSetup.PinscapeVelocityScale));
		}

		[Test]
		public void OpdPresetWithoutVelocityLeavesItUnmapped()
		{
			Assert.That(PlungerSensorSetup.TryCreatePreset(Opd(), false, out var sensor), Is.True);
			Assert.That(sensor.velocity, Is.Empty);
		}

		[Test]
		public void ShortOpdReportHasNoPreset()
		{
			Assert.That(PlungerSensorSetup.GetDeviceKind(Opd(false)), Is.EqualTo(PlungerSensorSetup.DeviceKind.OpenPinballDevice));
			Assert.That(PlungerSensorSetup.TryCreatePreset(Opd(false), true, out _), Is.False);
		}

		[Test]
		public void PinscapePresetUsesZAndRz()
		{
			Assert.That(PlungerSensorSetup.TryCreatePreset(Pinscape(), true, out var sensor), Is.True);
			Assert.That(Parse(sensor.position).AxisId, Is.EqualTo(2));
			Assert.That(Parse(sensor.velocity).AxisId, Is.EqualTo(5));
		}

		[Test]
		public void PinscapeWithoutUsagesFallsBackToTheThirdAxis()
		{
			Assert.That(PlungerSensorSetup.TryCreatePreset(Pinscape(false), true, out var sensor), Is.True);
			Assert.That(Parse(sensor.position).AxisId, Is.EqualTo(2));
			Assert.That(sensor.velocity, Is.Empty);
		}

		[Test]
		public void GamepadPresetUsesTheRightStickWithADeadZone()
		{
			var device = new NativeInputDeviceInfo(2, "sdl-gamepad:test", "Gamepad", true, new[] {
				new NativeInputAxisInfo(1, "Left Stick Y", 0xff00, 1, NativeInputApi.AxisKind.Position, 0f, 0),
				new NativeInputAxisInfo(7, "Right Stick Y", 0xff00, 3, NativeInputApi.AxisKind.Position, 0f, 0)
			});
			Assert.That(PlungerSensorSetup.GetDeviceKind(device), Is.EqualTo(PlungerSensorSetup.DeviceKind.Gamepad));
			Assert.That(PlungerSensorSetup.TryCreatePreset(device, true, out var sensor), Is.True);
			var position = Parse(sensor.position);
			Assert.That(position.AxisId, Is.EqualTo(7));
			Assert.That(position.DeadZone, Is.EqualTo(PlungerSensorSetup.GamepadDeadZone));
			Assert.That(sensor.velocity, Is.Empty);
		}

		[Test]
		public void UnknownDeviceHasNoPreset()
		{
			var device = new NativeInputDeviceInfo(3, "joy", "Some Joystick", true, new[] {
				new NativeInputAxisInfo(0, "X", 1, 0x30, NativeInputApi.AxisKind.Position, 0f, 0)
			});
			Assert.That(PlungerSensorSetup.GetDeviceKind(device), Is.EqualTo(PlungerSensorSetup.DeviceKind.Generic));
			Assert.That(PlungerSensorSetup.TryCreatePreset(device, true, out _), Is.False);
		}

		[Test]
		public void CalibrationSolvesTheFullRange()
		{
			var mapping = new SensorMapping { DeviceId = "d", AxisId = 0 };
			Assert.That(PlungerCalibrationMath.TryApply(mapping, -1f, 1f, out _), Is.True);
			Assert.That(mapping.Limit, Is.EqualTo(2f).Within(1e-6f));
			Assert.That(mapping.Scale, Is.EqualTo(0.5f).Within(1e-6f));
			Assert.That(mapping.ProcessRawValue(-1f, 1), Is.EqualTo(0f).Within(1e-6f));
			Assert.That(mapping.ProcessRawValue(1f, 2), Is.EqualTo(1f).Within(1e-6f));
			Assert.That(mapping.ProcessRawValue(0f, 3), Is.EqualTo(0.5f).Within(1e-6f));
		}

		[Test]
		public void CalibrationAccountsForTheDeadZone()
		{
			var mapping = new SensorMapping { DeviceId = "d", AxisId = 0, DeadZone = 0.1f };
			Assert.That(PlungerCalibrationMath.TryApply(mapping, 0f, 0.5f, out _), Is.True);
			Assert.That(mapping.Scale, Is.EqualTo(9f / 4f).Within(1e-5f));
			Assert.That(mapping.ProcessRawValue(0.5f, 1), Is.EqualTo(1f).Within(1e-5f));
			Assert.That(mapping.ProcessRawValue(0.3f, 2), Is.EqualTo(0.5f).Within(1e-5f));
		}

		[Test]
		public void CalibrationOfAnInvertedAxisReadsPullAsPositive()
		{
			var mapping = new SensorMapping { DeviceId = "d", AxisId = 0 };
			Assert.That(PlungerCalibrationMath.TryApply(mapping, 0.2f, -0.6f, out _), Is.True);
			Assert.That(mapping.ProcessRawValue(-0.6f, 1), Is.EqualTo(1f).Within(1e-5f));
		}

		[Test]
		public void CalibrationRejectsTinyTravelAndKeepsTheMapping()
		{
			var mapping = new SensorMapping { DeviceId = "d", AxisId = 0, DeadZone = 0.1f, Scale = 3f };
			Assert.That(PlungerCalibrationMath.TryApply(mapping, 0f, 0.12f, out var message), Is.False);
			Assert.That(message, Is.Not.Empty);
			Assert.That(mapping.Scale, Is.EqualTo(3f));
		}

		[Test]
		public void ChangingTheDeadZoneKeepsFullPullAtOne()
		{
			var mapping = new SensorMapping { DeviceId = "d", AxisId = 0 };
			Assert.That(PlungerCalibrationMath.TryApply(mapping, 0.1f, 0.9f, out _), Is.True);
			Assert.That(PlungerCalibrationMath.TryChangeDeadZone(mapping, 0.2f, out _), Is.True);
			Assert.That(mapping.ProcessRawValue(0.9f, 1), Is.EqualTo(1f).Within(1e-5f));
			Assert.That(PlungerCalibrationMath.TryChangeDeadZone(mapping, 0.9f, out _), Is.False);
			Assert.That(mapping.DeadZone, Is.EqualTo(0.2f).Within(1e-6f));
		}

		[Test]
		public void TwoStepCalibrationSessionUpdatesTheMapping()
		{
			var mapping = new SensorMapping { DeviceId = "d", AxisId = 0 };
			var calibration = new PlungerSensorCalibration(mapping);
			long t = 1000;
			for (var i = 0; i < 100; i++) {
				calibration.Add(0.1f + (i % 2) * 0.01f, t += 10000);
			}
			Assert.That(calibration.TryCompleteStep(out _), Is.True);
			Assert.That(calibration.CurrentStep, Is.EqualTo(PlungerSensorCalibration.Step.Pull));
			Assert.That(mapping.Scale, Is.EqualTo(1f), "mapping only changes when done");

			for (var i = 0; i < 100; i++) {
				calibration.Add(Ramp(0.1f, 0.9f, i / 50f), t += 10000);
			}
			Assert.That(calibration.TryCompleteStep(out _), Is.True);
			Assert.That(calibration.CurrentStep, Is.EqualTo(PlungerSensorCalibration.Step.Done));
			Assert.That(mapping.RawCenter, Is.EqualTo(0.105f).Within(1e-4f));
			Assert.That(mapping.ProcessRawValue(0.9f, t + 1), Is.EqualTo(1f).Within(1e-4f));
		}

		[Test]
		public void RestStepRejectsMovement()
		{
			var calibration = new PlungerSensorCalibration(new SensorMapping { DeviceId = "d", AxisId = 0 });
			long t = 1000;
			for (var i = 0; i < 100; i++) {
				calibration.Add(i * 0.01f, t += 10000);
			}
			Assert.That(calibration.TryCompleteStep(out _), Is.False);
			Assert.That(calibration.CurrentStep, Is.EqualTo(PlungerSensorCalibration.Step.Rest));
		}

		[Test]
		public void CalibrationNeedsFreshSamples()
		{
			var calibration = new PlungerSensorCalibration(new SensorMapping { DeviceId = "d", AxisId = 0 });
			for (var i = 0; i < 100; i++) {
				calibration.Add(0f, 5000);
			}
			Assert.That(calibration.TryCompleteStep(out _), Is.False);
		}

		[Test]
		public void RetryAfterADisconnectStartsTheStepOver()
		{
			var calibration = new PlungerSensorCalibration(new SensorMapping { DeviceId = "d", AxisId = 0 });
			calibration.Sample(System.Array.Empty<NativeInputDeviceInfo>());
			Assert.That(calibration.TryCompleteStep(out _), Is.False);

			calibration.RestartStep();
			long t = 1000;
			for (var i = 0; i < 100; i++) {
				calibration.Add(0.1f, t += 10000);
			}
			Assert.That(calibration.TryCompleteStep(out _), Is.True);
			Assert.That(calibration.CurrentStep, Is.EqualTo(PlungerSensorCalibration.Step.Pull));
		}

		private static float Ramp(float a, float b, float t) => a + (b - a) * System.Math.Min(1f, t);
	}
}
