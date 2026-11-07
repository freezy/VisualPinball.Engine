// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: GPL-3.0-or-later

using NUnit.Framework;
using System.Reflection;
using UnityEngine;
using VisualPinball.Unity.Simulation;

namespace VisualPinball.Unity.Test
{
	public class NudgeSensorSetupTests
	{
		private static NativeInputDeviceInfo Device(string name, float x = 0.1f, long timestamp = 1000, bool connected = true)
		{
			return new NativeInputDeviceInfo(0, "board", name, connected, new[] {
				new NativeInputAxisInfo(3, "X", 1, 0x30, NativeInputApi.AxisKind.Position, x, timestamp),
				new NativeInputAxisInfo(5, "Y", 1, 0x31, NativeInputApi.AxisKind.Position, 0.2f, timestamp),
				new NativeInputAxisInfo(7, "RX", 1, 0x33, NativeInputApi.AxisKind.Position, 0f, timestamp),
				new NativeInputAxisInfo(9, "RY", 1, 0x34, NativeInputApi.AxisKind.Position, 0f, timestamp)
			});
		}

		[Test]
		public void AutoSetupRejectsGenericGamepadXY()
		{
			Assert.That(NudgeSensorSetup.TryCreateCabinetSensor(Device("Xbox controller"), out _), Is.False);
			Assert.That(NudgeSensorSetup.TryCreateGamepadSensor(Device("Xbox controller"), out _), Is.False);
		}

		[Test]
		public void GamepadPresetUsesStandardizedLeftStickAxes()
		{
			var device = new NativeInputDeviceInfo(1, "sdl-gamepad:test", "Gamepad", true, new[] {
				new NativeInputAxisInfo(2, "Right Stick X", 0xff00, 2, NativeInputApi.AxisKind.Position, 0f, 0),
				new NativeInputAxisInfo(5, "Left Stick Y", 0xff00, 1, NativeInputApi.AxisKind.Position, 0f, 0),
				new NativeInputAxisInfo(3, "Left Stick X", 0xff00, 0, NativeInputApi.AxisKind.Position, 0f, 0)
			});
			Assert.That(NudgeSensorSetup.TryCreateGamepadSensor(device, out var settings), Is.True);
			var sensor = settings.ToEngineConfig();
			Assert.That(sensor.Type, Is.EqualTo(NudgeSensorType.GamepadIntent));
			Assert.That(sensor.X.DeviceId, Is.EqualTo(device.Id));
			Assert.That(sensor.X.AxisId, Is.EqualTo(3));
			Assert.That(sensor.Y.AxisId, Is.EqualTo(5));
			Assert.That(sensor.X.Kind, Is.EqualTo(SensorMappingKind.Position));
		}

		[TestCase("mjrnet Pinscape Controller")]
		[TestCase("Pinscape Controller")]
		[TestCase("PinscapePico")]
		public void PinscapeAccelerationUsesUsageInsteadOfAxisIndex(string name)
		{
			Assert.That(NudgeSensorSetup.TryCreateCabinetSensor(Device(name), out var sensor), Is.True);
			Assert.That(SensorMapping.TryParse(sensor.accX, out var x), Is.True);
			Assert.That(x.AxisId, Is.EqualTo(3));
			Assert.That(x.Kind, Is.EqualTo(SensorMappingKind.Acceleration));
			Assert.That(x.Scale, Is.EqualTo(NudgeSensorSetup.Gravity));
			Assert.That(sensor.velX, Is.Empty, "Velocity requires an explicit firmware/setup choice.");
		}

		[Test]
		public void PinscapeVelocityUsesRXRYAndDeviceUnits()
		{
			Assert.That(NudgeSensorSetup.TryCreatePinscapeSensor(Device("Pinscape Controller"), true, out var sensor), Is.True);
			Assert.That(SensorMapping.TryParse(sensor.velX, out var x), Is.True);
			Assert.That(SensorMapping.TryParse(sensor.velY, out var y), Is.True);
			Assert.That(x.AxisId, Is.EqualTo(7));
			Assert.That(y.AxisId, Is.EqualTo(9));
			Assert.That(x.Kind, Is.EqualTo(SensorMappingKind.Velocity));
			Assert.That(x.Scale, Is.EqualTo(0.2048f));
			Assert.That(x.DeadZone, Is.Zero);
			Assert.That(sensor.accX, Is.Empty);
		}

		[Test]
		public void DisconnectedDeviceCannotBeAutoMapped()
		{
			Assert.That(NudgeSensorSetup.TryCreateCabinetSensor(Device("Pinscape Controller", connected: false), out _), Is.False);
		}

