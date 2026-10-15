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
using NLog;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	/// <summary>
	/// A slingshot built on a guide-driven rubber: one of the rubber's free spans is the
	/// slingshot's span, a slingshot wall in front of it kicks the ball and closes the
	/// switch, and the coil swings the arm and pushes the cord out at the arm.
	/// </summary>
	/// <remarks>
	/// The kick is the slingshot wall's: a Surface whose front edge is a slingshot
	/// segment, the way Visual Pinball has always done it. This component wires that
	/// wall's slingshot event to its switch and animates the rubber and the arm from its
	/// coil.
	/// </remarks>
	[PackAs("RubberSlingshot")]
	[AddComponentMenu("Pinball/Game Item/Rubber Slingshot")]
	[DisallowMultipleComponent]
	[RequireComponent(typeof(RubberDeformationRenderer))]
	public sealed class RubberSlingshotComponent : MonoBehaviour,
		ISwitchDeviceComponent, ICoilDeviceComponent, IPackable
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public const string SwitchItem = "slingshot_switch";
		public const string CoilItem = "slingshot_coil";

		#region Defaults

		internal const bool DefaultSelfWired = true;
		internal const float DefaultSelfWirePulseMs = 20f;

		#endregion

		[SerializeField] private RubberSpanBinding _span;
		[SerializeField, Range(0f, 1f)] private float _armContactPosition01 = 0.5f;
		[SerializeField, Min(0f)] private float _armRestClearance = 2f;
		[SerializeField, Min(0f)] private float _armTipTravel = 18f;

		[Tooltip("The Surface in front of the span whose slingshot edge kicks the ball. Its slingshot event closes this slingshot's switch.")]
		[SerializeField] private SurfaceComponent _slingshotWall;

		[Tooltip("When on, the switch fires the coil by itself, the way a real slingshot's switch is wired straight to it. Turn it off to drive the coil only from the gamelogic engine or a player wire.")]
		[SerializeField] private bool _selfWired = DefaultSelfWired;

		[Tooltip("How long the self-wired coil stays energized, in milliseconds.")]
		[SerializeField, Min(0f)] private float _selfWirePulseMs = DefaultSelfWirePulseMs;

		[Tooltip("The Motion Controller that moves the arm. The slingshot's coil fires it; the controller's mode, travel and followers decide how the arm moves, and the cord follows its position.")]
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
		public float ArmContactPosition01 { get => _armContactPosition01; set => _armContactPosition01 = value; }
		public float ArmRestClearance { get => _armRestClearance; set => _armRestClearance = value; }
		public float ArmTipTravel { get => _armTipTravel; set => _armTipTravel = value; }
		public SurfaceComponent SlingshotWall { get => _slingshotWall; set => _slingshotWall = value; }
		public MotionControllerComponent MotionController { get => _motionController; set => _motionController = value; }

		/// <summary>Whether the switch fires the coil by itself.</summary>
		public bool SelfWired { get => _selfWired; set => _selfWired = value; }

		/// <summary>Self-wire pulse length in milliseconds, sanitized.</summary>
		public float SelfWirePulseMs {
			get => float.IsFinite(_selfWirePulseMs) && _selfWirePulseMs >= 0f
				? _selfWirePulseMs
				: DefaultSelfWirePulseMs;
			set => _selfWirePulseMs = value;
		}

		/// <summary>The runtime api, created in <c>Awake</c> in play mode.</summary>
		public RubberSlingshotApi SlingshotApi { get; private set; }

		public IEnumerable<GamelogicEngineSwitch> AvailableSwitches => new[] {
			new GamelogicEngineSwitch(SwitchItem) {
				Description = "Slingshot switch",
				IsPulseSwitch = true,
			},
		};

		public IEnumerable<GamelogicEngineCoil> AvailableCoils => new[] {
			new GamelogicEngineCoil(CoilItem) { Description = "Slingshot coil" },
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
			=> RubberSlingshotReferencesPackable.Pack(this, refs);

		public void Unpack(byte[] bytes) => RubberSlingshotPackable.Unpack(bytes, this);

		public void UnpackReferences(byte[] data, Transform root, PackagedRefs refs,
			PackagedFiles files)
			=> RubberSlingshotReferencesPackable.Unpack(data, this, refs);

		public bool TryResolveSpan(out ResolvedRubberSpan resolved, out string error)
			=> RubberSpanResolver.TryResolve(Rubber, _span, out resolved, out error);

		public IReadOnlyList<string> ValidateConfiguration(bool includeSceneUniqueness = false)
		{
			var errors = new List<string>();
			if (!Rubber) {
				errors.Add("A rubber slingshot must be a component of a rubber.");
			}
			var spanResolved = TryResolveSpan(out var resolved, out var spanError);
			if (!spanResolved) {
				errors.Add(spanError);
			}
			if (!IsNormalized(_armContactPosition01)) {
				errors.Add("Arm contact position must be in [0, 1].");
			}
			if (!IsFiniteNonNegative(_armRestClearance)
				|| !IsFiniteNonNegative(_armTipTravel)) {
				errors.Add("Arm clearance and travel must be finite and non-negative.");
			}
			if (!_slingshotWall) {
				errors.Add("The slingshot has no slingshot wall, so nothing kicks the ball or closes the switch.");
			}

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

		internal void RestoreReferences(RubberGuideComponent startGuide,
			RubberGuideComponent endGuide, SurfaceComponent slingshotWall,
			MotionControllerComponent motionController)
		{
			_span.StartSupport.Guide = startGuide;
			_span.EndSupport.Guide = endGuide;
			_slingshotWall = slingshotWall;
			_motionController = motionController;
		}

		public void ValidateAfterUnpack()
		{
			var errors = ValidateConfiguration();
			if (errors.Count == 0) {
				return;
			}
			Logger.Warn("Rubber slingshot {Name} was unpacked with an incomplete configuration: {Errors:l}", name, string.Join(" ", errors));
		}

		#region Runtime

		[NonSerialized] private float _selfWireReleaseAt = float.NaN;

		private void Awake()
		{
			if (!Application.isPlaying) {
				return;
			}
			var player = GetComponentInParent<Player>();
			if (!player) {
				enabled = false;
				Logger.Warn("Cannot find player for rubber slingshot {Name}. Is it a child of a table?", name);
				return;
			}
			var errors = ValidateConfiguration();
			if (errors.Count > 0) {
				Logger.Warn("Rubber slingshot {Name} is incomplete: {Errors:l}", name, string.Join(" ", errors));
			}
			SlingshotApi = new RubberSlingshotApi(gameObject, player,
				GetComponentInParent<PhysicsEngine>(), this);
			SlingshotApi.CoilStatusChanged += OnCoilStatusChanged;
			player.Register(SlingshotApi, this);
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

		private void Update()
		{
			if (float.IsNaN(_selfWireReleaseAt) || Time.time < _selfWireReleaseAt) {
				return;
			}
			_selfWireReleaseAt = float.NaN;
			SlingshotApi?.SetCoil(false);
		}

		/// <summary>
		/// The switch closed with the slingshot wired to its coil: one pulse of
		/// <see cref="SelfWirePulseMs"/>. A pulse that is still running is not restarted.
		/// </summary>
		internal void FireSelfWiredPulse()
		{
			if (SlingshotApi == null || SlingshotApi.IsCoilEnergized) {
				return;
			}
			SlingshotApi.SetCoil(true);
			_selfWireReleaseAt = Time.time + SelfWirePulseMs / 1000f;
		}

		#endregion

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

		/// <summary>How far the arm pushes the cord out past its rest line, in VPX units.</summary>
		private float ArmTravel => Mathf.Max(0f, ArmTipTravel - ArmRestClearance);

		/// <summary>The deformation renderer on the bound rubber, or null.</summary>
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
		/// The cord follows the arm: it is pushed out at the contact position by the
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
		/// Plays the renderer's timed kick on the bound span, for a slingshot whose arm is
		/// not animated.
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

		private static bool IsNormalized(float value)
			=> float.IsFinite(value) && value >= 0f && value <= 1f;

		private static bool IsFiniteNonNegative(float value)
			=> float.IsFinite(value) && value >= 0f;
	}
}
