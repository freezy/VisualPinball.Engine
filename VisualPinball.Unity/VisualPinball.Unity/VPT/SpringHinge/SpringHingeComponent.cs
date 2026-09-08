// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Collections.Generic;
using NLog;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;
using VisualPinball.Unity.Collections;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	[DisallowMultipleComponent]
	[PackAs("SpringHinge")]
	[AddComponentMenu("Pinball/Mechs/Spring Hinge")]
	public class SpringHingeComponent : MonoBehaviour, IAnimationValueEmitter<float>, IPackable, ISwitchDeviceComponent
	{
		private const float MillimetersToWorld = 0.001f;
		public const string AngleSwitchItem = "angle_switch";
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		[Tooltip("Fixed hinge axis in this object's local frame.")]
		public Vector3 HingeAxis = Vector3.right;

		[Unit("mm")]
		[Tooltip("Unloaded toy centre of mass relative to the pivot, in this object's local frame.")]
		public Vector3 CentreOfMass = new(0f, -50f, 0f);

		[Min(0.001f)]
		[Tooltip("Unloaded toy mass relative to VPE's standard ball mass.")]
		public float ToyMass = 1f;

		[Tooltip("Use Manual Inertia instead of the box estimate.")]
		public bool OverrideInertia = true;

		[Min(0.001f)]
		[Tooltip("Moment of inertia about the hinge axis in ball-mass times VPX-unit squared.")]
		public float ManualInertia = 2500f;

		[Unit("mm")]
		[Tooltip("Half-extents of the box used to estimate unloaded toy inertia.")]
		public Vector3 MassBoxHalfExtents = new(25f, 50f, 10f);

		[Min(0f)]
		[Tooltip("Torsional spring stiffness in hinge simulation units.")]
		public float SpringStiffness = 100f;

		[Min(0f)]
		[Tooltip("Physical torsional damping coefficient in hinge simulation units.")]
		public float SpringDamping = 5f;

		[Range(-180f, 180f)]
		[Tooltip("Canonical spring equilibrium angle in degrees.")]
		public float EquilibriumAngle;

		[Range(-180f, 180f)]
		[Tooltip("Lower hard stop in degrees.")]
		public float MinimumAngle = -30f;

		[Range(-180f, 180f)]
		[Tooltip("Upper hard stop in degrees.")]
		public float MaximumAngle = 30f;

		[Range(-180f, 180f)]
		[Tooltip("Runtime angle at table start in degrees.")]
		public float InitialAngle;

		[Tooltip("Expose a maintained switch that closes above Close Angle and opens below Open Angle.")]
		public bool EnableAngleSwitch;

		[Range(-180f, 180f)] public float SwitchCloseAngle = 10f;
		[Range(-180f, 180f)] public float SwitchOpenAngle = 5f;

		public SpringHingeApi SpringHingeApi { get; private set; }
		public int ItemId => UnityObjectId.Get(gameObject);
		internal float PublishedAngle => _animationValue;

		public event Action<float> OnAnimationValueChanged;

		private PhysicsEngine _physicsEngine;
		private float _animationValue;
		private Matrix4x4 _referenceLocalMatrix;
		private Quaternion _referenceLocalRotation;
		private bool _referencePoseCaptured;

		public IEnumerable<GamelogicEngineSwitch> AvailableSwitches => EnableAngleSwitch
			? new[] { new GamelogicEngineSwitch(AngleSwitchItem) }
			: Array.Empty<GamelogicEngineSwitch>();

		public SwitchDefault SwitchDefault => SwitchDefault.NormallyOpen;

		IEnumerable<GamelogicEngineSwitch> IDeviceComponent<GamelogicEngineSwitch>.AvailableDeviceItems
			=> AvailableSwitches;

		public byte[] Pack() => SpringHingePackable.Pack(this);

		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files)
			=> Array.Empty<byte>();

		public void Unpack(byte[] bytes) => SpringHingePackable.Unpack(bytes, this);

		public void UnpackReferences(byte[] data, Transform root, PackagedRefs refs, PackagedFiles files) { }

		private void Awake()
		{
			CaptureReferencePose();
			var player = GetComponentInParent<Player>();
			if (!player) {
				Logger.Error($"Cannot find player for spring hinge {name}.");
				return;
			}

			_physicsEngine = GetComponentInParent<PhysicsEngine>();
			SpringHingeApi = new SpringHingeApi(this, _physicsEngine);
			player.Register(SpringHingeApi, this);
			if (_physicsEngine) {
				_physicsEngine.Register(this);
			} else {
				Logger.Error($"Cannot find physics engine for spring hinge {name}.");
			}
		}

		private void OnValidate()
		{
			if (math.lengthsq((float3)HingeAxis) < 1e-8f) {
				HingeAxis = Vector3.right;
			}
			ToyMass = math.max(0.001f, ToyMass);
			ManualInertia = math.max(0.001f, ManualInertia);
			MassBoxHalfExtents = Vector3.Max(MassBoxHalfExtents, Vector3.zero);
			SpringStiffness = math.max(0f, SpringStiffness);
			SpringDamping = math.max(0f, SpringDamping);
			if (MinimumAngle > MaximumAngle) {
				(MinimumAngle, MaximumAngle) = (MaximumAngle, MinimumAngle);
			}
			if (SwitchOpenAngle > SwitchCloseAngle) {
				(SwitchOpenAngle, SwitchCloseAngle) = (SwitchCloseAngle, SwitchOpenAngle);
			}
			InitialAngle = math.clamp(InitialAngle, MinimumAngle, MaximumAngle);
			SyncPhysicsState();
		}

		internal SpringHingeState CreateState()
		{
			var pivot = ToPlayfieldVpx(ReferenceLocalToWorldMatrix.MultiplyPoint3x4(Vector3.zero));
			var axis = ToPlayfieldDirection(HingeAxis);
			var centreOfMass = ToPlayfieldVpx(ReferenceLocalToWorldMatrix.MultiplyPoint3x4(
				CentreOfMass * MillimetersToWorld));
			var minimumAngle = math.radians(math.min(MinimumAngle, MaximumAngle));
			var maximumAngle = math.radians(math.max(MinimumAngle, MaximumAngle));
			var angle = math.clamp(math.radians(InitialAngle), minimumAngle, maximumAngle);

			var staticState = new SpringHingeStaticState {
				OwnerId = ItemId,
				Pivot = pivot,
				Axis = axis,
				CentreOfMassArm = centreOfMass - pivot,
				Mass = ToyMass,
				Inertia = OverrideInertia ? ManualInertia : EstimateInertia(axis),
				EquilibriumAngle = math.radians(EquilibriumAngle),
				Stiffness = SpringStiffness,
				Damping = SpringDamping,
				MinimumAngle = minimumAngle,
				MaximumAngle = maximumAngle
			};
			return new SpringHingeState(ItemId, staticState, new SpringHingeMovementState {
				Angle = angle,
				ActiveStop = angle <= minimumAngle ? (sbyte)-1 : angle >= maximumAngle ? (sbyte)1 : (sbyte)0
			});
		}

		public void UpdateAnimationValue(float angle)
		{
			if (math.abs(DeltaAngle(_animationValue, angle)) <= 0.0005f) {
				return;
			}
			_animationValue = angle;
			OnAnimationValueChanged?.Invoke(angle);
			SpringHingeApi?.OnAngleChanged(angle);
		}

		private static float DeltaAngle(float first, float second)
		{
			var delta = math.fmod(first - second + math.PI, math.TAU);
			if (delta < 0f) {
				delta += math.TAU;
			}
			return delta - math.PI;
		}

		private float EstimateInertia(float3 axis)
		{
			var x = ToPlayfieldDirection(Vector3.right);
			var y = ToPlayfieldDirection(Vector3.up);
			var z = ToPlayfieldDirection(Vector3.forward);
			var halfExtents = new float3(
				Physics.ScaleToVpx(MassBoxHalfExtents.x * MillimetersToWorld * math.abs(transform.lossyScale.x)),
				Physics.ScaleToVpx(MassBoxHalfExtents.y * MillimetersToWorld * math.abs(transform.lossyScale.y)),
				Physics.ScaleToVpx(MassBoxHalfExtents.z * MillimetersToWorld * math.abs(transform.lossyScale.z)));
			var principal = ToyMass / 3f * new float3(
				halfExtents.y * halfExtents.y + halfExtents.z * halfExtents.z,
				halfExtents.x * halfExtents.x + halfExtents.z * halfExtents.z,
				halfExtents.x * halfExtents.x + halfExtents.y * halfExtents.y);
			var inertiaAtCentre = math.dot(principal, new float3(
				math.pow(math.dot(axis, x), 2f),
				math.pow(math.dot(axis, y), 2f),
				math.pow(math.dot(axis, z), 2f)));
			var referenceMatrix = ReferenceLocalToWorldMatrix;
			var centreArm = ToPlayfieldVpx(referenceMatrix.MultiplyPoint3x4(CentreOfMass * MillimetersToWorld))
				- ToPlayfieldVpx(referenceMatrix.MultiplyPoint3x4(Vector3.zero));
			var perpendicularArm = centreArm - axis * math.dot(axis, centreArm);
			return math.max(0.001f, inertiaAtCentre + ToyMass * math.lengthsq(perpendicularArm));
		}

		internal float3 ToPlayfieldVpx(Vector3 worldPosition)
		{
			var playfield = GetComponentInParent<PlayfieldComponent>();
			return playfield
				? (float3)worldPosition.TranslateToVpx(playfield.transform)
				: (float3)worldPosition.TranslateToVpx();
		}

		internal float3 ToPlayfieldDirection(Vector3 localDirection)
		{
			var direction = ReferenceWorldRotation * localDirection.normalized;
			var playfield = GetComponentInParent<PlayfieldComponent>();
			if (playfield) {
				direction = playfield.transform.InverseTransformDirection(direction);
			}
			return math.normalizesafe(Physics.WorldToVpx.MultiplyVector(direction), new float3(1f, 0f, 0f));
		}

		internal float3 ToPlayfieldVector(Vector3 localVector)
		{
			var vector = ReferenceLocalToWorldMatrix.MultiplyVector(localVector);
			var playfield = GetComponentInParent<PlayfieldComponent>();
			if (playfield) {
				vector = playfield.transform.InverseTransformVector(vector);
			}
			return Physics.WorldToVpx.MultiplyVector(vector);
		}

		internal Matrix4x4 ReferenceLocalToWorldMatrix {
			get {
				if (!Application.isPlaying || !_referencePoseCaptured) {
					return transform.localToWorldMatrix;
				}
				var parentMatrix = transform.parent
					? transform.parent.localToWorldMatrix
					: Matrix4x4.identity;
				return parentMatrix * _referenceLocalMatrix;
			}
		}

		private Quaternion ReferenceWorldRotation {
			get {
				if (!Application.isPlaying || !_referencePoseCaptured) {
					return transform.rotation;
				}
				return transform.parent
					? transform.parent.rotation * _referenceLocalRotation
					: _referenceLocalRotation;
			}
		}

		private void CaptureReferencePose()
		{
			_referenceLocalMatrix = Matrix4x4.TRS(transform.localPosition,
				transform.localRotation, transform.localScale);
			_referenceLocalRotation = transform.localRotation;
			_referencePoseCaptured = true;
		}

		private void SyncPhysicsState()
		{
			if (!Application.isPlaying || !_physicsEngine) {
				return;
			}

			var itemId = ItemId;
			var synced = CreateState();
			var hasBakedCollider = GetComponent<SpringHingeColliderComponent>() != null;
			var componentName = name;
			_physicsEngine.MutateState((ref PhysicsState state) => {
				if (!state.SpringHingeStates.ContainsKey(itemId)) {
					return;
				}
				ref var hinge = ref state.SpringHingeStates.GetValueByRef(itemId);
				if (hasBakedCollider) {
					var geometryChanged = math.distancesq(synced.Static.Pivot, hinge.Static.Pivot) > 1e-8f
						|| math.distancesq(synced.Static.Axis, hinge.Static.Axis) > 1e-8f;
					if (geometryChanged) {
						Logger.Warn($"Spring hinge {componentName} transform changes require a physics rebuild; keeping its baked collision frame for this play session.");
					}
					synced.Static.Pivot = hinge.Static.Pivot;
					synced.Static.Axis = hinge.Static.Axis;
				}
				synced.Movement = hinge.Movement;
				synced.Movement.Angle = math.clamp(synced.Movement.Angle,
					synced.Static.MinimumAngle, synced.Static.MaximumAngle);
				hinge = synced;
			});
		}
	}
}
