// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;

namespace VisualPinball.Unity
{
	[Serializable]
	public struct SlingshotSwitchZone
	{
		[Range(0f, 1f)] public float Position01;
		[Min(0f)] public float CloseDeflection;
		[Min(0f)] public float OpenDeflection;
		[Min(0f)] public float MinimumClosedTimeMs;
	}

	[PackAs("RubberSlingshot")]
	[AddComponentMenu("Pinball/Game Item/Rubber Slingshot")]
	[DisallowMultipleComponent]
	public sealed class RubberSlingshotComponent : MonoBehaviour,
		ISwitchDeviceComponent, ICoilDeviceComponent, IPackable
	{
		public const string SwitchItem = "slingshot_switch";
		public const string CoilItem = "slingshot_coil";

		#region Defaults

		internal const bool DefaultSelfWired = true;
		internal const float DefaultWireDelayMs = 8f;
		internal const float DefaultSelfWirePulseMs = 20f;
		internal const float DefaultKickSpeed = 30f;
		internal const float DefaultArmHalfWidth01 = 0.25f;
		internal const float DefaultKickWindowMs = 30f;
		internal const float DefaultMinimumSwitchPulseMs = 30f;

		#endregion

		[SerializeField] private RubberSpanBinding _span;
		[SerializeField] private SlingshotSwitchZone[] _switchZones = {
			new() { Position01 = 0.3f, CloseDeflection = 4f, OpenDeflection = 2f, MinimumClosedTimeMs = 8f },
			new() { Position01 = 0.7f, CloseDeflection = 4f, OpenDeflection = 2f, MinimumClosedTimeMs = 8f },
		};
		[SerializeField, Range(0f, 1f)] private float _armContactPosition01 = 0.5f;
		[SerializeField, Min(0f)] private float _armRestClearance = 2f;
		[SerializeField, Min(0f)] private float _armTipTravel = 18f;
		[SerializeField] private SlingshotActuatorAsset _actuator;

		[Tooltip("When on, a blade-switch closure energizes the coil by itself, the way a real slingshot's switch wires straight to it. Turn it off to drive the coil only from the gamelogic engine or a player wire.")]
		[SerializeField] private bool _selfWired = DefaultSelfWired;

		[Tooltip("Delay between the blade switch closing and the self-wired coil energizing, in milliseconds of simulation time.")]
		[SerializeField, Min(0f)] private float _wireDelayMs = DefaultWireDelayMs;

		[Tooltip("How long the self-wired coil stays energized, in milliseconds of simulation time.")]
		[SerializeField, Min(0f)] private float _selfWirePulseMs = DefaultSelfWirePulseMs;

		[Tooltip("Velocity the arm adds to a ball at its contact point, in VPX units per step.")]
		[SerializeField, Min(0f)] private float _kickSpeed = DefaultKickSpeed;

		[Tooltip("Half width of the arm's kick window along the span, as a fraction of the span length.")]
		[SerializeField, Range(0.01f, 0.5f)] private float _armHalfWidth01 = DefaultArmHalfWidth01;

		[Tooltip("How long after the closure the ball that closed the switch still gets the kick even when it has left the arm's reach, in milliseconds of simulation time.")]
		[SerializeField, Min(0f)] private float _kickWindowMs = DefaultKickWindowMs;

		[Tooltip("Shortest switch pulse delivered to the gamelogic engine, in milliseconds of frame time. Player wires always get the raw edges.")]
		[SerializeField, Min(0f)] private float _minimumSwitchPulseMs = DefaultMinimumSwitchPulseMs;

		[SerializeField] private GameObject _coilArmVisual;
		[SerializeField] private Axis _visualRotationAxis;
		[SerializeField] private float _visualRestAngle;
		[SerializeField] private float _visualFiredAngle = 20f;

		/// <summary>
		/// The rubber this slingshot belongs to: the one on the same game object. The
		/// slingshot is one of the rubber's components, so nothing has to be wired.
		/// </summary>
		public RubberComponent Rubber {
			get {
				if (!_rubber) {
					_rubber = GetComponent<RubberComponent>();
				}
				return _rubber;
			}
		}
		[NonSerialized] private RubberComponent _rubber;
		public RubberSpanBinding Span { get => _span; set => _span = value; }
		public SlingshotSwitchZone[] SwitchZones {
			get => _switchZones ?? Array.Empty<SlingshotSwitchZone>();
			set => _switchZones = value ?? Array.Empty<SlingshotSwitchZone>();
		}
		public float ArmContactPosition01 { get => _armContactPosition01; set => _armContactPosition01 = value; }
		public float ArmRestClearance { get => _armRestClearance; set => _armRestClearance = value; }
		public float ArmTipTravel { get => _armTipTravel; set => _armTipTravel = value; }
		public SlingshotActuatorAsset Actuator { get => _actuator; set => _actuator = value; }
		public GameObject CoilArmVisual { get => _coilArmVisual; set => _coilArmVisual = value; }
		public Axis VisualRotationAxis { get => _visualRotationAxis; set => _visualRotationAxis = value; }
		public float VisualRestAngle { get => _visualRestAngle; set => _visualRestAngle = value; }
		public float VisualFiredAngle { get => _visualFiredAngle; set => _visualFiredAngle = value; }

		#region Runtime fields

		/// <summary>Whether a blade-switch closure energizes the coil by itself.</summary>
		public bool SelfWired { get => _selfWired; set => _selfWired = value; }

		/// <summary>Wire delay in milliseconds, sanitized.</summary>
		public float WireDelayMs {
			get => Sanitize(_wireDelayMs, DefaultWireDelayMs);
			set => _wireDelayMs = value;
		}

		/// <summary>Self-wire pulse length in milliseconds, sanitized.</summary>
		public float SelfWirePulseMs {
			get => Sanitize(_selfWirePulseMs, DefaultSelfWirePulseMs);
			set => _selfWirePulseMs = value;
		}

		/// <summary>Kick speed in VPX units per step, sanitized.</summary>
		public float KickSpeed {
			get => Sanitize(_kickSpeed, DefaultKickSpeed);
			set => _kickSpeed = value;
		}

		/// <summary>The same kick speed in meters per second, for the inspector.</summary>
		public float KickSpeedMetersPerSecond => KickSpeed / SpeedUnitsPerMeterPerSecond;

		/// <summary>Half width of the arm's kick window along the span, sanitized.</summary>
		public float ArmHalfWidth01 {
			get {
				var value = Sanitize(_armHalfWidth01, DefaultArmHalfWidth01);
				return value <= 0f ? DefaultArmHalfWidth01 : Mathf.Min(value, 0.5f);
			}
			set => _armHalfWidth01 = value;
		}

		/// <summary>Causal-ball kick window in milliseconds, sanitized.</summary>
		public float KickWindowMs {
			get => Sanitize(_kickWindowMs, DefaultKickWindowMs);
			set => _kickWindowMs = value;
		}

		/// <summary>Shortest switch pulse delivered to the gamelogic engine, sanitized.</summary>
		public float MinimumSwitchPulseMs {
			get => Sanitize(_minimumSwitchPulseMs, DefaultMinimumSwitchPulseMs);
			set => _minimumSwitchPulseMs = value;
		}

		/// <summary>
		/// VPX speed units per meter per second, the same factor the elasticity falloff
		/// documents ("half the COR at 1 m/s (18.53 speed units)").
		/// </summary>
		private const float SpeedUnitsPerMeterPerSecond = 18.53f;

		#endregion

		public static bool RuntimeAvailable => true;

		/// <summary>The runtime api, created in <c>Awake</c> in play mode.</summary>
		public RubberSlingshotApi SlingshotApi { get; private set; }

		public IEnumerable<GamelogicEngineSwitch> AvailableSwitches => new[] {
			new GamelogicEngineSwitch(SwitchItem) {
				Description = "Slingshot blade switches",
				IsPulseSwitch = false,
			},
		};

		public IEnumerable<GamelogicEngineCoil> AvailableCoils => new[] {
			new GamelogicEngineCoil(CoilItem) { Description = "Slingshot actuator" },
		};

		public SwitchDefault SwitchDefault => SwitchDefault.NormallyOpen;

		IEnumerable<GamelogicEngineSwitch> IDeviceComponent<GamelogicEngineSwitch>.AvailableDeviceItems
			=> AvailableSwitches;
		IEnumerable<GamelogicEngineCoil> IDeviceComponent<GamelogicEngineCoil>.AvailableDeviceItems
			=> AvailableCoils;
		IEnumerable<IGamelogicEngineDeviceItem> IWireableComponent.AvailableWireDestinations
			=> AvailableCoils;
		IEnumerable<IGamelogicEngineDeviceItem> IDeviceComponent<IGamelogicEngineDeviceItem>.AvailableDeviceItems
			=> AvailableCoils;

		IApiCoil ICoilDeviceComponent.CoilDevice(string deviceId)
			=> SlingshotApi == null ? null : ((IApiCoilDevice)SlingshotApi).Coil(deviceId);

		public byte[] Pack() => RubberSlingshotPackable.Pack(this);

		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files)
			=> RubberSlingshotReferencesPackable.Pack(this, root, refs, files);

