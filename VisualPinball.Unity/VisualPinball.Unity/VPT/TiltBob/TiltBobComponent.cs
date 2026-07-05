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
using UnityEngine;
using UnityEngine.Serialization;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game.Engines;
using VisualPinball.Unity.Simulation;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Table-owned tilt-bob switch device.
	/// </summary>
	/// <remarks>
	/// The component is the table author's routing point: map the game logic's
	/// tilt switch to this device if the table should have a tilt bob. The
	/// player's cabinet settings decide whether the signal comes from the physics
	/// plumb-bob simulation or from a real physical cabinet switch.
	/// </remarks>
	[DisallowMultipleComponent]
	[PackAs("TiltBob")]
	[AddComponentMenu("Pinball/Mechs/Tilt Bob")]
	public sealed class TiltBobComponent : MonoBehaviour, ISwitchDeviceComponent, IPackable
	{
		public const string SwitchItem = "tilt_bob_switch";
		public const float DefaultDamping = 1f;
		public const float MinDamping = 0f;
		public const float MaxDamping = 2f;
		public const float DefaultThresholdAngle = 2f;
		public const float MinThresholdAngle = 0.5f;
		public const float MaxThresholdAngle = 4f;

		private readonly Queue<bool> _queuedTiltStates = new();
		private readonly List<bool> _pendingSimulatedTiltStates = new(8);
		private readonly object _queuedTiltLock = new();

		[Tooltip("Mechanical plumb-bob damping scale. Higher values calm the bob faster after a nudge.")]
		[Range(MinDamping, MaxDamping)]
		[FormerlySerializedAs("Damping")]
		public float PlumbDamping = DefaultDamping;

		[Tooltip("Mechanical plumb-bob tilt threshold angle in degrees. Lower values make the table easier to tilt.")]
		[Range(MinThresholdAngle, MaxThresholdAngle)]
		[FormerlySerializedAs("ThresholdAngle")]
		public float PlumbThresholdAngle = DefaultThresholdAngle;

		[NonSerialized] private Player _player;
		[NonSerialized] private PhysicsEngine _physicsEngine;
		[NonSerialized] private TiltBobApi _api;
		[NonSerialized] private TiltBobMode _mode = TiltBobMode.Simulated;
		[NonSerialized] private bool _enabled = true;
		[NonSerialized] private bool _settingsApplied;

		public IEnumerable<GamelogicEngineSwitch> AvailableSwitches => new[] {
			new GamelogicEngineSwitch(SwitchItem) { Description = "Tilt Bob" }
		};

		public SwitchDefault SwitchDefault => SwitchDefault.NormallyOpen;

		IEnumerable<GamelogicEngineSwitch> IDeviceComponent<GamelogicEngineSwitch>.AvailableDeviceItems => AvailableSwitches;

		public TiltBobMode Mode => _mode;
		public bool UsesSimulatedPlumb => _enabled && _mode == TiltBobMode.Simulated;
		public bool UsesPhysicalTiltInput => _enabled && _mode == TiltBobMode.Physical;

		public static TiltBobComponent FindFor(PhysicsEngine physicsEngine)
		{
			if (!physicsEngine) {
				return null;
			}

			var table = physicsEngine.GetComponentInParent<TableComponent>();
			if (table) {
				return table.GetComponentInChildren<TiltBobComponent>(true);
			}

			return physicsEngine.GetComponentInParent<TiltBobComponent>(true)
			       ?? physicsEngine.GetComponentInChildren<TiltBobComponent>(true);
		}

		public static void ApplySettings(PhysicsEngine physicsEngine, CabinetPlumbSettings settings)
		{
			settings ??= new CabinetPlumbSettings();
			settings.Normalize();

			var tiltBob = FindFor(physicsEngine);
			if (tiltBob != null) {
				tiltBob.ApplySettings(settings);
				return;
			}

			// Without a table tilt-bob component there is no switch route, so keep
			// the physics plumb disabled even if the player prefers simulation.
			physicsEngine?.ConfigurePlumb(false, DefaultDamping, DefaultThresholdAngle);
		}

		public byte[] Pack() => TiltBobPackable.Pack(this);

		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files) => null;

		public void Unpack(byte[] bytes) => TiltBobPackable.Unpack(bytes, this);

		public void UnpackReferences(byte[] bytes, Transform root, PackagedRefs refs, PackagedFiles files) { }

		public void ApplySettings(CabinetPlumbSettings settings)
		{
			settings ??= new CabinetPlumbSettings();
			settings.Normalize();

			_enabled = settings.enabled;
			_mode = settings.mode;
			_settingsApplied = true;

			ConfigurePhysicsPlumb();
			ConfigureSimulationThreadRouting();
		}

		internal void QueueTiltStateFromSimulationThread(bool enabled)
		{
			lock (_queuedTiltLock) {
				_queuedTiltStates.Enqueue(enabled);
			}
		}

		private void Awake()
		{
			_player = GetComponentInParent<Player>();
			_physicsEngine = GetComponentInParent<PhysicsEngine>();
			if (_player != null && _physicsEngine != null) {
				_api = new TiltBobApi(gameObject, _player, _physicsEngine);
				_player.Register(_api, this);
				_player.CabinetInputActionChanged += OnCabinetInputActionChanged;
			}
		}

		private void Start()
		{
			if (!_settingsApplied) {
				ApplySettings(new CabinetPlumbSettings());
			}
		}

		private void Update()
		{
			FlushQueuedTiltStates();

			if (!UsesSimulatedPlumb || !_physicsEngine || _physicsEngine.UsesExternalTiming) {
				return;
			}

			_pendingSimulatedTiltStates.Clear();
			_physicsEngine.DrainPlumbTiltEvents(_pendingSimulatedTiltStates);
			foreach (var tilted in _pendingSimulatedTiltStates) {
				SetSwitch(tilted);
			}
		}

		private void OnDestroy()
		{
			_enabled = false;
			if (_player != null) {
				_player.CabinetInputActionChanged -= OnCabinetInputActionChanged;
			}
			_physicsEngine?.ConfigurePlumb(false, DefaultDamping, DefaultThresholdAngle);
			ConfigureSimulationThreadRouting();
		}

		private void OnValidate()
		{
			NormalizeSettings();
			if (Application.isPlaying && _settingsApplied) {
				ConfigurePhysicsPlumb();
			}
		}

		internal void NormalizeSettings()
		{
			PlumbDamping = Mathf.Clamp(PlumbDamping, MinDamping, MaxDamping);
			PlumbThresholdAngle = Mathf.Clamp(PlumbThresholdAngle, MinThresholdAngle, MaxThresholdAngle);
		}

		private void OnCabinetInputActionChanged(string actionName, bool isPressed)
		{
			if (UsesPhysicalTiltInput && actionName == InputConstants.ActionTilt) {
				SetSwitch(isPressed);
			}
		}

		private void FlushQueuedTiltStates()
		{
			while (true) {
				bool tilted;
				lock (_queuedTiltLock) {
					if (_queuedTiltStates.Count == 0) {
						return;
					}
					tilted = _queuedTiltStates.Dequeue();
				}
				SetSwitch(tilted);
			}
		}

		private void SetSwitch(bool enabled)
		{
			if (!_enabled) {
				return;
			}
			_api?.SetSwitch(enabled);
		}

		private void ConfigurePhysicsPlumb()
		{
			NormalizeSettings();
			_physicsEngine ??= GetComponentInParent<PhysicsEngine>();
			_physicsEngine?.ConfigurePlumb(UsesSimulatedPlumb, PlumbDamping, PlumbThresholdAngle);
		}

		private void ConfigureSimulationThreadRouting()
		{
			var simulationThread = _physicsEngine != null
				? _physicsEngine.GetComponent<SimulationThreadComponent>()
				  ?? _physicsEngine.GetComponentInParent<SimulationThreadComponent>()
				  ?? _physicsEngine.GetComponentInChildren<SimulationThreadComponent>()
				: null;
			simulationThread?.ConfigureTiltBobRouting(this);
		}
	}
}
