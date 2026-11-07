// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using Unity.Mathematics;
using VisualPinball.Unity.Simulation;

namespace VisualPinball.Unity
{
	/// <summary>Shared device recognition and cabinet-axis defaults for editor and player setup.</summary>
	public static class NudgeSensorSetup
	{
		public const float Gravity = 9.80665f;
		// Pinscape's default joystick velocity range is +/-4096 / 20 mm/s.
		public const float PinscapeVelocityScale = 0.2048f;

		public static bool IsPinscape(NativeInputDeviceInfo device)
		{
			var name = device?.Name ?? string.Empty;
			return name.StartsWith("mjrnet Pinscape Controller", StringComparison.OrdinalIgnoreCase)
				|| name.Equals("Pinscape Controller", StringComparison.OrdinalIgnoreCase)
				|| name.Equals("PinscapePico", StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>
		/// Proposes the standard Pinscape joystick layout. Velocity must be explicitly
		/// selected after enabling it in firmware; an RX/RY axis alone does not identify its meaning.
		/// </summary>
		public static bool TryCreatePinscapeSensor(NativeInputDeviceInfo device, bool velocity,
			out CabinetNudgeSensorSettings sensor)
		{
			sensor = null;
			if (device == null || !device.IsConnected || !IsPinscape(device)
				|| !TryFindUsage(device, velocity ? 0x33 : 0x30, out var x)
				|| !TryFindUsage(device, velocity ? 0x34 : 0x31, out var y)) {
				return false;
			}
			var kind = velocity ? SensorMappingKind.Velocity : SensorMappingKind.Acceleration;
			var scale = velocity ? PinscapeVelocityScale : Gravity;
			sensor = new CabinetNudgeSensorSettings { type = CabinetNudgeSensorSettings.TypeCabinetDirect };
			if (velocity) {
				sensor.velX = Mapping(device, x, kind, scale);
				sensor.velY = Mapping(device, y, kind, scale);
			} else {
				sensor.accX = Mapping(device, x, kind, scale);
				sensor.accY = Mapping(device, y, kind, scale);
			}
			return true;
		}

		/// <summary>Auto-maps recognized Pinscape layouts or explicitly typed X/Y accelerometer axes.</summary>
		public static bool TryCreateCabinetSensor(NativeInputDeviceInfo device, out CabinetNudgeSensorSettings sensor)
		{
			if (TryCreatePinscapeSensor(device, false, out sensor)) {
				return true;
			}
			if (device == null || !device.IsConnected
				|| !TryFindUsage(device, 0x30, out var x) || x.Kind != NativeInputApi.AxisKind.Acceleration
				|| !TryFindUsage(device, 0x31, out var y) || y.Kind != NativeInputApi.AxisKind.Acceleration) {
				return false;
			}
			sensor = new CabinetNudgeSensorSettings {
				type = CabinetNudgeSensorSettings.TypeCabinetDirect,
				accX = Mapping(device, x, SensorMappingKind.Acceleration, Gravity),
				accY = Mapping(device, y, SensorMappingKind.Acceleration, Gravity)
			};
			return true;
		}

		public static bool TryFindAxis(IReadOnlyList<NativeInputDeviceInfo> devices, SensorMapping mapping,
			out NativeInputAxisInfo axis)
		{
			if (devices != null && mapping != null) {
				foreach (var device in devices) {
					if (device == null || !device.IsConnected || device.Id != mapping.DeviceId || device.Axes == null) {
						continue;
					}
					foreach (var candidate in device.Axes) {
						if (candidate.AxisId == mapping.AxisId) {
							axis = candidate;
							return true;
						}
					}
				}
			}
			axis = default;
			return false;
		}

		private static bool TryFindUsage(NativeInputDeviceInfo device, int usage, out NativeInputAxisInfo axis)
		{
			if (device.Axes != null) {
				foreach (var candidate in device.Axes) {
					if (candidate.UsagePage == 1 && candidate.Usage == usage) {
						axis = candidate;
						return true;
					}
				}
			}
			axis = default;
			return false;
		}

		private static string Mapping(NativeInputDeviceInfo device, NativeInputAxisInfo axis, SensorMappingKind kind, float scale)
		{
			return new SensorMapping {
				DeviceId = device.Id, AxisId = axis.AxisId, Kind = kind, Scale = scale,
				DeadZone = kind == SensorMappingKind.Velocity ? 0f : 0.02f
			}.ToString();
		}
	}

	/// <summary>
	/// Collects fresh rest samples for a set of mapping copies. Call Sample for about
	/// one second, then TryComplete. Failure leaves all centers intact.
	/// </summary>
	public sealed class NudgeSensorCalibration
	{
		private sealed class Channel
		{
			public SensorMapping Mapping;
			public long Timestamp;
			public long FirstTimestamp;
			public int Count;
			public double Sum;
			public float Min = float.PositiveInfinity;
			public float Max = float.NegativeInfinity;
		}

		private readonly List<Channel> _channels = new();
		private bool _unavailable;

		public NudgeSensorCalibration(IReadOnlyList<NudgeSensorConfig> sensors, IReadOnlyList<NativeInputDeviceInfo> devices)
		{
			foreach (var sensor in sensors) {
				if (sensor.Type == NudgeSensorType.GamepadIntent) {
					Add(sensor.X, devices);
					Add(sensor.Y, devices);
				} else {
					Add(sensor.AccelerationX, devices);
					Add(sensor.AccelerationY, devices);
					Add(sensor.VelocityX, devices);
					Add(sensor.VelocityY, devices);
				}
			}
		}

		private void Add(SensorMapping mapping, IReadOnlyList<NativeInputDeviceInfo> devices)
		{
			if (mapping == null || !mapping.IsMapped) {
				return;
			}
			var present = NudgeSensorSetup.TryFindAxis(devices, mapping, out var axis);
			_unavailable |= !present;
			_channels.Add(new Channel { Mapping = mapping, Timestamp = present ? axis.TimestampUsec : 0 });
		}

		public void Sample(IReadOnlyList<NativeInputDeviceInfo> devices)
		{
			foreach (var channel in _channels) {
				if (!NudgeSensorSetup.TryFindAxis(devices, channel.Mapping, out var axis)) {
					_unavailable = true;
					continue;
				}
				if (axis.TimestampUsec <= channel.Timestamp || float.IsNaN(axis.RawValue) || float.IsInfinity(axis.RawValue)) {
					continue;
				}
				channel.Timestamp = axis.TimestampUsec;
				if (channel.Count == 0) channel.FirstTimestamp = axis.TimestampUsec;
				channel.Count++;
				channel.Sum += axis.RawValue;
				channel.Min = math.min(channel.Min, axis.RawValue);
				channel.Max = math.max(channel.Max, axis.RawValue);
			}
		}

		public bool TryComplete(out string message)
		{
			if (_channels.Count == 0 || _unavailable) {
				message = "Calibration needs connected devices for every mapped channel.";
				return false;
			}
			foreach (var channel in _channels) {
				if (channel.Count < 8 || channel.Timestamp - channel.FirstTimestamp < 500000) {
					message = "Not enough fresh input samples. Check the device and try again.";
					return false;
				}
				if (channel.Max - channel.Min > 0.04f) {
					message = "Input moved too much. Keep the cabinet still and try again.";
					return false;
				}
			}
			foreach (var channel in _channels) {
				channel.Mapping.RawCenter = (float)(channel.Sum / channel.Count);
			}
			message = $"Calibrated {_channels.Count} nudge channels at rest.";
			return true;
		}
	}
}