		public void Unpack(byte[] bytes) => RubberSlingshotPackable.Unpack(bytes, this);

		public void UnpackReferences(byte[] data, Transform root, PackagedRefs refs,
			PackagedFiles files)
			=> RubberSlingshotReferencesPackable.Unpack(data, this, root, refs, files);

		public bool TryResolveSpan(out ResolvedRubberSpan resolved, out string error)
			=> RubberSpanResolver.TryResolve(Rubber, _span, out resolved, out error);

		public IReadOnlyList<string> ValidateConfiguration(bool includeSceneUniqueness = false)
		{
			var errors = new List<string>();
			if (!Rubber) {
				errors.Add("A physical slingshot must be a component of a rubber.");
			}
			var spanResolved = TryResolveSpan(out var resolved, out var spanError);
			if (!spanResolved) {
				errors.Add(spanError);
			}
			if (_switchZones == null || _switchZones.Length != 2) {
				errors.Add("A physical slingshot requires exactly two switch zones.");
			} else {
				for (var i = 0; i < _switchZones.Length; i++) {
					var zone = _switchZones[i];
					if (!IsNormalized(zone.Position01)) {
						errors.Add($"Switch zone {i + 1} position must be in [0, 1].");
					}
					if (!float.IsFinite(zone.OpenDeflection) || zone.OpenDeflection < 0f
						|| !float.IsFinite(zone.CloseDeflection)
						|| zone.CloseDeflection <= zone.OpenDeflection) {
						errors.Add($"Switch zone {i + 1} requires 0 <= open deflection < close deflection.");
					}
					if (!float.IsFinite(zone.MinimumClosedTimeMs)
						|| zone.MinimumClosedTimeMs < 0f) {
						errors.Add($"Switch zone {i + 1} minimum closed time must be non-negative.");
					}
				}
			}
			if (!IsNormalized(_armContactPosition01)) {
				errors.Add("Arm contact position must be in [0, 1].");
			}
			if (!IsFiniteNonNegative(_armRestClearance)
				|| !IsFiniteNonNegative(_armTipTravel)) {
				errors.Add("Arm clearance and travel must be finite and non-negative.");
			}
			if (!_actuator) {
				errors.Add("Assign a calibrated slingshot actuator asset.");
			} else {
				errors.AddRange(_actuator.ValidateData());
			}
			errors.AddRange(ValidateReboundEligibility(spanResolved, resolved));

			if (includeSceneUniqueness && spanResolved && errors.Count == 0) {
				foreach (var other in FindObjectsByType<RubberSlingshotComponent>(
					FindObjectsInactive.Include, FindObjectsSortMode.None)) {
					if (other == this || other.Rubber != Rubber
						|| !other.TryResolveSpan(out var otherResolved, out _)) {
						continue;
					}
					if (otherResolved.PathElementIndex == resolved.PathElementIndex) {
						errors.Add($"Rubber span is already bound by '{other.name}'.");
						break;
					}
				}
			}
			return errors.Where(error => !string.IsNullOrEmpty(error)).ToArray();
		}

