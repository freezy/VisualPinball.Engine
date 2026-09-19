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

		[Tooltip("The Motion Controller that moves the arm. The slingshot feeds its coil when the slingshot's coil fires; the controller's mode, travel and followers decide how the arm moves, and the cord follows its position.")]
		[SerializeField] private MotionControllerComponent _motionController;

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
		public MotionControllerComponent MotionController { get => _motionController; set => _motionController = value; }

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
			if (_actuator) {
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
			MotionControllerComponent motionController)
		{
			_span.StartSupport.Guide = startGuide;
			_span.EndSupport.Guide = endGuide;
			_actuator = actuator;
			_motionController = motionController;
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
			SlingshotApi = new RubberSlingshotApi(gameObject, player, physicsEngine, this);
			SlingshotApi.CoilStatusChanged += OnCoilStatusChanged;
			player.Register(SlingshotApi, this);
			physicsEngine.Register(this);
			if (_motionController) {
				_motionController.OnAnimationValueChanged += OnArmPositionChanged;
			}
		}

		private void OnDestroy()
		{
			if (SlingshotApi != null) {
				SlingshotApi.CoilStatusChanged -= OnCoilStatusChanged;
			}
			if (_motionController) {
				_motionController.OnAnimationValueChanged -= OnArmPositionChanged;
			}
		}

		#region Preview

		[NonSerialized] private bool _previewActive;
		[NonSerialized] private MotionTransformComponent[] _previewFollowers = Array.Empty<MotionTransformComponent>();
		[NonSerialized] private Vector3[] _previewFollowerPositions = Array.Empty<Vector3>();
		[NonSerialized] private Quaternion[] _previewFollowerRotations = Array.Empty<Quaternion>();

		/// <summary>Whether an edit-mode preview currently poses the arm and the cord.</summary>
		public bool IsPreviewing => _previewActive;

		/// <summary>
		/// Poses the arm and, with a renderer on the rubber, the cord between rest (0) and
		/// the fully fired pose (1) outside play mode, so the arm can be aligned without
		/// starting a game. The arm is posed through the Motion Controller's followers.
		/// Zero puts everything back as it was; nothing of the preview is saved. Ignored
		/// while the game runs.
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
				_previewFollowers = FindArmFollowers();
				_previewFollowerPositions = new Vector3[_previewFollowers.Length];
				_previewFollowerRotations = new Quaternion[_previewFollowers.Length];
				for (var i = 0; i < _previewFollowers.Length; i++) {
					_previewFollowerPositions[i] = _previewFollowers[i].transform.localPosition;
					_previewFollowerRotations[i] = _previewFollowers[i].transform.localRotation;
					_previewFollowers[i].CaptureInitialPose();
				}
			}
			foreach (var follower in _previewFollowers) {
				if (follower) {
					follower.ApplyValue(amount);
				}
			}
			var renderer = DeformationRenderer;
			if (renderer && TryResolveSpan(out var resolved, out _)) {
				renderer.Preview(resolved.PathElementIndex,
					resolved.ToPathCoordinate(Mathf.Clamp01(_armContactPosition01)),
					-ArmTravel * amount);
			}
		}

		/// <summary>
		/// Ends the preview: the arm's followers return to where they were and the cord
		/// to its shared mesh.
		/// </summary>
		public void EndPreview()
		{
			if (!_previewActive) {
				return;
			}
			_previewActive = false;
			for (var i = 0; i < _previewFollowers.Length; i++) {
				var follower = _previewFollowers[i];
				if (!follower) {
					continue;
				}
				follower.transform.localPosition = _previewFollowerPositions[i];
				follower.transform.localRotation = _previewFollowerRotations[i];
			}
			_previewFollowers = Array.Empty<MotionTransformComponent>();
			var renderer = DeformationRenderer;
			if (renderer) {
				renderer.EndPreview();
			}
		}

		/// <summary>
		/// The Motion Transform followers the arm's Motion Controller drives: those that
		/// name it as their emitter, and those without an emitter that resolve to it the
		/// way they do at runtime, through the nearest emitter up their own hierarchy.
		/// </summary>
		private MotionTransformComponent[] FindArmFollowers()
		{
			if (!_motionController) {
				return Array.Empty<MotionTransformComponent>();
			}
			return FindObjectsByType<MotionTransformComponent>(FindObjectsInactive.Include,
					FindObjectsSortMode.None)
				.Where(follower => follower._emitter
					? follower._emitter == _motionController
					: follower.GetComponentsInParent<IAnimationValueEmitter>(true)
						.OfType<IAnimationValueEmitter<float>>()
						.FirstOrDefault() == (IAnimationValueEmitter<float>)_motionController)
				.ToArray();
		}

		#endregion

		#region Visuals

		[NonSerialized] private RubberDeformationRenderer _renderer;
		[NonSerialized] private float _armPosition;

		/// <summary>
		/// Where the arm is between rest (0) and fired (1), as its Motion Controller
		/// reports it; zero without one.
		/// </summary>
		public float ArmPosition => _armPosition;

		/// <summary>How far the arm's tip travels past the cord's rest line, in VPX units.</summary>
		private float ArmTravel => Mathf.Max(0f, ArmTipTravel - ArmRestClearance);

		/// <summary>
		/// The deformation renderer on the bound rubber, or null. A rubber without one
		/// shows only the arm.
		/// </summary>
		internal RubberDeformationRenderer DeformationRenderer {
			get {
				// not latched, so a renderer added later, a package load creates components
				// in an order this component does not control, is still picked up
				if (!_renderer && Rubber) {
					_renderer = Rubber.GetComponent<RubberDeformationRenderer>();
				}
				return _renderer;
			}
		}

		/// <summary>
		/// The slingshot's coil is the arm's coil: both edges are handed to the Motion
		/// Controller, whose mode, travel and followers decide how the arm moves. A
		/// rubber without a Motion Controller still shows the kick on the cord, on the
		/// renderer's timed curve.
		/// </summary>
		private void OnCoilStatusChanged(object sender, NoIdCoilEventArgs args)
		{
			if (_motionController && _motionController.MotionControllerApi != null) {
				((IApiCoil)_motionController.MotionControllerApi).OnCoil(args.IsEnergized);
				return;
			}
			if (args.IsEnergized) {
				TriggerKickAnimation();
			}
		}

		/// <summary>
		/// The cord follows the arm: it is pushed outward at the contact position by the
		/// arm's travel times the arm's position.
		/// </summary>
		private void OnArmPositionChanged(float position)
		{
			_armPosition = Mathf.Clamp01(float.IsFinite(position) ? position : 0f);
			var renderer = DeformationRenderer;
			if (!renderer || !TryResolveSpan(out var resolved, out _)) {
				return;
			}
			renderer.DriveKick(resolved.PathElementIndex,
				resolved.ToPathCoordinate(Mathf.Clamp01(_armContactPosition01)),
				ArmTravel * _armPosition);
		}

		/// <summary>
		/// Plays the renderer's timed kick on the bound span, for a rubber whose arm is
		/// not modelled.
		/// </summary>
		internal void TriggerKickAnimation()
		{
			var renderer = DeformationRenderer;
			if (!renderer || !TryResolveSpan(out var resolved, out _)) {
				return;
			}
			renderer.PlayKick(resolved.PathElementIndex,
				resolved.ToPathCoordinate(Mathf.Clamp01(_armContactPosition01)), ArmTravel);
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
