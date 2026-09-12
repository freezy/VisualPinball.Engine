// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System.Linq;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Unity.Editor;

namespace VisualPinball.Unity.Test
{
	public class SpringHingeAuthoringTests
	{
		[Test]
		public void BashSetupCreatesCompleteOwnedRotatingObject()
		{
			var root = SpringHingeAuthoring.CreateBashToy();
			try {
				var hinge = root.GetComponent<SpringHingeComponent>();
				var proxy = root.GetComponent<SpringHingeColliderComponent>();
				var magnet = root.GetComponentInChildren<MagnetComponent>();

				Assert.That(hinge, Is.Not.Null);
				Assert.That(proxy, Is.Not.Null);
				Assert.That(magnet.CoupleToParentHinge, Is.True);
				Assert.That(magnet.MagnetType, Is.EqualTo(MagnetType.Spatial));
				Assert.That(magnet.ForceProfile, Is.EqualTo(MagnetForceProfile.Physical));
				Assert.That(magnet.Strength, Is.EqualTo(40000f));
				Assert.That(magnet.HoldStiffness, Is.EqualTo(4f));
				Assert.That(magnet.HoldDamping, Is.EqualTo(4f));
				Assert.That(magnet.MaxHoldForce, Is.EqualTo(50f));
				Assert.That(magnet.GetComponentInParent<SpringHingeColliderComponent>(), Is.SameAs(proxy));
				Assert.That(root.GetComponentInChildren<UnityEngine.Collider>(), Is.Null);
				Assert.That(SpringHingeAuthoring.Validate(hinge, proxy), Is.Empty);
			} finally {
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void SpatialOwnedMagnetUsesPhysicalResponseWithoutVisibleProfileSetup()
		{
			var root = SpringHingeAuthoring.CreateBashToy();
			try {
				var hinge = root.GetComponent<SpringHingeComponent>();
				var proxy = root.GetComponent<SpringHingeColliderComponent>();
				var magnet = root.GetComponentInChildren<MagnetComponent>();
				magnet.MagnetType = MagnetType.Spatial;
				magnet.ForceProfile = MagnetForceProfile.VpxCompatible;

				Assert.That(SpringHingeAuthoring.Validate(hinge, proxy), Is.Empty);
				var state = magnet.CreateState();
				Assert.That(state.CoupleToHinge, Is.True);
				Assert.That(state.Profile, Is.EqualTo(MagnetForceProfile.Physical));
			} finally {
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void HoldPointFitAccountsForScaledVisualHierarchyAndBallRadius()
		{
			var root = new GameObject("Scaled Spring Hinge");
			var magnetObject = new GameObject("Owned Magnet");
			try {
				root.transform.localScale = Vector3.one * 0.1f;
				var hinge = root.AddComponent<SpringHingeComponent>();
				var proxy = root.AddComponent<SpringHingeColliderComponent>();
				proxy.LocalCentre = Vector3.zero;
				proxy.HalfExtents = Vector3.one * 100f;

				magnetObject.transform.SetParent(root.transform, false);
				magnetObject.transform.localPosition = Vector3.right * Physics.ScaleToWorld(99f);
				magnetObject.transform.localScale = Vector3.one * 0.7f;
				var magnet = magnetObject.AddComponent<MagnetComponent>();
				magnet.MagnetType = MagnetType.Spatial;
				magnet.CoupleToParentHinge = true;
				magnet.HeldBallCentreOffset = Vector3.zero;

				Assert.That(SpringHingeAuthoring.TryGetHeldBallCentreGap(
					hinge, proxy, magnet, out var initialGap), Is.True);
				Assert.That(initialGap, Is.LessThan(-25f));
				Assert.That(SpringHingeAuthoring.TryGetFittedHeldBallCentreOffset(
					hinge, proxy, magnet, out var fittedOffset), Is.True);

				magnet.HeldBallCentreOffset = fittedOffset;
				Assert.That(fittedOffset.x, Is.EqualTo(25.1f).Within(0.01f));
				Assert.That(SpringHingeAuthoring.TryGetHeldBallCentreGap(
					hinge, proxy, magnet, out var fittedGap), Is.True);
				Assert.That(fittedGap, Is.EqualTo(0f).Within(0.001f));
				Assert.That(SpringHingeAuthoring.Validate(hinge, proxy), Is.Empty);
			} finally {
				Object.DestroyImmediate(root);
			}
		}

		[TestCase(false)]
		[TestCase(true)]
		public void HoldPointFitHandlesExteriorCorner(bool rotateProxy)
		{
			var root = new GameObject("Spring Hinge");
			var magnetObject = new GameObject("Owned Magnet");
			try {
				var hinge = root.AddComponent<SpringHingeComponent>();
				var proxy = root.AddComponent<SpringHingeColliderComponent>();
				proxy.LocalCentre = Vector3.zero;
				proxy.LocalRotation = rotateProxy ? new Vector3(0f, 0f, 37f) : Vector3.zero;
				proxy.HalfExtents = new Vector3(10f, 20f, 30f);

				magnetObject.transform.SetParent(root.transform, false);
				var boxRotation = Quaternion.Euler(proxy.LocalRotation);
				magnetObject.transform.localPosition = boxRotation
					* new Vector3(15f, 25f, 35f) * Physics.ScaleInv;
				var magnet = magnetObject.AddComponent<MagnetComponent>();
				magnet.MagnetType = MagnetType.Spatial;
				magnet.CoupleToParentHinge = true;

				Assert.That(SpringHingeAuthoring.TryGetFittedHeldBallCentreOffset(
					hinge, proxy, magnet, out var fittedOffset), Is.True);

				magnet.HeldBallCentreOffset = fittedOffset;
				Assert.That(SpringHingeAuthoring.TryGetHeldBallCentreGap(
					hinge, proxy, magnet, out var fittedGap), Is.True);
				Assert.That(fittedGap, Is.EqualTo(0f).Within(0.001f));
			} finally {
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void VisualBoundsFitMassAndProxyInVpxUnits()
		{
			var root = SpringHingeAuthoring.CreateBashToy();
			try {
				var hinge = root.GetComponent<SpringHingeComponent>();
				var proxy = root.GetComponent<SpringHingeColliderComponent>();
				hinge.CentreOfMass = Vector3.zero;
				hinge.MassBoxHalfExtents = Vector3.one;
				proxy.LocalCentre = Vector3.zero;
				proxy.HalfExtents = Vector3.one;

				Assert.That(SpringHingeAuthoring.FitFromVisuals(hinge, proxy), Is.True);

				Assert.That(hinge.CentreOfMass.x, Is.EqualTo(0f).Within(0.01f));
				Assert.That(hinge.CentreOfMass.y, Is.EqualTo(-50f).Within(0.01f));
				Assert.That(hinge.CentreOfMass.z, Is.EqualTo(0f).Within(0.01f));
				Assert.That(hinge.MassBoxHalfExtents.x, Is.EqualTo(25f).Within(0.01f));
				Assert.That(hinge.MassBoxHalfExtents.y, Is.EqualTo(50f).Within(0.01f));
				Assert.That(hinge.MassBoxHalfExtents.z, Is.EqualTo(10f).Within(0.01f));
				Assert.That(proxy.LocalCentre, Is.EqualTo(hinge.CentreOfMass));
				Assert.That(proxy.HalfExtents, Is.EqualTo(hinge.MassBoxHalfExtents));
			} finally {
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void AddSetupMovesOnlySelectedVisualsAndPreservesWorldPose()
		{
			var parent = new GameObject("Assembly");
			var selected = GameObject.CreatePrimitive(PrimitiveType.Cube);
			var bracket = GameObject.CreatePrimitive(PrimitiveType.Cube);
			GameObject root = null;
			try {
				selected.name = "Moving Toy";
				selected.transform.SetParent(parent.transform, false);
				selected.transform.SetPositionAndRotation(new Vector3(1f, 2f, 3f),
					Quaternion.Euler(10f, 20f, 30f));
				bracket.name = "Fixed Bracket";
				bracket.transform.SetParent(parent.transform, false);
				var worldPosition = selected.transform.position;
				var worldRotation = selected.transform.rotation;

				root = SpringHingeAuthoring.AddSpringHinge(
					new[] { selected.transform }, selected.transform);

				Assert.That(root, Is.SameAs(selected));
				Assert.That(root.transform.parent, Is.SameAs(parent.transform));
				Assert.That(selected.transform.position, Is.EqualTo(worldPosition));
				Assert.That(Quaternion.Angle(selected.transform.rotation, worldRotation), Is.LessThan(0.001f));
				Assert.That(selected.GetComponent<UnityEngine.Collider>().enabled, Is.False);
				Assert.That(selected.GetComponent<SpringHingeComponent>(), Is.Not.Null);
				Assert.That(selected.GetComponent<SpringHingeColliderComponent>(), Is.Not.Null);
				Assert.That(bracket.transform.parent, Is.SameAs(parent.transform));
				Assert.That(SpringHingeAuthoring.Validate(root.GetComponent<SpringHingeComponent>(),
					root.GetComponent<SpringHingeColliderComponent>()), Is.Empty);
			} finally {
				if (root) {
					Object.DestroyImmediate(root);
				}
				Object.DestroyImmediate(bracket);
				Object.DestroyImmediate(parent);
			}
		}

		[Test]
		public void ColliderUsesHingeAxisAndCachesRestRotation()
		{
			var root = new GameObject("Spring Hinge");
			try {
				var hinge = root.AddComponent<SpringHingeComponent>();
				hinge.HingeAxis = Vector3.forward;
				root.transform.localRotation = Quaternion.Euler(0f, 12f, 0f);
				var proxy = root.AddComponent<SpringHingeColliderComponent>();
				proxy.CaptureInitialPose();

				proxy.ApplyAngle(math.PI / 2f);
				var expected = Quaternion.Euler(0f, 12f, 0f)
				               * Quaternion.AngleAxis(90f, Vector3.forward);
				Assert.That(Quaternion.Angle(root.transform.localRotation, expected),
					Is.LessThan(0.001f));
			} finally {
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void ColliderVisibilityRoundTripsThroughPackage()
		{
			var root = new GameObject("Spring Hinge");
			try {
				root.AddComponent<SpringHingeComponent>();
				var proxy = root.AddComponent<SpringHingeColliderComponent>();
				proxy.ShowColliderMesh = true;

				var bytes = proxy.Pack();
				proxy.ShowColliderMesh = false;
				proxy.Unpack(bytes);

				Assert.That(proxy.ShowColliderMesh, Is.True);
			} finally {
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void ValidationReportsUnsupportedAndDuplicateOwnedMagnets()
		{
			var root = SpringHingeAuthoring.CreateBashToy();
			var secondObject = new GameObject("Second Magnet");
			try {
				secondObject.transform.SetParent(root.transform, false);
				var second = secondObject.AddComponent<MagnetComponent>();
				second.CoupleToParentHinge = true;
				second.MagnetType = MagnetType.Playfield;
				second.ForceProfile = MagnetForceProfile.VpxCompatible;

				var issues = SpringHingeAuthoring.Validate(
					root.GetComponent<SpringHingeComponent>(),
					root.GetComponent<SpringHingeColliderComponent>());

				Assert.That(issues.Any(issue => issue.Contains("Spatial")), Is.True);
				Assert.That(issues.Any(issue => issue.Contains("Only one")), Is.True);
			} finally {
				Object.DestroyImmediate(root);
			}
		}
	}
}