		/// <summary>
		/// The runtime needs the bound span to actually respond compliantly, otherwise
		/// nothing ever deflects and the blade switch can never close.
		/// </summary>
		private IEnumerable<string> ValidateReboundEligibility(bool spanResolved,
			ResolvedRubberSpan resolved)
		{
			if (!Rubber) {
				yield break;
			}
			var collider = Rubber.GetComponent<RubberColliderComponent>();
			if (!collider || !collider.DeformationEnabled) {
				yield return $"Rubber '{Rubber.name}' needs the compliant rebound enabled on its collider.";
				yield break;
			}
			if (!RubberSpanReboundEligibility.TryValidate(collider, out var reason)) {
				yield return $"Rubber '{Rubber.name}' cannot use the compliant rebound: {reason}";
				yield break;
			}
			if (!spanResolved) {
				yield break;
			}
			var eligible = RubberSpanReboundEligibility.EligibleDescriptors(collider);
			for (var i = 0; i < eligible.Count; i++) {
				if (eligible[i].PathElementIndex == resolved.PathElementIndex) {
					yield break;
				}
			}
			yield return "The bound free span is not eligible for the compliant rebound.";
		}

		internal void RestoreReferences(RubberGuideComponent startGuide,
			RubberGuideComponent endGuide, SlingshotActuatorAsset actuator,
			GameObject coilArmVisual)
		{
			_span.StartSupport.Guide = startGuide;
			_span.EndSupport.Guide = endGuide;
			_actuator = actuator;
			_coilArmVisual = coilArmVisual;
		}

