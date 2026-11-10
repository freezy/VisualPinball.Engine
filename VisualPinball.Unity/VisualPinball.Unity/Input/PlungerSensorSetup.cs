// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Generic;
using Unity.Mathematics;
using VisualPinball.Unity.Simulation;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Device recognition and default plunger mappings, shared by the editor and the player.
	/// </summary>
	public static class PlungerSensorSetup
	{
		/// <summary>
		/// Pinscape (KL25Z joystick RZ and Pico OPD) reports the plunger speed in
		/// position units per centisecond, so a normalized reading times 100 is
		/// position units per second.
		/// </summary>
		public const float PinscapeVelocityScale = 100f;

		/// <summary>Dead zone of a gamepad stick used as plunger.</summary>
		public const float GamepadDeadZone = 0.05f;

		private const int OpdUsagePage = 0x05;
		private const int OpdUsage = 0x02;
		private const int OpdPositionAxisId = 4;
		private const int OpdVelocityAxisId = 5;
		private const int JoystickUsagePage = 0x01;
		private const int JoystickZ = 0x32;
		private const int JoystickRz = 0x35;
		private const int PinscapeFallbackPositionAxisId = 2; // "Axis 3" when SDL doesn't report the HID usages
		private const int GamepadUsagePage = 0xff00; // VPE_GAMEPAD_AXIS_USAGE_PAGE
		private const int GamepadRightStickY = 3; // SDL_GAMEPAD_AXIS_RIGHTY, positive is down

		public enum DeviceKind
		{
			/// <summary>No known plunger layout, axes must be picked manually.</summary>
			Generic,
			OpenPinballDevice,
			Pinscape,
			Gamepad
		}

		public static DeviceKind GetDeviceKind(NativeInputDeviceInfo device)
		{
			if (device == null) {
				return DeviceKind.Generic;
			}
			if (NudgeSensorSetup.IsOpenPinballDevice(device)) {
				return DeviceKind.OpenPinballDevice;
			}
			if (NudgeSensorSetup.IsPinscape(device)) {
				return DeviceKind.Pinscape;
			}
			return TryFindAxis(device, GamepadUsagePage, GamepadRightStickY, out _) ? DeviceKind.Gamepad : DeviceKind.Generic;
		}

		/// <summary>
		/// Position axis of a recognized device.
		/// </summary>
		public static bool TryFindPositionAxis(NativeInputDeviceInfo device, out NativeInputAxisInfo axis)
		{
			axis = default;
			if (device?.Axes == null) {
				return false;
			}
			switch (GetDeviceKind(device)) {
				case DeviceKind.OpenPinballDevice:
					// Short OPD reports may omit the plunger fields; only listed axes count.
					return TryFindOpdAxis(device, OpdPositionAxisId, NativeInputApi.AxisKind.Position, out axis);
				case DeviceKind.Pinscape:
					if (TryFindAxis(device, JoystickUsagePage, JoystickZ, out axis)) {
						return true;
					}
					return device.Axes.Count < 6 && TryFindAxisId(device, PinscapeFallbackPositionAxisId, out axis);
				case DeviceKind.Gamepad:
					return TryFindAxis(device, GamepadUsagePage, GamepadRightStickY, out axis);
				default:
					return false;
			}
		}

		/// <summary>
		/// Velocity axis of a recognized device. For Pinscape joysticks the RZ
		/// axis only carries the speed if the firmware is set up to report it.
		/// </summary>
		public static bool TryFindVelocityAxis(NativeInputDeviceInfo device, out NativeInputAxisInfo axis)
		{
			axis = default;
			if (device?.Axes == null) {
				return false;
			}
			return GetDeviceKind(device) switch {
				DeviceKind.OpenPinballDevice => TryFindOpdAxis(device, OpdVelocityAxisId, NativeInputApi.AxisKind.Velocity, out axis),
				DeviceKind.Pinscape => TryFindAxis(device, JoystickUsagePage, JoystickRz, out axis),
				_ => false
			};
		}

		/// <summary>
		/// Creates the default plunger sensor of a recognized device.
		/// </summary>
		/// <param name="velocity">Also map the device's velocity axis, if it has one.</param>
		public static bool TryCreatePreset(NativeInputDeviceInfo device, bool velocity, out CabinetPlungerSensorSettings sensor)
		{
			sensor = null;
			if (device == null || !device.IsConnected || !TryFindPositionAxis(device, out var position)) {
				return false;
			}
			var kind = GetDeviceKind(device);
			var deadZone = kind == DeviceKind.Gamepad ? GamepadDeadZone : 0f;
			sensor = new CabinetPlungerSensorSettings {
				deviceId = device.Id,
				deviceName = device.Name,
				position = Mapping(device, position.AxisId, SensorMappingKind.Position, 1f, deadZone)
			};
			if (velocity && TryFindVelocityAxis(device, out var velocityAxis)) {
				sensor.velocity = Mapping(device, velocityAxis.AxisId, SensorMappingKind.Velocity, PinscapeVelocityScale, 0f);
			}
			return true;
		}

		/// <summary>
		/// Creates a sensor from explicitly picked axes. A negative velocity axis id means none.
		/// </summary>
		public static CabinetPlungerSensorSettings CreateManual(NativeInputDeviceInfo device, int positionAxisId, int velocityAxisId,
			float velocityScale = PinscapeVelocityScale)
		{
			return new CabinetPlungerSensorSettings {
				deviceId = device?.Id,
				deviceName = device?.Name,
				position = device == null || positionAxisId < 0
					? string.Empty
					: Mapping(device, positionAxisId, SensorMappingKind.Position, 1f, 0f),
				velocity = device == null || velocityAxisId < 0
					? string.Empty
					: Mapping(device, velocityAxisId, SensorMappingKind.Velocity, velocityScale, 0f)
			};
		}

		private static bool TryFindOpdAxis(NativeInputDeviceInfo device, int axisId, NativeInputApi.AxisKind kind, out NativeInputAxisInfo axis)
		{
			foreach (var candidate in device.Axes) {
				if (candidate.UsagePage == OpdUsagePage && candidate.Usage == OpdUsage && candidate.AxisId == axisId && candidate.Kind == kind) {
					axis = candidate;
					return true;
				}
			}
			axis = default;
			return false;
		}

		private static bool TryFindAxis(NativeInputDeviceInfo device, int page, int usage, out NativeInputAxisInfo axis)
		{
			if (device.Axes != null) {
				foreach (var candidate in device.Axes) {
					if (candidate.UsagePage == page && candidate.Usage == usage) {
						axis = candidate;
						return true;
					}
				}
			}
			axis = default;
			return false;
		}

		private static bool TryFindAxisId(NativeInputDeviceInfo device, int axisId, out NativeInputAxisInfo axis)
		{
			foreach (var candidate in device.Axes) {
				if (candidate.AxisId == axisId) {
					axis = candidate;
					return true;
				}
			}
			axis = default;
			return false;
		}

		private static string Mapping(NativeInputDeviceInfo device, int axisId, SensorMappingKind kind, float scale, float deadZone)
		{
			return new SensorMapping {
				DeviceId = device.Id, AxisId = axisId, Kind = kind, Scale = scale, DeadZone = deadZone
			}.ToString();
		}
	}

	/// <summary>
	/// Two-point calibration of a plunger position mapping.
	/// </summary>
	/// <remarks>
	/// <see cref="SensorMapping.ProcessRawValue"/> subtracts the raw center,
	/// renormalizes the dead zone, clamps to ±Limit and then scales. To read 0
	/// at rest and exactly 1 at full pull, limit and scale are solved through
	/// that whole chain, not as 1/travel.
	/// </remarks>
	public static class PlungerCalibrationMath
	{
		/// <summary>The travel must exceed the dead zone by at least this much.</summary>
		public const float MinTravelBeyondDeadZone = 0.05f;

		/// <summary>
		/// Solves limit and scale for a raw travel (full pull minus rest) and a dead zone.
		/// </summary>
		public static bool TrySolve(float travel, float deadZone, out float limit, out float scale, out string message)
		{
			limit = 1f;
			scale = 1f;
			var d = math.clamp(deadZone, 0f, 0.999f);
			if (!math.isfinite(travel)) {
				message = "The plunger reading is not a number. Check the device.";
				return false;
			}
			if (math.abs(travel) < d + MinTravelBeyondDeadZone) {
				message = "The plunger barely moved. Pull it all the way back and try again.";
				return false;
			}
			var v = (math.abs(travel) - d) / (1f - d);
			limit = v;
			scale = math.sign(travel) / v;
			message = null;
			return true;
		}

		/// <summary>
		/// The raw travel a calibrated mapping was solved for, recovered from its limit, scale and dead zone.
		/// </summary>
		public static float GetTravel(SensorMapping mapping)
		{
			var d = math.clamp(mapping.DeadZone, 0f, 0.999f);
			var magnitude = mapping.Limit * (1f - d) + d;
			return mapping.Scale < 0f ? -magnitude : magnitude;
		}

		/// <summary>
		/// Applies a calibration result to a mapping.
		/// </summary>
		public static bool TryApply(SensorMapping mapping, float rawCenter, float rawPull, out string message)
		{
			if (!TrySolve(rawPull - rawCenter, mapping.DeadZone, out var limit, out var scale, out message)) {
				return false;
			}
			mapping.RawCenter = rawCenter;
			mapping.Limit = limit;
			mapping.Scale = scale;
			return true;
		}

		/// <summary>
		/// Changes the dead zone and re-solves limit and scale for the same travel.
		/// </summary>
		/// <returns>False, leaving the mapping unchanged, if the travel would fall inside the new dead zone.</returns>
		public static bool TryChangeDeadZone(SensorMapping mapping, float deadZone, out string message)
		{
			var travel = GetTravel(mapping);
			if (!TrySolve(travel, deadZone, out var limit, out var scale, out message)) {
				message = "That dead zone would swallow the whole plunger travel.";
				return false;
			}
			mapping.DeadZone = math.clamp(deadZone, 0f, 0.999f);
			mapping.Limit = limit;
			mapping.Scale = scale;
			return true;
		}

		/// <summary>
		/// Flips the direction of a mapping, for devices that report pulling as negative.
		/// </summary>
		public static void Invert(SensorMapping mapping)
		{
			mapping.Scale = -mapping.Scale;
		}
	}

	/// <summary>
	/// Collects the samples of a two-step plunger calibration: at rest, then pulled
	/// all the way back. Call <see cref="Sample"/> about every few milliseconds
	/// for at least half a second per step, then <see cref="TryCompleteStep"/>.
	/// Failure leaves the mapping untouched.
	/// </summary>
	public sealed class PlungerSensorCalibration
	{
		public enum Step
		{
			Rest,
			Pull,
			Done
		}

		private const int MinSamples = 8;
		private const long MinDurationUsec = 500000;
		private const float MaxRestNoise = 0.04f;

		private readonly SensorMapping _mapping;
		private long _lastTimestamp;
		private long _firstTimestamp;
		private int _count;
		private double _sum;
		private float _min = float.PositiveInfinity;
		private float _max = float.NegativeInfinity;
		private float _rawCenter;
		private float _farthest;
		private bool _unavailable;

		/// <param name="mapping">The position mapping to calibrate. It is only changed when the calibration completes.</param>
		public PlungerSensorCalibration(SensorMapping mapping)
		{
			_mapping = mapping;
		}

		public Step CurrentStep { get; private set; } = Step.Rest;

		public SensorMapping Mapping => _mapping;

		public void Sample(IReadOnlyList<NativeInputDeviceInfo> devices)
		{
			if (CurrentStep == Step.Done || _mapping == null) {
				return;
			}
			if (!NudgeSensorSetup.TryFindAxis(devices, _mapping, out var axis)) {
				_unavailable = true;
				return;
			}
			Add(axis.RawValue, axis.TimestampUsec);
		}

		/// <summary>
		/// Adds one raw reading. Readings with a timestamp that didn't advance are ignored.
		/// </summary>
		public void Add(float rawValue, long timestampUsec)
		{
			if (CurrentStep == Step.Done || timestampUsec <= _lastTimestamp || !math.isfinite(rawValue)) {
				return;
			}
			_lastTimestamp = timestampUsec;
			if (_count == 0) {
				_firstTimestamp = timestampUsec;
			}
			_count++;
			_sum += rawValue;
			_min = math.min(_min, rawValue);
			_max = math.max(_max, rawValue);
			if (CurrentStep == Step.Pull && math.abs(rawValue - _rawCenter) > math.abs(_farthest - _rawCenter)) {
				_farthest = rawValue;
			}
		}

		/// <summary>
		/// Starts the current step over, e.g. after reconnecting the device.
		/// </summary>
		public void RestartStep()
		{
			if (CurrentStep == Step.Done) {
				return;
			}
			_unavailable = false;
			ResetSamples();
			if (CurrentStep == Step.Pull) {
				_farthest = _rawCenter;
			}
		}

		/// <summary>
		/// Finishes the current step. After the pull step the mapping is updated.
		/// </summary>
		/// <returns>False with a message if the step needs to be repeated; its samples are discarded.</returns>
		public bool TryCompleteStep(out string message)
		{
			if (_mapping == null || _unavailable) {
				message = "Calibration needs the plunger's device to be connected.";
				ResetSamples();
				return false;
			}
			if (_count < MinSamples || _lastTimestamp - _firstTimestamp < MinDurationUsec) {
				message = "Not enough fresh input samples. Check the device and try again.";
				ResetSamples();
				return false;
			}
			switch (CurrentStep) {
				case Step.Rest:
					if (_max - _min > MaxRestNoise) {
						message = "The plunger moved. Let go of it and try again.";
						ResetSamples();
						return false;
					}
					_rawCenter = (float)(_sum / _count);
					_farthest = _rawCenter;
					CurrentStep = Step.Pull;
					ResetSamples();
					message = "Now pull the plunger all the way back and hold it.";
					return true;

				case Step.Pull:
					if (!PlungerCalibrationMath.TryApply(_mapping, _rawCenter, _farthest, out message)) {
						ResetSamples();
						_farthest = _rawCenter;
						return false;
					}
					CurrentStep = Step.Done;
					message = "Plunger calibrated.";
					return true;

				default:
					message = "Plunger calibrated.";
					return true;
			}
		}

		private void ResetSamples()
		{
			_count = 0;
			_sum = 0;
			_min = float.PositiveInfinity;
			_max = float.NegativeInfinity;
			_firstTimestamp = 0;
		}
	}
}