		[Test]
		public void OpdDefaultsToAccelerationAndUsesThePicoVelocityScaleWhenSelected()
		{
			var device = new NativeInputDeviceInfo(1, "pico-opd", "Pinscape Pico (OPD)", true, new[] {
				new NativeInputAxisInfo(5, "Plunger Velocity", 5, 2, NativeInputApi.AxisKind.Velocity, 0f, 0),
				new NativeInputAxisInfo(3, "Nudge Y Velocity", 5, 2, NativeInputApi.AxisKind.Velocity, 0f, 0),
				new NativeInputAxisInfo(0, "Nudge X Acceleration", 5, 2, NativeInputApi.AxisKind.Acceleration, 0f, 0),
				new NativeInputAxisInfo(2, "Nudge X Velocity", 5, 2, NativeInputApi.AxisKind.Velocity, 0f, 0),
				new NativeInputAxisInfo(1, "Nudge Y Acceleration", 5, 2, NativeInputApi.AxisKind.Acceleration, 0f, 0)
			});
			Assert.That(NudgeSensorSetup.TryCreateCabinetSensor(device, out var acceleration), Is.True);
			var acc = acceleration.ToEngineConfig();
			Assert.That(acc.AccelerationX.AxisId, Is.Zero);
			Assert.That(acc.AccelerationY.AxisId, Is.EqualTo(1));
			Assert.That(acc.VelocityX.IsMapped, Is.False);
			Assert.That(NudgeSensorSetup.TryCreateCabinetPreset(device, true, out var velocity), Is.True);
			var vel = velocity.ToEngineConfig();
			Assert.That(vel.VelocityX.DeviceId, Is.EqualTo("pico-opd"));
			Assert.That(vel.VelocityX.AxisId, Is.EqualTo(2));
			Assert.That(vel.VelocityY.AxisId, Is.EqualTo(3));
			Assert.That(vel.VelocityX.Scale, Is.EqualTo(0.32768f));
			Assert.That(vel.AccelerationX.IsMapped, Is.False);
		}

		[Test]
		public void OpdVelocityPresetRequiresBothNudgeVelocityChannels()
		{
			var device = new NativeInputDeviceInfo(1, "pico-opd", "Pinscape Pico (OPD)", true, new[] {
				new NativeInputAxisInfo(2, "Nudge X Velocity", 5, 2, NativeInputApi.AxisKind.Velocity, 0f, 0),
				new NativeInputAxisInfo(5, "Plunger Velocity", 5, 2, NativeInputApi.AxisKind.Velocity, 0f, 0)
			});
			Assert.That(NudgeSensorSetup.TryCreateCabinetPreset(device, true, out _), Is.False);
		}

		[Test]
		public void ExistingDeviceCheckUsesOnlyTheActiveSensorChannels()
		{
			var sensor = new NudgeSensorConfig {
				Type = NudgeSensorType.GamepadIntent,
				Y = new SensorMapping { DeviceId = "stick", AxisId = 1 },
				VelocityY = new SensorMapping { DeviceId = "pico-hid", AxisId = 4 }
			};
			Assert.That(NudgeSensorSetup.IsMappedToDevice(sensor, "stick"), Is.True);
			Assert.That(NudgeSensorSetup.IsMappedToDevice(sensor, "pico-hid"), Is.False);
			sensor.Type = NudgeSensorType.CabinetDirect;
			Assert.That(NudgeSensorSetup.IsMappedToDevice(sensor, "pico-hid"), Is.True);
			Assert.That(NudgeSensorSetup.IsMappedToDevice(sensor, "stick"), Is.False);
		}

		private static NudgeSensorConfig Sensor()
		{
			NudgeSensorSetup.TryCreateCabinetSensor(Device("Pinscape Controller"), out var settings);
			return settings.ToEngineConfig();
		}

		[Test]
		public void CalibrationAveragesFreshSamplesAndPreservesGain()
		{
			var sensor = Sensor();
			sensor.AccelerationX.Scale = -19.6133f;
			var calibration = new NudgeSensorCalibration(new[] { sensor }, new[] { Device("Pinscape Controller") });
			for (var i = 1; i <= 20; i++) {
				calibration.Sample(new[] { Device("Pinscape Controller", i % 2 == 0 ? 0.102f : 0.098f, 1000 + i * 50000) });
			}
			Assert.That(calibration.TryComplete(out _), Is.True);
			Assert.That(sensor.AccelerationX.RawCenter, Is.EqualTo(0.1f).Within(0.00001f));
			Assert.That(sensor.AccelerationY.RawCenter, Is.EqualTo(0.2f).Within(0.00001f));
			Assert.That(sensor.AccelerationX.Scale, Is.EqualTo(-19.6133f));
		}

		[Test]
		public void RepeatedSnapshotsCannotCalibrateAStoppedDevice()
		{
			var sensor = Sensor();
			sensor.AccelerationX.RawCenter = -0.1f;
			var devices = new[] { Device("Pinscape Controller") };
			var calibration = new NudgeSensorCalibration(new[] { sensor }, devices);
			for (var i = 0; i < 30; i++) calibration.Sample(devices);
			Assert.That(calibration.TryComplete(out _), Is.False);
			Assert.That(sensor.AccelerationX.RawCenter, Is.EqualTo(-0.1f));
		}