		public void ValidateAfterUnpack()
		{
			var errors = ValidateConfiguration();
			if (errors.Count == 0) {
				return;
			}
			enabled = false;
			Debug.LogWarning(
				$"Rubber slingshot '{name}' was unpacked with an invalid configuration: {string.Join(" ", errors)}",
				this);
		}

		/// <summary>
		/// Builds the physics state of the plan's 3.1. The dense span id is not known
		/// yet, see <c>PhysicsEngine.ResolveSlingshotSpans</c>; the zone and arm
		/// positions are converted into span coordinates here, so nothing on the physics
		/// thread has to know about reversed bindings.
		/// </summary>
		internal RubberSlingshotState CreateState()
		{
			var state = new RubberSlingshotState {
				SpanId = -1,
				PathElementIndex = -1,
				RubberItemId = Rubber ? UnityObjectId.Get(Rubber.gameObject) : 0,
				SelfWired = (byte)(_selfWired ? 1 : 0),
				WireDelayUsec = ToUsec(WireDelayMs),
				SelfWirePulseUsec = ToUsec(SelfWirePulseMs),
				KickWindowUsec = ToUsec(KickWindowMs),
				KickSpeed = KickSpeed,
				ArmHalfWidth01 = ArmHalfWidth01,
				ArmReach = Mathf.Max(0f, ArmTipTravel - ArmRestClearance),
				ArmPosition01 = Mathf.Clamp01(_armContactPosition01),
			};
			if (TryResolveSpan(out var resolved, out _)) {
				state.PathElementIndex = resolved.PathElementIndex;
				state.IsReversed = (byte)(resolved.IsReversed ? 1 : 0);
				state.ArmPosition01 = resolved.ToPathCoordinate(state.ArmPosition01);
			}

			var zones = SwitchZones;
			var count = Mathf.Min(zones.Length, RubberSlingshotState.MaxZones);
			state.ZoneCount = count;
			for (var i = 0; i < count; i++) {
				ref var target = ref RubberSlingshotState.Zone(ref state, i);
				var position = Mathf.Clamp01(zones[i].Position01);
				target.Position01 = state.IsReversed != 0 ? 1f - position : position;
				target.CloseDeflection = Sanitize(zones[i].CloseDeflection, 0f);
				target.DwellUsec = ToUsec(Sanitize(zones[i].MinimumClosedTimeMs, 0f));
				target.ClosedUntilUsec = 0;
				target.IsClosed = 0;
			}
			return state;
		}

