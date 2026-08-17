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
using VisualPinball.Engine.Game.Engines;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	[DisallowMultipleComponent]
	[PackAs("Actuator")]
	[AddComponentMenu("Pinball/Mechs/Actuator")]
	[HelpURL("https://docs.visualpinball.org/creators-guide/manual/mechanisms/actuators.html")]
	public class ActuatorComponent : MonoBehaviour, ICoilDeviceComponent, IAnimationValueProvider<float>, IPackable
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

			ActuatorApi = new ActuatorApi(gameObject);
			player.Register(ActuatorApi, this);
		}

		private void Update()
		{
			EnsureInitialized();
			var previousPosition = _motion.Position;
			var previousReachedSequence = _motion.ReachedSequence;
			var config = CreateConfig();
			_motion.Advance(Time.deltaTime, in config);
			PublishChanges(previousPosition, previousReachedSequence);
		}

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
			if (!Approximately(previousPosition, _motion.Position)) {
				OnAnimationValueChanged?.Invoke(_motion.Position);
			}
			if (_motion.ReachedSequence != previousReachedSequence) {
				ActuatorApi?.NotifyReached();
			}
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
