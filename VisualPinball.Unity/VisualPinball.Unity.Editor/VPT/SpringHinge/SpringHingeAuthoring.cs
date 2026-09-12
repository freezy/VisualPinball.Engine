// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	public static class SpringHingeAuthoring
	{
		private const float StandardBallRadiusVpx = 25f;
		private const float BashMagnetStrength = 40000f;
		private const float BashMagnetHoldStiffness = 4f;
		private const float BashMagnetHoldDamping = 4f;
		private const float BashMagnetMaxHoldForce = 50f;

		[MenuItem("GameObject/Pinball/Add Spring Hinge", false, 12)]
		private static void AddSpringHingeMenu(MenuCommand command)
		{
			var selected = Selection.transforms;
			var root = AddSpringHinge(selected, Selection.activeTransform);
			Selection.activeGameObject = root;
		}

		[MenuItem("GameObject/Pinball/Add Spring Hinge", true)]
		private static bool ValidateAddSpringHingeMenu()
			=> Selection.transforms.Length > 0;

		[MenuItem("GameObject/Pinball/Spring Hinge Bash Toy", false, 13)]
		private static void CreateBashToyMenu(MenuCommand command)
		{
			var context = command.context as GameObject;
			var root = CreateBashToy(context ? context.transform : null);
			Selection.activeGameObject = root;
		}

		public static GameObject CreateBashToy(Transform parent = null)
		{
			var root = new GameObject("Spring Hinge Bash Toy");
			Undo.RegisterCreatedObjectUndo(root, "Create Spring Hinge Bash Toy");
			if (parent) {
				GameObjectUtility.SetParentAndAlign(root, parent.gameObject);
			}

			var hinge = Undo.AddComponent<SpringHingeComponent>(root);
			var proxy = Undo.AddComponent<SpringHingeColliderComponent>(root);

			var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
			visual.name = "Toy Visual";
			visual.transform.SetParent(root.transform, false);
			visual.transform.localPosition = Vector3.down * Physics.ScaleToWorld(50f);
			visual.transform.localScale = new Vector3(
				Physics.ScaleToWorld(50f), Physics.ScaleToWorld(100f), Physics.ScaleToWorld(20f));
			var unityCollider = visual.GetComponent<UnityEngine.Collider>();
			if (unityCollider) {
				UnityEngine.Object.DestroyImmediate(unityCollider);
			}

			var magnetObject = new GameObject("Owned Magnet");
			magnetObject.transform.SetParent(root.transform, false);
			magnetObject.transform.localPosition = Vector3.down * Physics.ScaleToWorld(100f);
			var magnet = Undo.AddComponent<MagnetComponent>(magnetObject);

			ApplyBashPreset(hinge, proxy, magnet);
			EditorUtility.SetDirty(root);
			return root;
		}

		public static GameObject AddSpringHinge(IReadOnlyList<Transform> visualParts,
			Transform activeVisual)
		{
			if (visualParts == null || visualParts.Count == 0) {
				return null;
			}

			activeVisual = activeVisual ? activeVisual : visualParts[0];
			var rotatingObject = activeVisual.gameObject;
			var hinge = rotatingObject.GetComponent<SpringHingeComponent>()
			            ?? Undo.AddComponent<SpringHingeComponent>(rotatingObject);
			var proxy = rotatingObject.GetComponent<SpringHingeColliderComponent>()
			            ?? Undo.AddComponent<SpringHingeColliderComponent>(rotatingObject);
			foreach (var visualPart in visualParts) {
				if (!visualPart || visualPart == activeVisual || visualPart.IsChildOf(activeVisual)
				    || IsAncestorSelected(visualPart, visualParts)) {
					continue;
				}
				if (activeVisual.IsChildOf(visualPart)) {
					Debug.LogWarning($"Cannot add selected ancestor '{visualPart.name}' below spring hinge '{activeVisual.name}'. Select the common rotating root as the active object.", activeVisual);
					continue;
				}
				Undo.SetTransformParent(visualPart, activeVisual, "Add Visual To Spring Hinge");
			}
			DisableIndependentColliders(activeVisual);

			var ownedMagnets = rotatingObject.GetComponentsInChildren<MagnetComponent>(true);
			var magnet = ownedMagnets.Length == 1 ? ownedMagnets[0] : null;
			ApplyBashPreset(hinge, proxy, magnet);
			FitFromVisuals(hinge, proxy);
			EditorUtility.SetDirty(rotatingObject);
			return rotatingObject;
		}

		private static bool IsAncestorSelected(Transform candidate,
			IReadOnlyList<Transform> selected)
		{
			for (var parent = candidate.parent; parent; parent = parent.parent) {
				for (var i = 0; i < selected.Count; i++) {
					if (selected[i] == parent) {
						return true;
					}
				}
			}
			return false;
		}

		private static void DisableIndependentColliders(Transform visualPart)
		{
			foreach (var collider in visualPart.GetComponentsInChildren<UnityEngine.Collider>(true)) {
				Undo.RecordObject(collider, "Disable Independent Collider");
				collider.enabled = false;
			}
			foreach (var behaviour in visualPart.GetComponentsInChildren<MonoBehaviour>(true)) {
				if (behaviour is not ICollidableComponent
				    || behaviour is MagnetComponent
				    || behaviour is SpringHingeColliderComponent) {
					continue;
				}
				Undo.RecordObject(behaviour, "Disable Independent Collider");
				behaviour.enabled = false;
			}
		}

		public static void ApplyBashPreset(SpringHingeComponent hinge,
			SpringHingeColliderComponent proxy, MagnetComponent magnet = null)
		{
			hinge.HingeAxis = Vector3.right;
			hinge.CentreOfMass = new Vector3(0f, -50f, 0f);
			hinge.ToyMass = 1f;
			hinge.OverrideInertia = false;
			hinge.MassBoxHalfExtents = new Vector3(25f, 50f, 10f);
			hinge.SpringStiffness = 100f;
			hinge.SpringDamping = 5f;
			hinge.EquilibriumAngle = 0f;
			hinge.MinimumAngle = 0f;
			hinge.MaximumAngle = 20f;
			hinge.InitialAngle = 0f;

			proxy.LocalCentre = new Vector3(0f, -50f, 0f);
			proxy.LocalRotation = Vector3.zero;
			proxy.HalfExtents = new Vector3(25f, 50f, 10f);
			proxy.Elasticity = 0.1f;
			proxy.ElasticityFalloff = 0.5f;
			proxy.Friction = 0.3f;
			proxy.HitEvent = true;
			proxy.HitThreshold = 0f;

			if (!magnet) {
				return;
			}
			magnet.MagnetType = MagnetType.Spatial;
			magnet.ForceProfile = MagnetForceProfile.Physical;
			magnet.Radius = MagnetComponent.DefaultInfluenceRadius;
			magnet.Strength = BashMagnetStrength;
			magnet.PoleRadius = MagnetComponent.DefaultPoleRadius;
			magnet.GrabBall = true;
			magnet.GrabRadius = MagnetComponent.DefaultGrabRadius;
			magnet.CoupleToParentHinge = true;
			magnet.HeldBallCentreOffset = Vector3.down * StandardBallRadiusVpx;
			magnet.HoldStiffness = BashMagnetHoldStiffness;
			magnet.HoldDamping = BashMagnetHoldDamping;
			magnet.MaxHoldForce = BashMagnetMaxHoldForce;
			magnet.IsKinematic = false;
		}

		public static bool TryGetVisualBounds(SpringHingeComponent hinge,
			out Vector3 centreVpx, out Vector3 halfExtentsVpx)
		{
			centreVpx = Vector3.zero;
			halfExtentsVpx = Vector3.zero;
			if (!hinge) {
				return false;
			}

			var renderers = hinge.GetComponentsInChildren<Renderer>(true);
			var minimum = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
			var maximum = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
			var found = false;
			foreach (var renderer in renderers) {
				if (!renderer || renderer.hideFlags.HasFlag(HideFlags.DontSave)) {
					continue;
				}
				var bounds = renderer.bounds;
				for (var corner = 0; corner < 8; corner++) {
					var world = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
						(corner & 1) == 0 ? -1f : 1f,
						(corner & 2) == 0 ? -1f : 1f,
						(corner & 4) == 0 ? -1f : 1f));
					var local = hinge.transform.InverseTransformPoint(world);
					minimum = Vector3.Min(minimum, local);
					maximum = Vector3.Max(maximum, local);
					found = true;
				}
			}
			if (!found) {
				return false;
			}
			centreVpx = (minimum + maximum) * (0.5f / Physics.ScaleInv);
			halfExtentsVpx = (maximum - minimum) * (0.5f / Physics.ScaleInv);
			return true;
		}

		public static bool FitFromVisuals(SpringHingeComponent hinge,
			SpringHingeColliderComponent proxy)
		{
			if (!hinge || !proxy || !TryGetVisualBounds(hinge, out var centre, out var halfExtents)) {
				return false;
			}
			hinge.CentreOfMass = centre;
			hinge.MassBoxHalfExtents = halfExtents;
			proxy.LocalCentre = centre;
			proxy.LocalRotation = Vector3.zero;
			proxy.HalfExtents = halfExtents;
			return true;
		}

		public static IReadOnlyList<string> Validate(SpringHingeComponent hinge,
			SpringHingeColliderComponent proxy)
		{
			var issues = new List<string>();
			if (!hinge) {
				issues.Add("A Spring Hinge component is required.");
				return issues;
			}
			if (hinge.HingeAxis.sqrMagnitude < 1e-8f) {
				issues.Add("Hinge Axis must be non-zero.");
			}
			if (hinge.transform.parent && hinge.transform.parent.GetComponentInParent<SpringHingeComponent>()) {
				issues.Add("Nested spring hinges are not supported.");
			}
			if (!HasRigidFrame(hinge.transform)) {
				issues.Add("The spring-hinge frame must have non-zero orthogonal axes; bake shear into the visual mesh before simulation.");
			}
			if (hinge.MinimumAngle >= hinge.MaximumAngle) {
				issues.Add("Minimum Angle must be lower than Maximum Angle.");
			}
			if (hinge.InitialAngle < hinge.MinimumAngle || hinge.InitialAngle > hinge.MaximumAngle) {
				issues.Add("Initial Angle must lie between the hard stops.");
			}
			if (!proxy || proxy.HalfExtents.x <= 0f || proxy.HalfExtents.y <= 0f || proxy.HalfExtents.z <= 0f) {
				issues.Add("The analytic box proxy needs three positive half-extents.");
			}

			if (hinge.GetComponentInChildren<HitTargetAnimationComponent>(true)) {
				issues.Add("Remove hit-target animation from spring-hinge visuals; the hinge is their only animation driver.");
			}

			var ownedMagnets = hinge.GetComponentsInChildren<MagnetComponent>(true);
			var ownedCount = 0;
			foreach (var magnet in ownedMagnets) {
				if (!magnet.CoupleToParentHinge) {
					continue;
				}
				ownedCount++;
				if (magnet.MagnetType != MagnetType.Spatial) {
					issues.Add($"Owned magnet '{magnet.name}' must use Spatial type.");
				}
			}
			if (ownedCount > 1) {
				issues.Add("Only one owned magnet is supported per spring hinge.");
			}
			return issues;
		}

		public static bool TryGetHeldBallCentreGap(SpringHingeComponent hinge,
			SpringHingeColliderComponent proxy, MagnetComponent magnet, out float gap)
		{
			gap = 0f;
			if (!hinge || !proxy || !magnet) {
				return false;
			}
			var target = ToPlayfieldVpx(hinge, magnet.GetHeldBallCentreWorldPosition());
			return TryGetProxyDistance(hinge, proxy, target, StandardBallRadiusVpx,
				out gap, out _, out _);
		}

		public static bool TryGetFittedHeldBallCentreOffset(SpringHingeComponent hinge,
			SpringHingeColliderComponent proxy, MagnetComponent magnet, out Vector3 offset)
		{
			offset = Vector3.zero;
			if (!hinge || !proxy || !magnet) {
				return false;
			}
			var pole = ToPlayfieldVpx(hinge, magnet.transform.position);
			if (!TryGetProxyDistance(hinge, proxy, pole, 0f,
				    out _, out var witness, out var normal)) {
				return false;
			}
			var heldCentre = ToWorld(hinge, witness + normal * StandardBallRadiusVpx);
			offset = Quaternion.Inverse(magnet.transform.rotation)
			         * (heldCentre - magnet.transform.position) / Physics.ScaleInv;
			return true;
		}

		private static bool TryGetProxyDistance(SpringHingeComponent hinge,
			SpringHingeColliderComponent proxy, Vector3 point, float sphereRadius,
			out float separation, out Vector3 witness, out Vector3 normal)
		{
			separation = 0f;
			witness = Vector3.zero;
			normal = Vector3.zero;
			try {
				var collider = SpringHingeColliderGenerator.Create(hinge, proxy,
					new ColliderInfo { ItemId = hinge.ItemId }, 0f);
				var hingeState = hinge.CreateState();
				hingeState.Movement.Angle = 0f;
				var pointVpx = (float3)point;
				var distance = collider.Distance(in hingeState, in pointVpx, sphereRadius);
				separation = distance.Separation;
				witness = distance.Witness;
				normal = distance.Normal;
				return true;
			} catch (InvalidOperationException) {
				return false;
			}
		}

		private static Vector3 ToPlayfieldVpx(SpringHingeComponent hinge, Vector3 worldPoint)
		{
			var playfield = hinge.GetComponentInParent<PlayfieldComponent>();
			return playfield ? worldPoint.TranslateToVpx(playfield.transform) : worldPoint.TranslateToVpx();
		}

		private static Vector3 ToWorld(SpringHingeComponent hinge, Vector3 point)
		{
			var playfield = hinge.GetComponentInParent<PlayfieldComponent>();
			return playfield ? point.TranslateToWorld(playfield.transform) : point.TranslateToWorld();
		}

		private static bool HasRigidFrame(Transform transform)
		{
			var matrix = transform.localToWorldMatrix;
			var x = matrix.GetColumn(0);
			var y = matrix.GetColumn(1);
			var z = matrix.GetColumn(2);
			if (x.sqrMagnitude < 1e-8f || y.sqrMagnitude < 1e-8f || z.sqrMagnitude < 1e-8f) {
				return false;
			}
			x.Normalize();
			y.Normalize();
			z.Normalize();
			return Mathf.Abs(Vector3.Dot(x, y)) < 1e-4f
			       && Mathf.Abs(Vector3.Dot(x, z)) < 1e-4f
			       && Mathf.Abs(Vector3.Dot(y, z)) < 1e-4f;
		}
	}
}
