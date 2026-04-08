// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
// https://github.com/freezy/VisualPinball.Engine
//
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Runtime.InteropServices;
using System.Diagnostics;
using NLog;
using VisualPinball.Unity.Simulation;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	internal static class InputLatencyTracker
	{
		private enum TimestampClock
		{
			None,
			Native,
			Stopwatch,
		}

		private readonly struct PendingInput
		{
			public readonly long TimestampUsec;
			public readonly TimestampClock Clock;

			public PendingInput(long timestampUsec, TimestampClock clock)
			{
				TimestampUsec = timestampUsec;
				Clock = clock;
			}
		}

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private const string LogPrefix = "[VPE]";
		private static readonly object LockObj = new object();

		private static PendingInput _leftPending;
		private static PendingInput _rightPending;

		private static double _flipperLatencySumMs;
		private static int _flipperLatencyCount;
		private static float _lastFlipperLatencyMs;

		private static bool _nativeTimestampAvailable = true;
		private static int _inputPressLogCount;
		private static int _visualDetectLogCount;
		private static bool _nativeTimestampFailureLogged;

		public static void Reset()
		{
			lock (LockObj) {
				_leftPending = default;
				_rightPending = default;
				_flipperLatencySumMs = 0;
				_flipperLatencyCount = 0;
				_lastFlipperLatencyMs = 0;
				_inputPressLogCount = 0;
				_visualDetectLogCount = 0;
				_nativeTimestampFailureLogged = false;
			}
			Logger.Info($"{LogPrefix} [InputLatency] Reset tracker");
		}

		public static void RecordInputPolled(NativeInputApi.InputAction action, bool isPressed, long timestampUsec)
		{
			if (!isPressed) {
				return;
			}

			if (timestampUsec <= 0) {
				timestampUsec = GetStopwatchTimestampUsec();
			}

			lock (LockObj) {
				switch (action) {
					case NativeInputApi.InputAction.LeftFlipper:
					case NativeInputApi.InputAction.UpperLeftFlipper:
						_leftPending = new PendingInput(timestampUsec, TimestampClock.Native);
						break;

					case NativeInputApi.InputAction.RightFlipper:
					case NativeInputApi.InputAction.UpperRightFlipper:
						_rightPending = new PendingInput(timestampUsec, TimestampClock.Native);
						break;
				}

				if (_inputPressLogCount < 10) {
					_inputPressLogCount++;
					Logger.Info($"{LogPrefix} [InputLatency] Polled press action={action}, ts={timestampUsec}us");
				}
			}
		}

		public static void RecordSwitchInputDispatched(string switchId, bool isClosed)
		{
			if (!isClosed || string.IsNullOrEmpty(switchId)) {
				return;
			}

			var timestampUsec = GetStopwatchTimestampUsec();
			var handled = false;

			lock (LockObj) {
				switch (switchId) {
					case "s_flipper_lower_left":
					case "s_flipper_upper_left":
						_leftPending = new PendingInput(timestampUsec, TimestampClock.Stopwatch);
						handled = true;
						break;

					case "s_flipper_lower_right":
					case "s_flipper_upper_right":
						_rightPending = new PendingInput(timestampUsec, TimestampClock.Stopwatch);
						handled = true;
						break;
				}

				if (handled && _inputPressLogCount < 10) {
					_inputPressLogCount++;
					Logger.Info($"{LogPrefix} [InputLatency] Switch press dispatch switchId={switchId}, ts={timestampUsec}us");
				}
			}
		}

		public static void RecordFlipperVisualMovement(bool isLeftFlipper)
		{
			PendingInput pending;
			lock (LockObj) {
				pending = isLeftFlipper ? _leftPending : _rightPending;
			}

			if (pending.TimestampUsec <= 0 || pending.Clock == TimestampClock.None) {
				return;
			}

			if (!TryGetVisualTimestampUsec(pending.Clock, out var visualTimestampUsec)) {
				if (!_nativeTimestampFailureLogged) {
					_nativeTimestampFailureLogged = true;
					Logger.Warn($"{LogPrefix} [InputLatency] Visual timestamp unavailable while movement detected (clock={pending.Clock})");
				}
				return;
			}

			var latencyUsec = visualTimestampUsec - pending.TimestampUsec;
			if (latencyUsec < 0) {
				Logger.Warn($"{LogPrefix} [InputLatency] Negative latency detected (clock={pending.Clock}, visual={visualTimestampUsec}us, input={pending.TimestampUsec}us)");
				latencyUsec = 0;
			}

			var latencyMs = latencyUsec / 1000.0;
			lock (LockObj) {
				if (isLeftFlipper) {
					_leftPending = default;
				} else {
					_rightPending = default;
				}

				_flipperLatencySumMs += latencyMs;
				_flipperLatencyCount++;
				_lastFlipperLatencyMs = (float)latencyMs;

				if (_visualDetectLogCount < 20) {
					_visualDetectLogCount++;
					Logger.Info($"{LogPrefix} [InputLatency] Visual flipper movement ({(isLeftFlipper ? "L" : "R")}) latency={latencyMs:0.000}ms (clock={pending.Clock}, input={pending.TimestampUsec}us, visual={visualTimestampUsec}us)");
				}
			}
		}

		public static float SampleFlipperLatencyMs()
		{
			lock (LockObj) {
				if (_flipperLatencyCount > 0) {
					_lastFlipperLatencyMs = (float)(_flipperLatencySumMs / _flipperLatencyCount);
					Logger.Info($"{LogPrefix} [InputLatency] Sample window avg={_lastFlipperLatencyMs:0.000}ms from {_flipperLatencyCount} sample(s)");
					_flipperLatencySumMs = 0;
					_flipperLatencyCount = 0;
				}

				return _lastFlipperLatencyMs;
			}
		}

		private static bool TryGetVisualTimestampUsec(TimestampClock clock, out long timestampUsec)
		{
			timestampUsec = 0;
			switch (clock) {
				case TimestampClock.Native:
					return TryGetNativeTimestampUsec(out timestampUsec);

				case TimestampClock.Stopwatch:
					timestampUsec = GetStopwatchTimestampUsec();
					return timestampUsec > 0;

				default:
					return false;
			}
		}

		private static bool TryGetNativeTimestampUsec(out long timestampUsec)
		{
			timestampUsec = 0;
			if (!_nativeTimestampAvailable) {
				return false;
			}

			try {
				timestampUsec = NativeInputApi.VpeGetTimestampUsec();
				return timestampUsec > 0;
			}
			catch (DllNotFoundException) {
				_nativeTimestampAvailable = false;
			}
			catch (EntryPointNotFoundException) {
				_nativeTimestampAvailable = false;
			}
			catch (TypeLoadException) {
				_nativeTimestampAvailable = false;
			}

			return false;
		}

		private static long GetStopwatchTimestampUsec()
		{
			var ticks = Stopwatch.GetTimestamp();
			return (ticks * 1_000_000L) / Stopwatch.Frequency;
		}
	}
}
