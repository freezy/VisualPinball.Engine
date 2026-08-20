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
using System.Collections.Generic;
using NLog;
using Unity.Mathematics;
using UnityEngine;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	public class ActuatorApi : IApi, IApiCoilDevice, IApiSwitchDevice, IApiWireDeviceDest, IApiCoil
	{
		private const int MaxPendingPulses = 1024;
		private const float PulseGapDuration = 0.001f;

		private sealed class PositionSwitchRuntime
		{
			internal readonly ActuatorPositionSwitch Config;
			internal readonly DeviceSwitch Switch;
			internal int PendingPulses;
			internal bool PulseActive;
			internal bool WaitingForGap;
			internal float StateTimeRemaining;
			internal bool WarnedPulseOverflow;

			internal PositionSwitchRuntime(ActuatorPositionSwitch config, DeviceSwitch positionSwitch)
			{
				Config = config;
				Switch = positionSwitch;
			}
		}

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private readonly ActuatorComponent _component;
		private readonly Dictionary<string, PositionSwitchRuntime> _switches = new();
		private readonly PositionSwitchRuntime[] _switchRuntimes;
		private bool _warnedBooleanFollowValue;

		public event EventHandler Init;
		public event EventHandler Reached;
		public event EventHandler<NoIdCoilEventArgs> CoilStatusChanged;

		internal ActuatorApi(GameObject go, Player player = null, PhysicsEngine physicsEngine = null)
		{
			_component = go.GetComponent<ActuatorComponent>();
			var switchRuntimes = new List<PositionSwitchRuntime>();
			foreach (var positionSwitch in _component.Switches ?? Array.Empty<ActuatorPositionSwitch>()) {
				if (positionSwitch == null || !positionSwitch.HasId) {
					Logger.Warn($"Ignoring actuator position switch without an ID on '{_component.name}'.");
					continue;
				}
				if (_switches.ContainsKey(positionSwitch.SwitchId)) {
					Logger.Warn($"Ignoring duplicate actuator position switch ID '{positionSwitch.SwitchId}' on '{_component.name}'.");
					continue;
				}
				var deviceSwitch = new DeviceSwitch(positionSwitch.SwitchId, false, SwitchDefault.NormallyOpen, player, physicsEngine);
				var runtime = new PositionSwitchRuntime(positionSwitch, deviceSwitch);
				_switches[positionSwitch.SwitchId] = runtime;
				switchRuntimes.Add(runtime);
			}
			_switchRuntimes = switchRuntimes.ToArray();
		}

		public float Position => _component.Position;
		public float TargetPosition => _component.TargetPosition;
		public bool IsMoving => _component.IsMoving;

		/// <summary>
		/// Gets or commands the active endpoint. A script command takes precedence until the next
		/// qualified coil edge; it does not rewrite the sampled physical coil state.
		/// </summary>
		public bool IsActive {
			get => TargetPosition >= 0.5f;
			set => _component.SetActive(value);
		}

		/// <summary>
		/// Toggles the commanded endpoint. A script command takes precedence until the next
		/// qualified coil edge; it does not rewrite the sampled physical coil state.
		/// </summary>
		public void Toggle() => _component.Toggle();

		/// <summary>
		/// Immediately restores or previews a normalized pose. A script command takes precedence
		/// until the next qualified coil edge; it does not rewrite the sampled physical coil state.
		/// </summary>
		public void SnapTo(float position) => _component.SnapTo(position);

		void IApi.OnInit(BallManager ballManager) => Init?.Invoke(this, EventArgs.Empty);
		void IApi.OnDestroy() => CancelPulses();

		IApiCoil IApiCoilDevice.Coil(string deviceItem) => Coil(deviceItem);
		IApiSwitch IApiSwitchDevice.Switch(string deviceItem) => Switch(deviceItem);
		public IApiWireDest Wire(string deviceItem) => Coil(deviceItem);

		public IApiSwitch Switch(string deviceItem)
		{
			if (_switches.TryGetValue(deviceItem, out var positionSwitch)) {
				return positionSwitch.Switch;
			}
			return null;
		}

		private IApiCoil Coil(string deviceItem)
		{
			return deviceItem switch {
				ActuatorComponent.ActuatorCoilItem => this,
				_ => throw new ArgumentException($"Unknown actuator coil \"{deviceItem}\". Valid name is \"{ActuatorComponent.ActuatorCoilItem}\".")
			};
		}

		void IApiCoil.OnCoil(bool enabled) => ApplyCoilValue(enabled ? 1f : 0f);

		void IApiCoil.OnCoil(float value) => ApplyCoilValue(math.saturate(value));

		void IApiWireDest.OnChange(bool enabled)
		{
			if (_component.CoilMode == ActuatorCoilMode.FollowValue && !_warnedBooleanFollowValue) {
				_warnedBooleanFollowValue = true;
				Debug.LogWarning($"Actuator '{_component.name}' is configured to Follow Value but is receiving boolean wire input. Use a plain coil mapping to preserve proportional values.", _component);
			}
			ApplyCoilValue(enabled ? 1f : 0f);
		}

		internal void NotifyReached() => Reached?.Invoke(this, EventArgs.Empty);

		internal void UpdateSwitches(float previousPosition, float position, bool emitPulses, bool forceMaintained = false, bool resetPulses = false)
		{
			if (resetPulses) {
				ResetPulseQueues();
			}

			foreach (var runtime in _switchRuntimes) {
				if (!runtime.Config.EmitsPulses) {
					if (runtime.PulseActive || runtime.WaitingForGap || runtime.PendingPulses > 0) {
						ResetPulseQueue(runtime);
					}
					var enabled = runtime.Config.Contains(position);
					if (forceMaintained || runtime.Switch.IsSwitchEnabled != enabled) {
						runtime.Switch.SetSwitch(enabled);
					}
					continue;
				}

				if (!emitPulses) {
					continue;
				}
				var pulseCount = runtime.Config.CountPulses(previousPosition, position);
				if (pulseCount <= 0) {
					continue;
				}

				var availableQueueSlots = MaxPendingPulses - runtime.PendingPulses;
				var queuedPulses = math.min(availableQueueSlots, pulseCount);
				runtime.PendingPulses += queuedPulses;
				if (queuedPulses < pulseCount && !runtime.WarnedPulseOverflow) {
					runtime.WarnedPulseOverflow = true;
					Logger.Warn($"Actuator '{_component.name}' position switch '{runtime.Config.Name}' exceeded its {MaxPendingPulses}-pulse backlog; dropping additional pulses.");
				}
				TryStartPulse(runtime);
			}
		}

		internal void AdvancePulses(float deltaTime)
		{
			var elapsed = math.max(0f, deltaTime);
			foreach (var runtime in _switchRuntimes) {
				if (runtime.Config.EmitsPulses) {
					AdvancePulse(runtime, elapsed);
				}
			}
		}

		internal void CancelPulses() => ResetPulseQueues();

		private static void AdvancePulse(PositionSwitchRuntime runtime, float elapsed)
		{
			if (runtime.PulseActive) {
				runtime.StateTimeRemaining -= elapsed;
				if (runtime.StateTimeRemaining > 0f) {
					return;
				}
				runtime.Switch.SetSwitch(false);
				runtime.PulseActive = false;
				runtime.WaitingForGap = true;
				runtime.StateTimeRemaining = PulseGapDuration;
				return;
			}

			if (runtime.WaitingForGap) {
				runtime.StateTimeRemaining -= elapsed;
				if (runtime.StateTimeRemaining > 0f) {
					return;
				}
				runtime.WaitingForGap = false;
				runtime.StateTimeRemaining = 0f;
			}

			TryStartPulse(runtime);
		}

		private static bool TryStartPulse(PositionSwitchRuntime runtime)
		{
			if (runtime.PulseActive || runtime.WaitingForGap || runtime.PendingPulses <= 0) {
				return false;
			}
			runtime.PendingPulses--;
			runtime.PulseActive = true;
			runtime.StateTimeRemaining = math.max(1, runtime.Config.PulseDuration) / 1000f;
			runtime.Switch.SetSwitch(true);
			return true;
		}

		private void ResetPulseQueues()
		{
			foreach (var runtime in _switchRuntimes) {
				ResetPulseQueue(runtime);
			}
		}

		private static void ResetPulseQueue(PositionSwitchRuntime runtime)
		{
			if (runtime.PulseActive) {
				runtime.Switch.SetSwitch(false);
			}
			runtime.PendingPulses = 0;
			runtime.PulseActive = false;
			runtime.WaitingForGap = false;
			runtime.StateTimeRemaining = 0f;
			runtime.WarnedPulseOverflow = false;
		}

		private void ApplyCoilValue(float value)
		{
			_component.ApplyCoilValue(value);
			CoilStatusChanged?.Invoke(this, new NoIdCoilEventArgs(value > 0f));
		}
	}
}
