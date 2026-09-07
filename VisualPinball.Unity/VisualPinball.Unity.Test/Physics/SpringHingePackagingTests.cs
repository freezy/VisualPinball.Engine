// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Unity.Simulation;

namespace VisualPinball.Unity.Test
{
	public class SpringHingePackagingTests
	{
		[Test]
		public void HingeAndColliderValuesRoundTrip()
		{
			var gameObject = new GameObject("Spring Hinge");
			try {
				var hinge = gameObject.AddComponent<SpringHingeComponent>();
				var collider = gameObject.AddComponent<SpringHingeColliderComponent>();
				hinge.HingeAxis = new Vector3(0f, 0f, 1f);
				hinge.CentreOfMass = new Vector3(1f, 2f, 3f);
				hinge.ToyMass = 4f;
				hinge.OverrideInertia = false;
				hinge.ManualInertia = 5f;
				hinge.MassBoxHalfExtents = new Vector3(6f, 7f, 8f);
				hinge.SpringStiffness = 9f;
				hinge.SpringDamping = 10f;
				hinge.EquilibriumAngle = 11f;
				hinge.MinimumAngle = -12f;
				hinge.MaximumAngle = 13f;
				hinge.InitialAngle = 3f;
				hinge.EnableAngleSwitch = true;
				hinge.SwitchCloseAngle = 23f;
				hinge.SwitchOpenAngle = 17f;
				collider.LocalCentre = new Vector3(14f, 15f, 16f);
				collider.LocalRotation = new Vector3(17f, 18f, 19f);
				collider.HalfExtents = new Vector3(20f, 21f, 22f);
				collider.Elasticity = 0.4f;
				collider.ElasticityFalloff = 0.5f;
				collider.Friction = 0.6f;
				collider.HitEvent = false;
				collider.HitThreshold = 7f;
				collider.OverwritePhysics = false;
				var hingeBytes = hinge.Pack();
				var colliderBytes = collider.Pack();
				var refs = new PackagedRefs(gameObject.transform);
				var files = new PackagedFiles(null, refs);
				var colliderReferenceBytes = collider.PackReferences(
					gameObject.transform, refs, files);

				hinge.HingeAxis = Vector3.right;
				hinge.CentreOfMass = Vector3.zero;
				hinge.ToyMass = 1f;
				collider.LocalCentre = Vector3.zero;
				collider.HitEvent = true;
				collider.OverwritePhysics = true;
				hinge.Unpack(hingeBytes);
				collider.Unpack(colliderBytes);
				collider.UnpackReferences(colliderReferenceBytes,
					gameObject.transform, refs, files);

				Assert.That(hinge.HingeAxis, Is.EqualTo(new Vector3(0f, 0f, 1f)));
				Assert.That(hinge.CentreOfMass, Is.EqualTo(new Vector3(1f, 2f, 3f)));
				Assert.That(hinge.ToyMass, Is.EqualTo(4f));
				Assert.That(hinge.OverrideInertia, Is.False);
				Assert.That(hinge.ManualInertia, Is.EqualTo(5f));
				Assert.That(hinge.MassBoxHalfExtents, Is.EqualTo(new Vector3(6f, 7f, 8f)));
				Assert.That(hinge.SpringStiffness, Is.EqualTo(9f));
				Assert.That(hinge.SpringDamping, Is.EqualTo(10f));
				Assert.That(hinge.EquilibriumAngle, Is.EqualTo(11f));
				Assert.That(hinge.MinimumAngle, Is.EqualTo(-12f));
				Assert.That(hinge.MaximumAngle, Is.EqualTo(13f));
				Assert.That(hinge.InitialAngle, Is.EqualTo(3f));
				Assert.That(hinge.EnableAngleSwitch, Is.True);
				Assert.That(hinge.SwitchCloseAngle, Is.EqualTo(23f));
				Assert.That(hinge.SwitchOpenAngle, Is.EqualTo(17f));
				Assert.That(collider.LocalCentre, Is.EqualTo(new Vector3(14f, 15f, 16f)));
				Assert.That(collider.LocalRotation, Is.EqualTo(new Vector3(17f, 18f, 19f)));
				Assert.That(collider.HalfExtents, Is.EqualTo(new Vector3(20f, 21f, 22f)));
				Assert.That(collider.Elasticity, Is.EqualTo(0.4f));
				Assert.That(collider.ElasticityFalloff, Is.EqualTo(0.5f));
				Assert.That(collider.Friction, Is.EqualTo(0.6f));
				Assert.That(collider.HitEvent, Is.False);
				Assert.That(collider.HitThreshold, Is.EqualTo(7f));
				Assert.That(collider.OverwritePhysics, Is.False);
				Assert.That(collider.PhysicsMaterial, Is.Null);
			} finally {
				UnityEngine.Object.DestroyImmediate(gameObject);
			}
		}

