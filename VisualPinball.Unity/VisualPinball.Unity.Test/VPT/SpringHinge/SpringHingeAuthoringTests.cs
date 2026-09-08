// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System.Collections.Generic;
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
				var animation = root.GetComponent<SpringHingeAnimationComponent>();
				var magnet = root.GetComponentInChildren<MagnetComponent>();

				Assert.That(hinge, Is.Not.Null);
				Assert.That(proxy, Is.Not.Null);
				Assert.That(animation, Is.Not.Null);
				Assert.That(animation.gameObject, Is.SameAs(root));
				Assert.That(animation._emitter, Is.SameAs(hinge));
				Assert.That(magnet.CoupleToParentHinge, Is.True);
				Assert.That(magnet.MagnetType, Is.EqualTo(MagnetType.Spatial));
				Assert.That(magnet.ForceProfile, Is.EqualTo(MagnetForceProfile.Physical));
				Assert.That(magnet.GetComponentInParent<SpringHingeAnimationComponent>(), Is.SameAs(animation));
				Assert.That(root.GetComponentInChildren<UnityEngine.Collider>(), Is.Null);
				Assert.That(SpringHingeAuthoring.Validate(hinge, proxy), Is.Empty);
			} finally {
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void VisualBoundsFitMassAndProxyInMillimeters()
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
				Assert.That(selected.GetComponent<SpringHingeAnimationComponent>(), Is.Not.Null);
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
		public void SameObjectTransformDriverCachesRestRotationAndRoundTripsReferences()
		{
			var root = new GameObject("Spring Hinge");
			try {
				var hinge = root.AddComponent<SpringHingeComponent>();
				root.transform.localRotation = Quaternion.Euler(0f, 12f, 0f);
				var animation = root.AddComponent<SpringHingeAnimationComponent>();
				animation._emitter = hinge;
				animation.RotationAxis = Vector3.forward;
				animation.CaptureInitialPose();

				animation.ApplyAngle(math.PI / 2f);
				var expected = Quaternion.Euler(0f, 12f, 0f)
				               * Quaternion.AngleAxis(90f, Vector3.forward);
				Assert.That(Quaternion.Angle(root.transform.localRotation, expected),
					Is.LessThan(0.001f));

				var refs = new PackagedRefs(root.transform);
				refs.SetNodeIdsForWrite(new Dictionary<Transform, string> {
					{ root.transform, "hinge" }
				});
				var values = animation.Pack();
				var references = animation.PackReferences(root.transform, refs, null);
				animation.RotationAxis = Vector3.right;
				animation._emitter = null;
				animation.Unpack(values);
				refs.SetNodeIdsForRead(new Dictionary<string, Transform> {
					{ "hinge", root.transform }
				});
				animation.UnpackReferences(references, root.transform, refs, null);

				Assert.That(animation.RotationAxis, Is.EqualTo(Vector3.forward));
				Assert.That(animation._emitter, Is.SameAs(hinge));
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