		private void Awake()
		{
			if (!Application.isPlaying) {
				return;
			}
			var player = GetComponentInParent<Player>();
			if (!player) {
				enabled = false;
				Debug.LogError($"Cannot find player for physical slingshot '{name}'.", this);
				return;
			}
			// Awake order between the components of one game object is not defined, so
			// the bakes are brought up to date here as well as in the collider's Awake.
			var collider = Rubber ? Rubber.GetComponent<RubberColliderComponent>() : null;
			if (collider) {
				collider.EnsureBakesCurrent();
			}
			var errors = ValidateConfiguration();
			if (errors.Count > 0) {
				enabled = false;
				Debug.LogError($"Physical slingshot '{name}' is not configured for play mode: {string.Join(" ", errors)}",
					this);
				return;
			}
			var physicsEngine = GetComponentInParent<PhysicsEngine>();
			if (!physicsEngine) {
				enabled = false;
				Debug.LogError($"Cannot find physics engine for physical slingshot '{name}'.", this);
				return;
			}
			_physicsEngine = physicsEngine;
			SlingshotApi = new RubberSlingshotApi(gameObject, player, physicsEngine, this);
			SlingshotApi.CoilStatusChanged += OnCoilStatusChanged;
			player.Register(SlingshotApi, this);
			physicsEngine.Register(this);
		}

		private void OnDestroy()
		{
			if (SlingshotApi != null) {
				SlingshotApi.CoilStatusChanged -= OnCoilStatusChanged;
			}
		}

		#region Preview

		[NonSerialized] private bool _previewActive;
		[NonSerialized] private Vector3 _previewArmRestEuler;

		/// <summary>Whether an edit-mode preview currently poses the arm and the cord.</summary>
		public bool IsPreviewing => _previewActive;

		/// <summary>
		/// Poses the arm and, with a renderer on the rubber, the cord between rest (0) and
		/// the fully fired pose (1) outside play mode, so the arm's angle and contact
		/// position can be aligned without starting a game. Zero puts everything back
		/// as it was; nothing of the preview is saved. Ignored while the game runs.
		/// </summary>
		public void Preview(float amount01)
		{
			if (Application.isPlaying) {
				return;
			}
			var amount = Mathf.Clamp01(float.IsFinite(amount01) ? amount01 : 0f);
			if (amount <= 0f) {
				EndPreview();
				return;
			}
			if (!_previewActive) {
				_previewActive = true;
				_previewArmRestEuler = _coilArmVisual
					? _coilArmVisual.transform.localEulerAngles
					: Vector3.zero;
			}
			_armValue = amount;
			ApplyArmRotation();

			var renderer = DeformationRenderer;
			if (renderer && TryResolveSpan(out var resolved, out _)) {
				var depth = Mathf.Max(0f, ArmTipTravel - ArmRestClearance);
				renderer.Preview(resolved.PathElementIndex,
					resolved.ToPathCoordinate(Mathf.Clamp01(_armContactPosition01)),
					-depth * amount);
			}
		}

		/// <summary>
		/// Ends the preview: the arm returns to where it was and the cord to its shared
		/// mesh.
		/// </summary>
		public void EndPreview()
		{
			if (!_previewActive) {
				return;
			}
			_previewActive = false;
			_armValue = 0f;
			if (_coilArmVisual) {
				_coilArmVisual.transform.localEulerAngles = _previewArmRestEuler;
			}
			var renderer = DeformationRenderer;
			if (renderer) {
				renderer.EndPreview();
			}
		}

		#endregion

		#region Visuals

		[NonSerialized] private PhysicsEngine _physicsEngine;
		[NonSerialized] private RubberDeformationRenderer _renderer;
		[NonSerialized] private AnimationCurve _fallbackCurve;
		[NonSerialized] private bool _armAnimating;
		[NonSerialized] private float _armStartMs;
		[NonSerialized] private float _fallbackClockMs;
		[NonSerialized] private float _armValue;

		/// <summary>
		/// Current value of the kick animation, zero at rest and one at the fired angle.
		/// </summary>
		public float ArmAnimationValue => _armValue;