		[TestCase(true)]
		[TestCase(false)]
		public void CalibrationFailureDoesNotPartiallyUpdateCenters(bool disconnect)
		{
			var sensor = Sensor();
			sensor.AccelerationX.RawCenter = -0.1f;
			sensor.AccelerationY.RawCenter = -0.2f;
			var calibration = new NudgeSensorCalibration(new[] { sensor }, new[] { Device("Pinscape Controller") });
			for (var i = 1; i <= 20; i++) {
				var x = !disconnect && i == 10 ? 0.5f : 0.1f;
				calibration.Sample(new[] { Device("Pinscape Controller", x, 1000 + i * 50000, !disconnect || i != 10) });
			}
			Assert.That(calibration.TryComplete(out _), Is.False);
			Assert.That(sensor.AccelerationX.RawCenter, Is.EqualTo(-0.1f));
			Assert.That(sensor.AccelerationY.RawCenter, Is.EqualTo(-0.2f));
		}

		[Test]
		public void DisconnectResetClearsMotionAndReconnectStartsAtRest()
		{
			var state = new NudgeState(KeyboardNudgeMode.CabModel, 1f, 5f);
			var config = new NudgeSensorRuntimeConfig { Type = NudgeSensorType.CabinetDirect, Strength = 1f, CabinetMassKg = 113f, AccelerationXMapped = 1 };
			state.ConfigureSensor(0, config);
			state.ApplySensorSample(0, NudgeSensorChannel.AccelerationX, -12f, 1000);
			for (var i = 0; i < 10; i++) state.StepOneMillisecond();
			Assert.That(state.ActiveSourceIndex, Is.EqualTo(0));
			state.ConfigureSensor(0, config, false);
			state.StepOneMillisecond();
			Assert.That(state.ActiveSourceIndex, Is.EqualTo(-2));
			Assert.That(state.CabinetAcceleration.x, Is.Zero);
			Assert.That(state.CabinetOffset.x, Is.Zero);
			state.ConfigureSensor(0, config, true);
			state.StepOneMillisecond();
			Assert.That(state.ActiveSourceIndex, Is.EqualTo(-2));
		}

		[Test]
		public void RemappingDropsPendingSamplesFromThePreviousDevice()
		{
			var go = new GameObject("Nudge input test");
			go.SetActive(false);
			try {
				var engine = go.AddComponent<PhysicsEngine>();
				var system = new NudgeSystem(engine);
				var context = (PhysicsEngineContext)typeof(PhysicsEngine).GetField("_ctx", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(engine);
				system.ConfigureSensors(new[] { Sensor() });
				engine.EnqueueNudgeSensorSample(0, NudgeSensorChannel.AccelerationX, 10f, 1000);
				system.ConfigureSensors(new[] { Sensor() });
				Assert.That(context.PendingNudgeSensorSamples, Is.Empty);
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void LosingOneDeviceResetsItsSensorInOrderWithoutResettingTheOther()
		{
			var go = new GameObject("Nudge hotplug test");
			go.SetActive(false);
			try {
				var engine = go.AddComponent<PhysicsEngine>();
				var system = new NudgeSystem(engine);
				var context = (PhysicsEngineContext)typeof(PhysicsEngine).GetField("_ctx", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(engine);
				var otherSensor = Sensor();
				otherSensor.AccelerationX.DeviceId = "other";
				otherSensor.AccelerationY.DeviceId = "other";
				var board = Device("Pinscape Controller");
				var other = new NativeInputDeviceInfo(1, "other", "Other sensor", true, board.Axes);
				system.UpdateConnectedDevices(new[] { board, other });
				system.ConfigureSensors(new[] { Sensor(), otherSensor });
				engine.EnqueueNudgeSensorSample(0, NudgeSensorChannel.AccelerationX, 10f, 1000);
				system.UpdateConnectedDevices(new[] { other });
				Assert.That(context.PendingNudgeSensorSamples.Count, Is.EqualTo(2));
				Assert.That(context.PendingNudgeSensorSamples.Dequeue().IsConfiguration, Is.False);
				var reset = context.PendingNudgeSensorSamples.Dequeue();
				Assert.That(reset.IsConfiguration, Is.True);
				Assert.That(reset.SensorIndex, Is.Zero);
				Assert.That(reset.Enabled, Is.False);
				system.UpdateConnectedDevices(new[] { board, other });
				Assert.That(context.PendingNudgeSensorSamples.Dequeue().Enabled, Is.True);
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void QueueOverflowRetainsDisconnectWhileDiscardingStaleMotion()
		{
			var go = new GameObject("Nudge backlog test");
			go.SetActive(false);
			try {
				var engine = go.AddComponent<PhysicsEngine>();
				var context = (PhysicsEngineContext)typeof(PhysicsEngine).GetField("_ctx", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(engine);
				engine.EnqueueNudgeSensorReset(0, default, false);
				for (var i = 0; i < 9000; i++) {
					engine.EnqueueNudgeSensorSample(1, NudgeSensorChannel.X, 0.1f, (ulong)i);
				}
				Assert.That(context.PendingNudgeSensorSamples.Count, Is.LessThanOrEqualTo(8192));
				var reset = context.PendingNudgeSensorSamples.Dequeue();
				Assert.That(reset.IsConfiguration, Is.True);
				Assert.That(reset.SensorIndex, Is.Zero);
				Assert.That(reset.Enabled, Is.False);
			} finally {
				Object.DestroyImmediate(go);
			}
		}
	}
}