		[Test]
		public void OwnedMagnetVersionFourRoundTripsAndOlderVersionsStayUnowned()
		{
			var gameObject = new GameObject("Magnet");
			try {
				var magnet = gameObject.AddComponent<MagnetComponent>();
				magnet.CoupleToParentHinge = true;
				magnet.HeldBallCentreOffset = new Vector3(1f, 2f, 3f);
				magnet.HoldStiffness = 4f;
				magnet.HoldDamping = 5f;
				magnet.MaxHoldForce = 6f;
				var bytes = magnet.Pack();

				magnet.CoupleToParentHinge = false;
				magnet.HeldBallCentreOffset = Vector3.zero;
				magnet.HoldStiffness = 0f;
				magnet.HoldDamping = 0f;
				magnet.MaxHoldForce = 0f;
				magnet.Unpack(bytes);

				Assert.That(magnet.CoupleToParentHinge, Is.True);
				Assert.That(magnet.HeldBallCentreOffset, Is.EqualTo(new Vector3(1f, 2f, 3f)));
				Assert.That(magnet.HoldStiffness, Is.EqualTo(4f));
				Assert.That(magnet.HoldDamping, Is.EqualTo(5f));
				Assert.That(magnet.MaxHoldForce, Is.EqualTo(6f));

				magnet.CoupleToParentHinge = true;
				magnet.Unpack(PackageApi.Packer.Pack(new MagnetPackable { Version = 3 }));
				Assert.That(magnet.CoupleToParentHinge, Is.False);
			} finally {
				UnityEngine.Object.DestroyImmediate(gameObject);
			}
		}

		[Test]
		public void RestoredHierarchyResolvesOwnerAndDisablesPrescribedMotion()
		{
			var hingeObject = new GameObject("Spring Hinge");
			var pivotObject = new GameObject("Preserved Pivot");
			var magnetObject = new GameObject("Owned Magnet");
			try {
				var hinge = hingeObject.AddComponent<SpringHingeComponent>();
				pivotObject.transform.SetParent(hingeObject.transform, false);
				magnetObject.transform.SetParent(pivotObject.transform, false);
				var magnet = magnetObject.AddComponent<MagnetComponent>();
				magnet.MagnetType = MagnetType.Spatial;
				magnet.ForceProfile = MagnetForceProfile.Physical;
				magnet.CoupleToParentHinge = true;
				magnet.IsKinematic = true;

				var state = magnet.CreateState();

				Assert.That(state.CoupleToHinge, Is.True);
				Assert.That(state.HingeOwnerId, Is.EqualTo(hinge.ItemId));
				Assert.That(((IKinematicTransformComponent)magnet).IsKinematic, Is.False);
			} finally {
				UnityEngine.Object.DestroyImmediate(hingeObject);
			}
		}

		[Test]
		public void SynchronousMovementPublishesHingeAngle()
		{
			var states = new NativeParallelHashMap<int, SpringHingeState>(1, Allocator.Temp);
			try {
				states.Add(12, new SpringHingeState(12, default,
					new SpringHingeMovementState { Angle = 0.75f }));
				var emitter = new RecordingEmitter();
				var emitters = new Dictionary<int, IAnimationValueEmitter<float>> { { 12, emitter } };

				new PhysicsMovements().ApplySpringHingeMovement(ref states, emitters);

				Assert.That(emitter.Value, Is.EqualTo(0.75f));
			} finally {
				states.Dispose();
			}
		}

		[Test]
		public void AngleSwitchUsesSeparateCloseAndOpenThresholds()
		{
			var gameObject = new GameObject("Table");
			var hingeObject = new GameObject("Spring Hinge");
			try {
				hingeObject.transform.SetParent(gameObject.transform, false);
				gameObject.AddComponent<Player>();
				var hinge = hingeObject.AddComponent<SpringHingeComponent>();
				hinge.EnableAngleSwitch = true;
				hinge.SwitchCloseAngle = 10f;
				hinge.SwitchOpenAngle = 5f;
				var api = new SpringHingeApi(hinge, null);
				var angleSwitch = (DeviceSwitch)((IApiSwitchDevice)api).Switch(
					SpringHingeComponent.AngleSwitchItem);
				var transitions = new List<bool>();
				angleSwitch.Switch += (_, args) => transitions.Add(args.IsEnabled);

				api.OnAngleChanged(math.radians(11f));
				api.OnAngleChanged(math.radians(8f));
				api.OnAngleChanged(math.radians(4f));

				Assert.That(transitions, Is.EqualTo(new[] { true, false }));
			} finally {
				UnityEngine.Object.DestroyImmediate(gameObject);
			}
		}

		[Test]
		public void OwnedSnapshotsRejectPartialBallOrHingeOutput()
		{
			Assert.DoesNotThrow(() => PhysicsEngineThreading.ValidateOwnedSnapshotCapacity(
				0, SimulationState.MaxBalls + 1, SimulationState.MaxFloatAnimations + 1));
			Assert.Throws<InvalidOperationException>(() =>
				PhysicsEngineThreading.ValidateOwnedSnapshotCapacity(
					1, SimulationState.MaxBalls + 1, 1));
			Assert.Throws<InvalidOperationException>(() =>
				PhysicsEngineThreading.ValidateOwnedSnapshotCapacity(
					1, 1, SimulationState.MaxFloatAnimations + 1));
			Assert.That(PhysicsEngineThreading.ShouldSuppressOwnedSnapshot(
				0, SimulationState.MaxBalls + 1), Is.False);
			Assert.That(PhysicsEngineThreading.ShouldSuppressOwnedSnapshot(
				1, SimulationState.MaxBalls), Is.False);
			Assert.That(PhysicsEngineThreading.ShouldSuppressOwnedSnapshot(
				1, SimulationState.MaxBalls + 1), Is.True);
		}

		private sealed class RecordingEmitter : IAnimationValueEmitter<float>
		{
			public float Value { get; private set; }
			public event Action<float> OnAnimationValueChanged;

			public void UpdateAnimationValue(float value)
			{
				Value = value;
				OnAnimationValueChanged?.Invoke(value);
			}
		}
	}
}