		/// <summary>Whether the arm is currently swinging.</summary>
		public bool IsArmAnimating => _armAnimating;

		/// <summary>
		/// The deformation renderer on the bound rubber, or null. A rubber without one
		/// shows only the arm.
		/// </summary>
		internal RubberDeformationRenderer DeformationRenderer {
			get {
				// not latched, so a renderer added later — a package load creates components
				// in an order this component does not control — is still picked up
				if (!_renderer && Rubber) {
					_renderer = Rubber.GetComponent<RubberDeformationRenderer>();
				}
				return _renderer;
			}
		}

		/// <summary>
		/// Shape of the kick animation. It lives on the renderer, so a rubber without one
		/// animates its arm on the renderer's defaults.
		/// </summary>
		private AnimationCurve KickCurve {
			get {
				var renderer = DeformationRenderer;
				if (renderer) {
					return renderer.KickCurve;
				}
				return _fallbackCurve ??= RubberDeformationRenderer.CreateDefaultKickCurve();
			}
		}

		private float KickDurationMs {
			get {
				var renderer = DeformationRenderer;
				return renderer
					? renderer.AnimationDurationMs
					: RubberDeformationRenderer.DefaultAnimationDurationMs;
			}
		}

		/// <summary>
		/// Simulation time the visuals animate on, the same clock the renderer uses.
		/// </summary>
		private float NowMs => _physicsEngine ? _physicsEngine.TimeMsec : _fallbackClockMs;

		private void OnCoilStatusChanged(object sender, NoIdCoilEventArgs args)
		{
			if (!args.IsEnergized) {
				return;
			}
			TriggerKickAnimation();
		}

		/// <summary>
		/// Starts the arm swing and, when the rubber has a renderer, the outward kick
		/// profile on the bound span.
		/// </summary>
		internal void TriggerKickAnimation()
		{
			_armAnimating = true;
			_armStartMs = NowMs;
			_armValue = 0f;

			var renderer = DeformationRenderer;
			if (!renderer || !TryResolveSpan(out var resolved, out _)) {
				return;
			}
			var depth = Mathf.Max(0f, ArmTipTravel - ArmRestClearance);
			renderer.PlayKick(resolved.PathElementIndex,
				resolved.ToPathCoordinate(Mathf.Clamp01(_armContactPosition01)), depth);
		}

		private void Update()
		{
			if (!Application.isPlaying) {
				return;
			}
			if (Time.deltaTime > 0f) {
				_fallbackClockMs += Time.deltaTime * 1000f;
			}
			if (!_armAnimating) {
				return;
			}
			var duration = KickDurationMs;
			var progress = duration > 0f ? (NowMs - _armStartMs) / duration : 1f;
			if (progress >= 1f || progress < 0f) {
				_armAnimating = false;
				_armValue = 0f;
			} else {
				_armValue = KickCurve.Evaluate(Mathf.Clamp01(progress));
			}
			ApplyArmRotation();
		}

		/// <summary>
		/// Rotates the arm visual from the rest angle to the fired angle on the kick
		/// curve, the way the legacy slingshot component does.
		/// </summary>
		private void ApplyArmRotation()
		{
			if (!_coilArmVisual) {
				return;
			}
			var angle = Mathf.LerpUnclamped(_visualRestAngle, _visualFiredAngle, _armValue);
			var euler = _coilArmVisual.transform.localEulerAngles;
			switch (_visualRotationAxis) {
				case Axis.X: euler.x = angle; break;
				case Axis.Y: euler.y = angle; break;
				case Axis.Z: euler.z = angle; break;
			}
			_coilArmVisual.transform.localEulerAngles = euler;
		}

		#endregion

		private static ulong ToUsec(float milliseconds)
			=> (ulong)Mathf.Max(0f, Mathf.Round(milliseconds * 1000f));

		private static float Sanitize(float value, float fallback)
			=> float.IsFinite(value) && value >= 0f ? value : fallback;

		private static bool IsNormalized(float value)
			=> float.IsFinite(value) && value >= 0f && value <= 1f;

		private static bool IsFiniteNonNegative(float value)
			=> float.IsFinite(value) && value >= 0f;
	}
}
