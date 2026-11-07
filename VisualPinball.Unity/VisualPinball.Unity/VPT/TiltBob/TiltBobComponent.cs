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
	/// physical cabinet switch always participates. The player can also enable
	/// the physics plumb-bob simulation.
	/// </remarks>
	[DisallowMultipleComponent]
	[PackAs("TiltBob")]
	[AddComponentMenu("Pinball/Mechs/Tilt Bob")]
	[HelpURL("https://docs.visualpinball.org/creators-guide/manual/mechanisms/tilt-bobs.html")]
	public sealed class TiltBobComponent : MonoBehaviour, ISwitchDeviceComponent, IPackable
	{
		public const string SwitchItem = "tilt_bob_switch";
		public const float DefaultDamping = PlumbState.DefaultDamping;
		public const float MinDamping = PlumbState.MinDamping;
		public const float MaxDamping = PlumbState.MaxDamping;
		public const float DefaultThresholdAngle = PlumbState.DefaultThresholdAngle;
		public const float MinThresholdAngle = PlumbState.MinThresholdAngle;
		public const float MaxThresholdAngle = PlumbState.MaxThresholdAngle;

		private readonly List<bool> _pendingSimulatedTiltStates = new(8);

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
		private (float Damping, float Threshold)? _playerTuning;
		private bool _physicalTiltPressed;
		private bool _simulatedTiltPressed;
		private int _plumbDispatchGeneration;

		public IEnumerable<GamelogicEngineSwitch> AvailableSwitches => new[] {
			new GamelogicEngineSwitch(SwitchItem) { Description = "Tilt Bob" }
		};

		public SwitchDefault SwitchDefault => SwitchDefault.NormallyOpen;

		IEnumerable<GamelogicEngineSwitch> IDeviceComponent<GamelogicEngineSwitch>.AvailableDeviceItems => AvailableSwitches;

		public TiltBobMode Mode => _mode;
		public bool UsesSimulatedPlumb => _enabled && _mode == TiltBobMode.Simulated;
		public bool UsesPhysicalTiltInput => _enabled;
		internal bool HasRegisteredSwitch => _api != null;

		internal static bool OwnsTiltInputDestination(SwitchMapping mapping, IReadOnlyList<SwitchMapping> mappings)
		{
			if (InputManager.GetCanonicalActionName(mapping.InputAction) != InputConstants.ActionTilt) return false;
			foreach (var candidate in mappings) {
				if (candidate.Id == mapping.Id && candidate.Source == SwitchSource.Playfield
					&& candidate.Device is TiltBobComponent bob && bob && bob.HasRegisteredSwitch
					&& candidate.DeviceItem == SwitchItem) return true;
			}
			return false;
		}

		public static TiltBobComponent FindFor(PhysicsEngine physicsEngine)
		{
			if (!physicsEngine) {
				return null;
			}

			var table = physicsEngine.GetComponentInParent<TableComponent>(true);
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
				tiltBob._physicsEngine = physicsEngine;
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

		internal CabinetPlumbSettings CaptureSettings() => new() {
			mode = _mode,
			overrideTuning = _playerTuning.HasValue,
			damping = _playerTuning?.Damping ?? PlumbDamping,
			thresholdDeg = _playerTuning?.Threshold ?? PlumbThresholdAngle
		};

		public void ApplySettings(CabinetPlumbSettings settings)
		{
			settings ??= new CabinetPlumbSettings();
			settings.Normalize();

			var wasSimulated = UsesSimulatedPlumb;
			_enabled = settings.enabled;
			_mode = settings.mode;
			_playerTuning = settings.overrideTuning ? (settings.damping, settings.thresholdDeg) : null;
			_settingsApplied = true;
			if (wasSimulated != UsesSimulatedPlumb) {
				_plumbDispatchGeneration++;
				_simulatedTiltPressed = false;
			}
			ConfigurePhysicsPlumb();
			ConfigureSimulationThreadRouting();
			UpdateSwitch();
		}

		internal Action<bool> CreateSimulatedTiltDispatcher()
		{
			if (!UsesSimulatedPlumb) return null;
			// An edge already queued by the simulation thread belongs to the
			// mode in effect when it was drained, even after re-enabling.
			var generation = _plumbDispatchGeneration;
			return tilted => {
				if (generation == _plumbDispatchGeneration) SetSimulatedTilt(tilted);
			};
		}

		private void Awake()
		{
			_player = GetComponentInParent<Player>(true);
			_physicsEngine ??= FindPhysicsEngine();
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
			if (!UsesSimulatedPlumb || !_physicsEngine || _physicsEngine.UsesExternalTiming) {
				return;
			}

			_pendingSimulatedTiltStates.Clear();
			_physicsEngine.DrainPlumbTiltEvents(_pendingSimulatedTiltStates);
			foreach (var tilted in _pendingSimulatedTiltStates) {
				SetSimulatedTilt(tilted);
			}
		}

		private void OnDestroy()
		{
			_enabled = false;
			_plumbDispatchGeneration++;
			if (_player != null) {
				_player.CabinetInputActionChanged -= OnCabinetInputActionChanged;
			}
			if (_physicsEngine) _physicsEngine.ConfigurePlumb(false, DefaultDamping, DefaultThresholdAngle);
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
			PlumbDamping = PlumbState.ClampDamping(PlumbDamping);
			PlumbThresholdAngle = PlumbState.ClampThresholdAngle(PlumbThresholdAngle);
		}

		private void OnCabinetInputActionChanged(string actionName, bool isPressed)
		{
			if (actionName == InputConstants.ActionTilt) {
				SetPhysicalTilt(isPressed);
			}
		}

		internal void SetPhysicalTilt(bool tilted)
		{
			if (!UsesPhysicalTiltInput) return;
			_physicalTiltPressed = tilted;
			UpdateSwitch();
		}

		private void SetSimulatedTilt(bool tilted)
		{
			if (!UsesSimulatedPlumb) return;
			_simulatedTiltPressed = tilted;
			UpdateSwitch();
		}

		private void UpdateSwitch()
		{
			var tilted = _physicalTiltPressed || _simulatedTiltPressed;
			if (_api != null && _api.IsSwitchEnabled != tilted) _api.SetSwitch(tilted);
		}

		private void ConfigurePhysicsPlumb()
		{
			NormalizeSettings();
			_physicsEngine ??= FindPhysicsEngine();
			_physicsEngine?.ConfigurePlumb(UsesSimulatedPlumb,
				_playerTuning?.Damping ?? PlumbDamping, _playerTuning?.Threshold ?? PlumbThresholdAngle);
		}

		private PhysicsEngine FindPhysicsEngine() =>
			GetComponentInParent<PhysicsEngine>(true)
			?? GetComponentInParent<TableComponent>(true)?.GetComponentInChildren<PhysicsEngine>(true)
			?? GetComponentInChildren<PhysicsEngine>(true);

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
