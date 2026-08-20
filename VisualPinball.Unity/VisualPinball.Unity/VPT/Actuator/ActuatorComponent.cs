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
using System.Linq;
using NLog;
using Newtonsoft.Json;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	[Serializable]
	public sealed class ActuatorPositionSwitch
	{
		private const float MinimumPulseInterval = 0.001f;

		[Tooltip("How actuator travel controls this switch.")]
		public ActuatorPositionSwitchType Type;

		[Tooltip("Name shown in VPE's switch mapping UI.")]
		public string Name = "Position Switch";

		[SerializeField, JsonProperty]
		private string _switchId;

		[Range(0f, 1f)]
		[Tooltip("Beginning of the inclusive normalized position range.")]
		public float PositionBeginning;

		[Range(0f, 1f)]
		[Tooltip("End of the inclusive normalized position range.")]
		public float PositionEnd = 0.01f;

		[Min(MinimumPulseInterval)]
		[Tooltip("Normalized travel between pulses.")]
		public float PulseInterval = 0.1f;

		[Min(1)]
		[Unit("ms")]
		[Tooltip("How long each generated pulse remains enabled.")]
		public int PulseDuration = 20;

		[JsonIgnore]
		public GamelogicEngineSwitch Switch => new(_switchId) {
			Description = Name,
		};

		[JsonIgnore]
		public string SwitchId => _switchId;

		[JsonIgnore]
		public bool EmitsPulses => Type != ActuatorPositionSwitchType.EnableBetween;

		public ActuatorPositionSwitch()
		{
		}

		public ActuatorPositionSwitch(ActuatorPositionSwitchType type, string name, string switchId, float positionBeginning, float positionEnd, float pulseInterval = 0.1f, int pulseDuration = 20)
		{
			Type = type;
			Name = name;
			_switchId = switchId;
			PositionBeginning = positionBeginning;
			PositionEnd = positionEnd;
			PulseInterval = pulseInterval;
			PulseDuration = pulseDuration;
			Normalize();
		}

		internal bool HasId => !string.IsNullOrEmpty(_switchId);
		internal void GenerateId() => _switchId = $"switch_{Guid.NewGuid().ToString()[..8]}";

		internal void Normalize()
		{
			PositionBeginning = math.saturate(PositionBeginning);
			PositionEnd = math.saturate(PositionEnd);
			PulseInterval = math.max(MinimumPulseInterval, PulseInterval);
			PulseDuration = math.max(1, PulseDuration);
		}

		internal bool Contains(float position)
		{
			var normalizedPosition = math.saturate(position);
			var beginning = math.min(PositionBeginning, PositionEnd);
			var end = math.max(PositionBeginning, PositionEnd);
			return normalizedPosition >= beginning && normalizedPosition <= end;
		}

		internal int CountPulses(float previousPosition, float position)
		{
			if (!EmitsPulses) {
				return 0;
			}

			var previous = math.saturate(previousPosition);
			var current = math.saturate(position);
			var beginning = Type == ActuatorPositionSwitchType.AlwaysPulse ? 0f : math.min(PositionBeginning, PositionEnd);
			var end = Type == ActuatorPositionSwitchType.AlwaysPulse ? 1f : math.max(PositionBeginning, PositionEnd);
			var pulseInterval = (double)math.max(MinimumPulseInterval, PulseInterval);

			if (math.abs(current - previous) <= 0.000001f) {
				return 0;
			}

			const double epsilon = 0.000001;
			var rangeBeginning = (double)beginning;
			var lastRangeMark = System.Math.Max(0, (int)System.Math.Floor(((double)end - rangeBeginning + epsilon) / pulseInterval));
			int firstCrossedMark;
			int lastCrossedMark;
			if (current > previous) {
				firstCrossedMark = (int)System.Math.Floor(((double)previous + epsilon - rangeBeginning) / pulseInterval) + 1;
				lastCrossedMark = (int)System.Math.Floor(((double)current + epsilon - rangeBeginning) / pulseInterval);
			} else {
				firstCrossedMark = (int)System.Math.Ceiling(((double)current - epsilon - rangeBeginning) / pulseInterval);
				lastCrossedMark = (int)System.Math.Ceiling(((double)previous - epsilon - rangeBeginning) / pulseInterval) - 1;
			}

			firstCrossedMark = System.Math.Max(0, firstCrossedMark);
			lastCrossedMark = System.Math.Min(lastRangeMark, lastCrossedMark);
			return System.Math.Max(0, lastCrossedMark - firstCrossedMark + 1);
		}
	}

	public enum ActuatorPositionSwitchType
	{
		EnableBetween = 0,
		AlwaysPulse = 1,
		PulseBetween = 2,
	}

	[DisallowMultipleComponent]
	[PackAs("Actuator")]
	[AddComponentMenu("Pinball/Mechs/Actuator")]
	[HelpURL("https://docs.visualpinball.org/creators-guide/manual/mechanisms/actuators.html")]
	public class ActuatorComponent : MonoBehaviour, ICoilDeviceComponent, ISwitchDeviceComponent, IAnimationValueProvider<float>, ISerializationCallbackReceiver, IPackable
	{
		public const string ActuatorCoilItem = "actuator_coil";

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		[Tooltip("How the coil signal controls the actuator position.")]
		public ActuatorCoilMode CoilMode = ActuatorCoilMode.FollowCoil;

		[Range(0f, 1f)]
		[Tooltip("Normalized actuator pose applied before the first physics frame. The authored transform is position 0.")]
		public float InitialPosition;

		[Min(0f)]
		[Unit("s")]
		[Tooltip("Full-stroke travel time from position 0 to 1.")]
		public float ActivationDuration = 0.3f;

		[Min(0f)]
		[Unit("s")]
		[Tooltip("Full-stroke travel time from position 1 to 0.")]
		public float ReleaseDuration = 0.3f;

		[Tooltip("Easing applied while travelling toward position 1.")]
		public AnimationCurve ActivationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

		[Tooltip("Easing applied while travelling toward position 0.")]
		public AnimationCurve ReleaseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

		[Min(0f)]
		[Unit("s")]
		[Tooltip("How long a binary coil must remain off before it is released and another pulse can be recognized.")]
		public float ReleaseDelay = 0.05f;

		[Range(0f, 0.999f)]
		[Tooltip("Normalized duty cycle above which a binary coil is active. Keep this small because plain PinMAME outputs can be 1/255 after modulation is enabled.")]
		public float ActivationThreshold = 0.001f;

		[Min(0f)]
		[Unit("s")]
		[Tooltip("Time spent at position 1 before returning in One Shot mode.")]
		public float OneShotHoldDuration = 0.5f;

		[Tooltip("Switches controlled by the actuator's normalized position.")]
		public ActuatorPositionSwitch[] Switches = Array.Empty<ActuatorPositionSwitch>();

		public ActuatorApi ActuatorApi { get; private set; }
		public float Position => _initialized ? _motion.Position : math.saturate(InitialPosition);
		public float TargetPosition => _initialized ? _motion.TargetPosition : math.saturate(InitialPosition);
		public bool IsMoving => _initialized && _motion.IsMoving;
		public float AnimationValue => Position;

		public event Action<float> OnAnimationValueChanged;

		private readonly ActuatorMotionState _motion = new();
		private bool _initialized;

		public IEnumerable<GamelogicEngineCoil> AvailableCoils => new[] {
			new GamelogicEngineCoil(ActuatorCoilItem) {
				Description = "Actuator"
			}
		};

		IApiCoil ICoilDeviceComponent.CoilDevice(string deviceId) => ((IApiCoilDevice)ActuatorApi).Coil(deviceId);
		IEnumerable<GamelogicEngineCoil> IDeviceComponent<GamelogicEngineCoil>.AvailableDeviceItems => AvailableCoils;
		IEnumerable<IGamelogicEngineDeviceItem> IWireableComponent.AvailableWireDestinations => AvailableCoils;
		IEnumerable<IGamelogicEngineDeviceItem> IDeviceComponent<IGamelogicEngineDeviceItem>.AvailableDeviceItems => AvailableCoils;

		public IEnumerable<GamelogicEngineSwitch> AvailableSwitches => (Switches ?? Array.Empty<ActuatorPositionSwitch>())
			.Where(positionSwitch => positionSwitch != null && positionSwitch.HasId)
			.Select(positionSwitch => positionSwitch.Switch);

		public SwitchDefault SwitchDefault => SwitchDefault.NormallyOpen;
		IEnumerable<GamelogicEngineSwitch> IDeviceComponent<GamelogicEngineSwitch>.AvailableDeviceItems => AvailableSwitches;

		public byte[] Pack() => ActuatorPackable.Pack(this);
		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files) => Array.Empty<byte>();
		public void Unpack(byte[] bytes) => ActuatorPackable.Unpack(bytes, this);
		public void UnpackReferences(byte[] bytes, Transform root, PackagedRefs refs, PackagedFiles files) { }

		private void Awake()
		{
			EnsureInitialized();

			var player = GetComponentInParent<Player>();
			if (player == null) {
				Logger.Error($"Cannot find player for actuator {name}.");
				return;
			}

			var physicsEngine = GetComponentInParent<PhysicsEngine>();
			ActuatorApi = new ActuatorApi(gameObject, player, physicsEngine);
			ActuatorApi.UpdateSwitches(Position, Position, false, true, true);
			player.Register(ActuatorApi, this);
		}

		private void Update()
		{
			EnsureInitialized();
			ActuatorApi?.AdvancePulses(Time.deltaTime);
			var previousPosition = _motion.Position;
			var previousReachedSequence = _motion.ReachedSequence;
			var config = CreateConfig();
			_motion.Advance(Time.deltaTime, in config);
			PublishChanges(previousPosition, previousReachedSequence);
		}

		private void OnDisable() => ActuatorApi?.CancelPulses();

		private void OnValidate()
		{
			InitialPosition = math.saturate(InitialPosition);
			ActivationDuration = math.max(0f, ActivationDuration);
			ReleaseDuration = math.max(0f, ReleaseDuration);
			ReleaseDelay = math.max(0f, ReleaseDelay);
			ActivationThreshold = math.clamp(ActivationThreshold, 0f, 0.999f);
			OneShotHoldDuration = math.max(0f, OneShotHoldDuration);
			ActivationCurve = EnsureCurve(ActivationCurve, true);
			ReleaseCurve = EnsureCurve(ReleaseCurve, true);
			Switches ??= Array.Empty<ActuatorPositionSwitch>();
			foreach (var positionSwitch in Switches) {
				positionSwitch?.Normalize();
			}
		}

		internal void ApplyCoilValue(float value)
		{
			EnsureInitialized();
			var previousPosition = _motion.Position;
			var previousReachedSequence = _motion.ReachedSequence;
			var config = CreateConfig();
			_motion.SetInput(value, in config);
			PublishChanges(previousPosition, previousReachedSequence);
		}

		internal void SetActive(bool active)
		{
			EnsureInitialized();
			var previousPosition = _motion.Position;
			var previousReachedSequence = _motion.ReachedSequence;
			var config = CreateConfig();
			_motion.SetActive(active, in config);
			PublishChanges(previousPosition, previousReachedSequence);
		}

		internal void Toggle()
		{
			EnsureInitialized();
			var previousPosition = _motion.Position;
			var previousReachedSequence = _motion.ReachedSequence;
			var config = CreateConfig();
			_motion.Toggle(in config);
			PublishChanges(previousPosition, previousReachedSequence);
		}

		internal void SnapTo(float position)
		{
			EnsureInitialized();
			var previousPosition = _motion.Position;
			_motion.SnapTo(position);
			if (!Approximately(previousPosition, _motion.Position)) {
				OnAnimationValueChanged?.Invoke(_motion.Position);
			}
			ActuatorApi?.UpdateSwitches(previousPosition, _motion.Position, false, true, true);
		}

		void IAnimationValueEmitter<float>.UpdateAnimationValue(float value) => SnapTo(value);

		private void EnsureInitialized()
		{
			if (_initialized) {
				return;
			}
			_motion.Initialize(InitialPosition);
			_initialized = true;
		}

		private ActuatorMotionConfig CreateConfig()
		{
			return new ActuatorMotionConfig {
				CoilMode = CoilMode,
				ActivationDuration = math.max(0f, ActivationDuration),
				ReleaseDuration = math.max(0f, ReleaseDuration),
				ActivationCurve = ActivationCurve,
				ReleaseCurve = ReleaseCurve,
				ReleaseDelay = math.max(0f, ReleaseDelay),
				ActivationThreshold = math.clamp(ActivationThreshold, 0f, 0.999f),
				OneShotHoldDuration = math.max(0f, OneShotHoldDuration),
			};
		}

		private void PublishChanges(float previousPosition, int previousReachedSequence)
		{
			var positionChanged = !Approximately(previousPosition, _motion.Position);
			if (positionChanged) {
				OnAnimationValueChanged?.Invoke(_motion.Position);
				ActuatorApi?.UpdateSwitches(previousPosition, _motion.Position, true);
			}
			if (_motion.ReachedSequence != previousReachedSequence) {
				ActuatorApi?.NotifyReached();
			}
		}

		public void OnBeforeSerialize()
		{
			#if UNITY_EDITOR
			Switches ??= Array.Empty<ActuatorPositionSwitch>();
			var switchIds = new HashSet<string>();
			var switchNames = new HashSet<string>();
			foreach (var positionSwitch in Switches) {
				if (positionSwitch == null) {
					continue;
				}
				positionSwitch.Normalize();
				if (string.IsNullOrWhiteSpace(positionSwitch.Name) || switchNames.Contains(positionSwitch.Name)) {
					const string defaultName = "Position Switch";
					var baseName = string.IsNullOrWhiteSpace(positionSwitch.Name) ? defaultName : positionSwitch.Name;
					var suffix = 1;
					var uniqueName = baseName;
					while (switchNames.Contains(uniqueName)) {
						uniqueName = $"{baseName} {++suffix}";
					}
					positionSwitch.Name = uniqueName;
				}
				switchNames.Add(positionSwitch.Name);
				if (!positionSwitch.HasId || switchIds.Contains(positionSwitch.SwitchId)) {
					positionSwitch.GenerateId();
				}
				switchIds.Add(positionSwitch.SwitchId);
			}
			#endif
		}

		public void OnAfterDeserialize()
		{
			Switches ??= Array.Empty<ActuatorPositionSwitch>();
		}

		private static bool Approximately(float a, float b) => math.abs(a - b) <= 0.000001f;

		internal static AnimationCurve EnsureCurve(AnimationCurve curve, bool easeInOut)
		{
			if (curve != null && curve.length >= 2) {
				return curve;
			}
			return easeInOut ? AnimationCurve.EaseInOut(0f, 0f, 1f, 1f) : AnimationCurve.Linear(0f, 0f, 1f, 1f);
		}
	}
}
