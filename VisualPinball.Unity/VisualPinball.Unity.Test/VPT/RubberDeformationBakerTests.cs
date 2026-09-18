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
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Unity.Editor;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Test
{
	public class RubberDeformationBakerTests
	{
		// Plan section 12: thickness 8 units, installed length 150 units, density 1100,
		// installed stretch 1.10, damping ratio 0.08.
		private const float SpanThickness = 8f;
		private const float SpanLength = 150f;
		private const float SpanStretch = 1.10f;

		#region Units

		[Test]
		public void ShouldExposeThePlanUnitConstants()
		{
			Assert.That(RubberDeformationBaker.VpxPerMeter, Is.EqualTo(1852.71f).Within(1e-2f));
			Assert.That(RubberDeformationBaker.StepTimeS, Is.EqualTo(0.01f).Within(1e-9f));
			Assert.That(RubberDeformationBaker.MassUnitKg, Is.EqualTo(0.08f).Within(1e-9f));
		}

		[Test]
		public void ShouldConvertBetweenSiAndVpeUnits()
		{
			Assert.That(RubberDeformationBaker.MassToVpe(0.08f), Is.EqualTo(1f).Within(1e-6f));
			Assert.That(RubberDeformationBaker.StiffnessToVpe(1f),
				Is.EqualTo(1e-4f / 0.08f).Within(1e-9f));
			Assert.That(RubberDeformationBaker.DampingToVpe(1f),
				Is.EqualTo(0.01f / 0.08f).Within(1e-9f));
			Assert.That(RubberDeformationBaker.ForceToVpe(1f),
				Is.EqualTo(1852.71f * 1e-4f / 0.08f).Within(1e-5f));
			Assert.That(RubberDeformationBaker.ForceToNewtons(RubberDeformationBaker.ForceToVpe(3.5f)),
				Is.EqualTo(3.5f).Within(1e-4f));
			Assert.That(RubberDeformationBaker.StressToPascals(RubberDeformationBaker.StressToVpe(56000f)),
				Is.EqualTo(56000f).Within(1f));
			Assert.That(RubberDeformationBaker.LengthToVpx(RubberDeformationBaker.LengthToMeters(150f)),
				Is.EqualTo(150f).Within(1e-3f));
		}

		[Test]
		public void ShouldConvertTensionConsistentlyThroughStressAndArea()
		{
			// sigma_vpe * A0_vpx must equal ForceToVpe(sigma_Pa * A0_m2).
			var areaVpx = RubberDeformationBaker.CrossSectionAreaVpx(SpanThickness);
			var areaM2 = math.PI * math.pow(
				RubberDeformationBaker.LengthToMeters(SpanThickness) * 0.5f, 2f);
			var tensionVpe = RubberDeformationBaker.StressToVpe(56000f) * areaVpx;
			Assert.That(RubberDeformationBaker.ForceToNewtons(tensionVpe),
				Is.EqualTo(56000f * areaM2).Within(1e-4f));
		}

		#endregion

		#region Material compilation

		[Test]
		public void ShouldCompileTheDefaultCurveWithinTheTableError()
		{
			var asset = DefaultMaterial();
			try {
				Assert.That(RubberDeformationBaker.TryCompileMaterial(asset, SpanThickness,
					out var material, out var error), Is.True, error);
				Assert.That(material.CompilerVersion,
					Is.EqualTo(RubberDeformationBaker.CompilerVersion));
				Assert.That(material.StressLut,
					Has.Length.EqualTo(RubberCompiledDeformationMaterial.LutSampleCount));
				Assert.That(material.TangentStiffnessLut,
					Has.Length.EqualTo(RubberCompiledDeformationMaterial.LutSampleCount));
				Assert.That(material.IsPopulated, Is.True);

				var megapascals = RubberDeformationBaker.StressToPascals(
					material.SampleStress(SpanStretch)) * 1e-6f;
				Assert.That(math.abs(megapascals - 0.056f) / 0.056f,
					Is.LessThanOrEqualTo(0.0012f),
					$"128-sample interpolation gave {megapascals} MPa");
			} finally {
				Object.DestroyImmediate(asset);
			}
		}

		[Test]
		public void ShouldProduceTheSectionTwelveCoefficientsForTheDefaultCurve()
		{
			var asset = DefaultMaterial();
			try {
				Assert.That(RubberDeformationBaker.TryCompileMaterial(asset, SpanThickness,
					out var material, out var error), Is.True, error);
				Coefficients(material, out var massGrams, out var stiffness, out var frequency,
					out var tensionNewtons);

				Assert.That(tensionNewtons, Is.EqualTo(0.82f).Within(0.01f));
				Assert.That(massGrams, Is.EqualTo(0.593f).Within(0.002f));
				Assert.That(stiffness, Is.EqualTo(50.0f).Within(0.2f));
				Assert.That(frequency, Is.EqualTo(46.2f).Within(0.15f));
			} finally {
				Object.DestroyImmediate(asset);
			}
		}

		[Test]
		public void ShouldProduceTheSectionTwelveCoefficientsForTheLinearRamp()
		{
			var asset = DefaultMaterial();
			asset.NominalStressMpaByStretchRatio = AnimationCurve.Linear(1f, 0f, 2f, 2f);
			try {
				Assert.That(RubberDeformationBaker.TryCompileMaterial(asset, SpanThickness,
					out var material, out var error), Is.True, error);
				Coefficients(material, out var massGrams, out var stiffness, out var frequency,
					out var tensionNewtons);

				Assert.That(tensionNewtons, Is.EqualTo(2.9f).Within(0.05f));
				Assert.That(massGrams, Is.EqualTo(0.593f).Within(0.002f));
				Assert.That(stiffness, Is.EqualTo(178.5f).Within(0.5f));
				Assert.That(frequency, Is.EqualTo(87.3f).Within(0.2f));
			} finally {
				Object.DestroyImmediate(asset);
			}
		}

		[Test]
		public void ShouldConserveModalMassAcrossStretch()
		{
			var asset = DefaultMaterial();
			try {
				RubberDeformationBaker.TryCompileMaterial(asset, SpanThickness,
					out var material, out _);
				// Doubling the installed length at the same stretch doubles the unloaded
				// length, so the modal mass scales with the unloaded length only.
				var loose = material.LinearDensity * (SpanLength / 1.05f) * 0.5f;
				var tight = material.LinearDensity * (SpanLength / 1.30f) * 0.5f;
				Assert.That(loose, Is.GreaterThan(tight));
				Assert.That(loose * 1.05f, Is.EqualTo(tight * 1.30f).Within(1e-6f));
			} finally {
				Object.DestroyImmediate(asset);
			}
		}

		[Test]
		public void ShouldRejectMaterialsOutsideTheModelDomain()
		{
			Assert.That(RubberDeformationBaker.TryCompileMaterial(null, SpanThickness,
				out _, out var error), Is.False);
			Assert.That(error, Does.Contain("No deformation material"));

			var asset = DefaultMaterial();
			try {
				asset.DensityKgPerCubicMeter = 0f;
				Assert.That(RubberDeformationBaker.TryCompileMaterial(asset, SpanThickness,
					out _, out error), Is.False);
				Assert.That(error, Does.Contain("density"));

				asset.DensityKgPerCubicMeter = 1100f;
				asset.ModalDampingRatio = 0f;
				Assert.That(RubberDeformationBaker.TryCompileMaterial(asset, SpanThickness,
					out _, out error), Is.False);
				Assert.That(error, Does.Contain("damping"));

				asset.ModalDampingRatio = 1f;
				Assert.That(RubberDeformationBaker.TryCompileMaterial(asset, SpanThickness,
					out _, out error), Is.False);
				Assert.That(error, Does.Contain("damping"));

				asset.ModalDampingRatio = 0.08f;
				asset.MaximumStretchRatio = 1f;
				Assert.That(RubberDeformationBaker.TryCompileMaterial(asset, SpanThickness,
					out _, out error), Is.False);
				Assert.That(error, Does.Contain("maximum stretch"));

				asset.MaximumStretchRatio = 2f;
				Assert.That(RubberDeformationBaker.TryCompileMaterial(asset, 0f,
					out _, out error), Is.False);
				Assert.That(error, Does.Contain("thickness"));
			} finally {
				Object.DestroyImmediate(asset);
			}
		}

		#endregion

		#region Inward sign

		[Test]
		public void ShouldOrientInwardSignOnASquareLoop()
		{
			var counterClockwise = SquareLoop(false);
			var clockwise = SquareLoop(true);

			Assert.That(RubberDeformationBaker.ComputeSignedArea(counterClockwise, 4f),
				Is.EqualTo(10000f).Within(1f));
			Assert.That(RubberDeformationBaker.ComputeInwardSign(counterClockwise, 4f),
				Is.EqualTo((sbyte)1));
			Assert.That(RubberDeformationBaker.ComputeSignedArea(clockwise, 4f),
				Is.EqualTo(-10000f).Within(1f));
			Assert.That(RubberDeformationBaker.ComputeInwardSign(clockwise, 4f),
				Is.EqualTo((sbyte)-1));
		}

		[Test]
		public void ShouldOrientInwardSignOnATwoGuideRoundedLoop()
		{
			// Roundness one makes the solver reject this pair, so the highest
			// roundness the two-guide hull accepts here stands in for it. The shared
			// anchor case of a fully rounded loop is covered by the two free spans of
			// ShouldOrientInwardSignOnATwoAnchorLoop.
			var solved = RubberCircleHullSolver.Solve(new[] {
				new RubberGuideCircle(new float2(-40f, 0f), 12f, 0),
				new RubberGuideCircle(new float2(50f, 10f), 20f, 1),
			}, 4f, RubberAutofit.SolverEpsilonVpx, 0.6f);
			Assert.That(solved.IsValid, Is.True, solved.Error);

			var area = RubberDeformationBaker.ComputeSignedArea(solved.Elements, 4f);
			var reference = ReferenceSignedArea(solved.Elements);
			Assert.That(math.sign(area), Is.EqualTo(math.sign(reference)));
			Assert.That(area, Is.EqualTo(reference).Within(math.abs(reference) * 0.02f));
			Assert.That(RubberDeformationBaker.ComputeInwardSign(solved.Elements, 4f),
				Is.EqualTo(area < 0f ? (sbyte)-1 : (sbyte)1));

			var mirrored = solved.Elements.Select(Mirror).ToArray();
			Assert.That(RubberDeformationBaker.ComputeSignedArea(mirrored, 4f),
				Is.EqualTo(-area).Within(math.abs(area) * 0.02f));
			Assert.That(RubberDeformationBaker.ComputeInwardSign(mirrored, 4f),
				Is.Not.EqualTo(RubberDeformationBaker.ComputeInwardSign(solved.Elements, 4f)));
		}

		[Test]
		public void ShouldOrientInwardSignOnATwoAnchorLoop()
		{
			// The fully rounded case: two free spans meeting at two shared anchors with
			// no supported arc between them.
			var loop = TwoAnchorLoop(false);
			var mirrored = TwoAnchorLoop(true);

			var area = RubberDeformationBaker.ComputeSignedArea(loop, 4f);
			Assert.That(area, Is.GreaterThan(0f));
			Assert.That(RubberDeformationBaker.ComputeInwardSign(loop, 4f),
				Is.EqualTo((sbyte)1));
			Assert.That(RubberDeformationBaker.ComputeSignedArea(mirrored, 4f),
				Is.EqualTo(-area).Within(math.abs(area) * 1e-3f));
			Assert.That(RubberDeformationBaker.ComputeInwardSign(mirrored, 4f),
				Is.EqualTo((sbyte)-1));
		}

		#endregion

		#region Tessellation certificate

		[Test]
		public void ShouldCertifyAStraightSpanAtThePlanResolutions()
		{
			var span = StraightSpan(new float2(-75f, 0f), new float2(75f, 0f));

			Assert.That(RubberDeformationBaker.TryCertifySegmentCount(span, 40f,
				out var atForty, out var error), Is.True, error);
			Assert.That(atForty, Is.GreaterThanOrEqualTo(16));
			Assert.That(atForty, Is.LessThanOrEqualTo(RubberDeformationBaker.MaximumSegmentCount));
			Assert.That(atForty % RubberDeformationBaker.SegmentCountStep, Is.Zero);

			Assert.That(RubberDeformationBaker.TryCertifySegmentCount(span, 80f,
				out var atEighty, out error), Is.True, error);
			Assert.That(atEighty, Is.GreaterThanOrEqualTo(20));
			Assert.That(atEighty, Is.GreaterThanOrEqualTo(atForty));
		}

		[Test]
		public void ShouldRefuseToCertifyADegenerateSpan()
		{
			// The hodograph vanishes exactly at t = 0.5: the tangent speed collapses and
			// the curvature bound cannot be converted to arc length.
			var span = new RubberPathElement {
				Type = RubberPathElementType.FreeSpan,
				Start = new float2(0f, 0f),
				Control1 = new float2(100f, 0f),
				Control2 = new float2(0f, 0f),
				End = new float2(100f, 0f),
			};
			span.Length = RubberPathMath.FreeSpanLength(span);

			Assert.That(RubberDeformationBaker.TryCertifySegmentCount(span, 40f,
				out var segments, out var error), Is.False);
			Assert.That(segments, Is.Zero);
			Assert.That(error, Does.Contain("degenerate"));
		}

		[Test]
		public void ShouldSpaceChainPointsUniformlyInArcLength()
		{
			var span = CurvedSpan();
			var fractions = RubberDeformationBaker.BuildArcLengthFractions(span, 16);

			Assert.That(fractions, Has.Length.EqualTo(17));
			Assert.That(fractions[0], Is.EqualTo(0f));
			Assert.That(fractions[16], Is.EqualTo(1f));
			var total = span.Length;
			var previous = RubberPathMath.PointAt(span, 0f);
			var previousLength = 0f;
			for (var i = 1; i <= 16; i++) {
				Assert.That(fractions[i], Is.GreaterThan(fractions[i - 1]));
				var length = previousLength;
				var steps = 256;
				for (var k = 1; k <= steps; k++) {
					var t = math.lerp(fractions[i - 1], fractions[i], k / (float)steps);
					var point = RubberPathMath.PointAt(span, t);
					length += math.distance(previous, point);
					previous = point;
				}
				Assert.That(length, Is.EqualTo(total * i / 16f).Within(total * 0.005f));
				previousLength = length;
			}
		}

		[Test]
		public void ShouldLimitDeflectionByTheMaximumStretchRatio()
		{
			var limit = RubberDeformationBaker.StretchLimitedDeflection(SpanLength,
				SpanStretch, 2f);
			Assert.That(limit, Is.GreaterThan(0f));
			var length = RubberDeformationBaker.DeflectedLength(SpanLength, limit);
			Assert.That(length, Is.EqualTo(SpanLength / SpanStretch * 2f).Within(0.05f));
			Assert.That(RubberDeformationBaker.DeflectedLength(SpanLength, 0f),
				Is.EqualTo(SpanLength).Within(1e-3f));
			Assert.That(RubberDeformationBaker.StretchLimitedDeflection(SpanLength, 2f, 2f),
				Is.Zero);
		}

		#endregion

		#region Outcomes

		[Test]
		public void ShouldBakeAGuidedPhysicalRubberAsDeformable()
		{
			using var fixtureScope = new Fixture();
			var result = RubberDeformationBaker.Compute(fixtureScope.Collider);

			Assert.That(result.Outcome, Is.EqualTo(RubberDeformationBakeOutcome.Deformable),
				result.Reason);
			Assert.That(result.Hash, Has.Length.EqualTo(64));
			Assert.That(result.Spans, Is.Not.Empty);
			Assert.That(result.BackedResponses, Is.Not.Empty);
			foreach (var span in result.Spans) {
				Assert.That(span.InstalledStretch, Is.EqualTo(SpanStretch).Within(1e-3f));
				Assert.That(span.ModalMass.x, Is.GreaterThan(0f));
				Assert.That(span.ModalMass.y, Is.EqualTo(span.ModalMass.x).Within(1e-9f));
				Assert.That(span.ModalStiffness.y,
					Is.EqualTo(span.ModalStiffness.x * 4f).Within(span.ModalStiffness.x * 1e-4f));
				Assert.That(span.ModalStiffness.z,
					Is.EqualTo(span.ModalStiffness.x * 9f).Within(span.ModalStiffness.x * 1e-3f));
				Assert.That(span.ModalStiffness.w,
					Is.EqualTo(span.ModalStiffness.x * 16f).Within(span.ModalStiffness.x * 1e-3f));
				Assert.That(span.ModalDamping.y,
					Is.EqualTo(span.ModalDamping.x * 2f).Within(span.ModalDamping.x * 1e-3f));
				Assert.That(span.ModalDamping.x, Is.EqualTo(2f * 0.08f
					* math.sqrt(span.ModalStiffness.x * span.ModalMass.x)).Within(1e-6f));
				Assert.That(span.MaxDeflection, Is.GreaterThan(0f));
				Assert.That(span.MaxDeflection,
					Is.LessThanOrEqualTo(fixtureScope.Collider.DeflectionClearance + 1e-4f));
				Assert.That(span.SegmentCount,
					Is.InRange(RubberDeformationBaker.MinimumSegmentCount,
						RubberDeformationBaker.MaximumSegmentCount));
				Assert.That(span.ArcLengthFractions, Has.Length.EqualTo(span.SegmentCount + 1));
				Assert.That(math.abs(span.InwardSign), Is.EqualTo(1));
				Assert.That(span.SurfaceFriction, Is.EqualTo(0.3f).Within(1e-6f));
			}
		}

		[Test]
		public void ShouldBakeOneBackedResponsePerReferencedBinding()
		{
			using var fixtureScope = new Fixture();
			var result = RubberDeformationBaker.Compute(fixtureScope.Collider);

			var expected = fixtureScope.Rubber.BakedPath
				.SelectMany(element => new[] { element.StartBindingIndex, element.EndBindingIndex })
				.Distinct().OrderBy(index => index).ToArray();
			Assert.That(result.BackedResponses.Select(response => response.BindingIndex).ToArray(),
				Is.EqualTo(expected));
			foreach (var response in result.BackedResponses) {
				Assert.That(response.ElasticityScale, Is.EqualTo(1f));
				Assert.That(response.Material.Elasticity,
					Is.EqualTo(fixtureScope.Collider.PhysicsElasticity));
				Assert.That(response.PathElementIndex, Is.GreaterThanOrEqualTo(-1));
			}
		}

		[Test]
		public void ShouldCarryVelocityLutsAndGuideScaleIntoBackedResponses()
		{
			using var fixtureScope = new Fixture();
			var contactMaterial = ScriptableObject.CreateInstance<PhysicsMaterialAsset>();
			try {
				contactMaterial.Elasticity = 0.7f;
				contactMaterial.Friction = 0.25f;
				contactMaterial.ElasticityOverVelocity = AnimationCurve.Linear(0f, 1f, 64f, 0.2f);
				contactMaterial.FrictionOverVelocity = AnimationCurve.Linear(0f, 0.1f, 127f, 0.9f);
				fixtureScope.Collider.PhysicsMaterial = contactMaterial;
				fixtureScope.Collider.OverwritePhysics = false;
				fixtureScope.Collider.GuideDistanceElasticityEnabled = true;
				fixtureScope.Collider.GuideElasticityScale = 0.4f;

				var result = RubberDeformationBaker.Compute(fixtureScope.Collider);

				foreach (var response in result.BackedResponses) {
					Assert.That(response.Material.UseElasticityOverVelocity, Is.True);
					Assert.That(response.Material.UseFrictionOverVelocity, Is.True);
					Assert.That(response.ElasticityOverVelocityLut, Has.Length.EqualTo(
						RubberBackedResponseDescriptor.VelocityLutSampleCount));
					Assert.That(response.FrictionOverVelocityLut, Has.Length.EqualTo(
						RubberBackedResponseDescriptor.VelocityLutSampleCount));
					Assert.That(response.ElasticityOverVelocityLut[0],
						Is.EqualTo(contactMaterial.ElasticityOverVelocity.Evaluate(0f)).Within(1e-5f));
					Assert.That(response.FrictionOverVelocityLut[10],
						Is.EqualTo(contactMaterial.FrictionOverVelocity.Evaluate(10f)).Within(1e-5f));
					Assert.That(response.Material.Elasticity, Is.EqualTo(0.7f).Within(1e-6f));
					Assert.That(response.ElasticityScale, Is.EqualTo(0.4f).Within(1e-6f));
				}
			} finally {
				Object.DestroyImmediate(contactMaterial);
			}
		}

		[Test]
		public void ShouldFallBackToStaticWhenTheRestLengthIsUnconfigured()
		{
			using var fixtureScope = new Fixture();
			fixtureScope.Rubber.RestLength = 0f;

			var result = RubberDeformationBaker.Compute(fixtureScope.Collider);

			Assert.That(result.Outcome, Is.EqualTo(RubberDeformationBakeOutcome.StaticFallback));
			Assert.That(result.Reason, Does.Contain("rest length"));
			Assert.That(result.Spans, Is.Empty);
			Assert.That(result.BackedResponses, Is.Not.Empty);
		}

		[Test]
		public void ShouldFallBackToStaticWhenTheRubberIsSlack()
		{
			using var fixtureScope = new Fixture();
			fixtureScope.Rubber.RestLength = fixtureScope.FittedLength;

			var result = RubberDeformationBaker.Compute(fixtureScope.Collider);

			Assert.That(result.Outcome, Is.EqualTo(RubberDeformationBakeOutcome.StaticFallback));
			Assert.That(result.Reason, Does.Contain("slack"));
		}

		[Test]
		public void ShouldFallBackToStaticWhenTheRubberIsKinematic()
		{
			using var fixtureScope = new Fixture();
			fixtureScope.Collider._isKinematic = true;

			var result = RubberDeformationBaker.Compute(fixtureScope.Collider);

			Assert.That(result.Outcome, Is.EqualTo(RubberDeformationBakeOutcome.StaticFallback));
			Assert.That(result.Reason, Does.Contain("kinematic"));
		}

		[Test]
		public void ShouldFallBackToStaticWhenTheTessellationCertificateIsUnmet()
		{
			using var fixtureScope = new Fixture(LargeRoundLoop());

			var result = RubberDeformationBaker.Compute(fixtureScope.Collider);

			Assert.That(result.Outcome, Is.EqualTo(RubberDeformationBakeOutcome.StaticFallback),
				result.Reason);
			Assert.That(result.Reason, Does.Contain("chord budget"));
			Assert.That(result.Spans, Is.Empty);
		}

		[Test]
		public void ShouldRejectAMissingDeformationMaterial()
		{
			using var fixtureScope = new Fixture();
			fixtureScope.Collider.RubberPhysicsMaterial = null;

			var result = RubberDeformationBaker.Compute(fixtureScope.Collider);

			Assert.That(result.Outcome, Is.EqualTo(RubberDeformationBakeOutcome.Invalid));
			Assert.That(result.Reason, Does.Contain("No deformation material"));
		}

		[Test]
		public void ShouldRejectAnInstalledStretchAboveTheMaterialMaximum()
		{
			using var fixtureScope = new Fixture();
			fixtureScope.Rubber.RestLength = fixtureScope.FittedLength / 3f;

			var result = RubberDeformationBaker.Compute(fixtureScope.Collider);

			Assert.That(result.Outcome, Is.EqualTo(RubberDeformationBakeOutcome.Invalid));
			Assert.That(result.Reason, Does.Contain("exceeds the material maximum"));
		}

		[Test]
		public void ShouldRejectANonPositiveStressAtTheInstalledStretch()
		{
			using var fixtureScope = new Fixture();
			fixtureScope.Material.NominalStressMpaByStretchRatio = new AnimationCurve(
				new Keyframe(1f, 0f), new Keyframe(1.5f, 0f), new Keyframe(2f, 2f));

			var result = RubberDeformationBaker.Compute(fixtureScope.Collider);

			Assert.That(result.Outcome, Is.EqualTo(RubberDeformationBakeOutcome.Invalid));
			Assert.That(result.Reason, Does.Contain("not positive"));
		}

		[Test]
		public void ShouldRejectAZeroLengthFreeSpan()
		{
			using var fixtureScope = new Fixture(MicroEdgeLoop());

			var result = RubberDeformationBaker.Compute(fixtureScope.Collider);

			Assert.That(result.Outcome, Is.EqualTo(RubberDeformationBakeOutcome.Invalid));
			Assert.That(result.Reason, Does.Contain("zero length"));
		}

		[Test]
		public void ShouldRejectARubberWithoutAUsableGuidedPath()
		{
			var gameObject = new GameObject("Spline Rubber");
			gameObject.SetActive(false);
			var material = DefaultMaterial();
			try {
				gameObject.AddComponent<RubberComponent>();
				var collider = gameObject.AddComponent<RubberColliderComponent>();
				collider.Mode = RubberColliderMode.Physical;
				collider.RubberPhysicsMaterial = material;

				var result = RubberDeformationBaker.Compute(collider);

				Assert.That(result.Outcome, Is.EqualTo(RubberDeformationBakeOutcome.Invalid));
				Assert.That(result.Reason, Does.Contain("guided path"));
			} finally {
				Object.DestroyImmediate(gameObject);
				Object.DestroyImmediate(material);
			}
		}

		#endregion

		#region Hash

		[Test]
		public void ShouldReportAFreshBakeAsCurrentAndAStaleOneAsNot()
		{
			using var fixtureScope = new Fixture();
			Assert.That(RubberDeformationBaker.IsBakeCurrent(fixtureScope.Collider), Is.False);

			var result = RubberDeformationBaker.Bake(fixtureScope.Collider);

			Assert.That(result.Outcome, Is.EqualTo(RubberDeformationBakeOutcome.Deformable),
				result.Reason);
			Assert.That(fixtureScope.Collider.BakeOutcome,
				Is.EqualTo(RubberDeformationBakeOutcome.Deformable));
			Assert.That(fixtureScope.Collider.DeformationBakeVersion,
				Is.EqualTo(RubberDeformationBaker.BakeVersion));
			Assert.That(fixtureScope.Collider.DeformationBakeHash, Has.Length.EqualTo(64));
			Assert.That(RubberDeformationBaker.IsBakeCurrent(fixtureScope.Collider), Is.True);

			fixtureScope.Collider.DeflectionClearance = 42f;
			Assert.That(RubberDeformationBaker.IsBakeCurrent(fixtureScope.Collider), Is.False);
		}

		[Test]
		public void ShouldHashEveryListedInputAndNothingElse()
		{
			using var fixtureScope = new Fixture();
			var collider = fixtureScope.Collider;
			var rubber = fixtureScope.Rubber;
			var material = fixtureScope.Material;
			var baseline = RubberDeformationBaker.ComputeBakeHash(collider);

			Assert.That(RubberDeformationBaker.ComputeBakeHash(collider), Is.EqualTo(baseline));
			Assert.That(baseline, Has.Length.EqualTo(64));
			Assert.That(baseline, Does.Match("^[0-9a-f]{64}$"));

			var changed = new List<string>();
			void AssertChanges(string what, Action mutate, Action restore)
			{
				mutate();
				var mutated = RubberDeformationBaker.ComputeBakeHash(collider);
				Assert.That(mutated, Is.Not.EqualTo(baseline), $"{what} did not change the hash");
				changed.Add(mutated);
				restore();
				Assert.That(RubberDeformationBaker.ComputeBakeHash(collider),
					Is.EqualTo(baseline), $"{what} did not restore the hash");
			}

			var originalPath = rubber.BakedPath.ToArray();
			AssertChanges("baked path", () => {
				var path = originalPath.ToArray();
				path[0].Start += new float2(0.5f, 0f);
				rubber.ApplyGuidedBake(path, null, rubber.BakeFrameToLocal, default, 2);
			}, () => rubber.ApplyGuidedBake(originalPath.ToArray(), null,
				rubber.BakeFrameToLocal, default, 1));

			var originalFrame = rubber.BakeFrameToLocal;
			AssertChanges("bake transform",
				() => rubber.ApplyGuidedBake(originalPath.ToArray(), null,
					Matrix4x4.Translate(new Vector3(0f, 0.01f, 0f)), default, 1),
				() => rubber.ApplyGuidedBake(originalPath.ToArray(), null, originalFrame,
					default, 1));

			var originalThickness = rubber._thickness;
			AssertChanges("thickness", () => rubber._thickness = originalThickness + 1,
				() => rubber._thickness = originalThickness);

			var originalRestLength = rubber.RestLength;
			AssertChanges("rest length", () => rubber.RestLength = originalRestLength * 0.9f,
				() => rubber.RestLength = originalRestLength);

			AssertChanges("deflection clearance", () => collider.DeflectionClearance = 33f,
				() => collider.DeflectionClearance = RubberColliderComponent.DefaultDeflectionClearance);

			AssertChanges("kinematic flag", () => collider._isKinematic = true,
				() => collider._isKinematic = false);

			AssertChanges("deformation density", () => material.DensityKgPerCubicMeter = 1150f,
				() => material.DensityKgPerCubicMeter = 1100f);
			AssertChanges("deformation damping", () => material.ModalDampingRatio = 0.09f,
				() => material.ModalDampingRatio = 0.08f);
			AssertChanges("deformation surface friction",
				() => material.BallSurfaceFriction = 0.35f,
				() => material.BallSurfaceFriction = 0.3f);
			AssertChanges("deformation maximum stretch",
				() => material.MaximumStretchRatio = 2.5f,
				() => material.MaximumStretchRatio = 2f);

			var originalCurve = material.NominalStressMpaByStretchRatio;
			AssertChanges("stress keyframes", () => material.NominalStressMpaByStretchRatio
					= new AnimationCurve(new Keyframe(1f, 0f), new Keyframe(2f, 2.5f)),
				() => material.NominalStressMpaByStretchRatio = originalCurve);
			AssertChanges("stress wrap mode", () => {
				var curve = new AnimationCurve(originalCurve.keys) {
					preWrapMode = WrapMode.Loop,
					postWrapMode = originalCurve.postWrapMode,
				};
				material.NominalStressMpaByStretchRatio = curve;
			}, () => material.NominalStressMpaByStretchRatio = originalCurve);

			AssertChanges("contact override values", () => collider.Elasticity += 0.1f,
				() => collider.Elasticity -= 0.1f);
			AssertChanges("contact overwrite flag", () => collider.OverwritePhysics = true,
				() => collider.OverwritePhysics = false);

			var contactMaterial = ScriptableObject.CreateInstance<PhysicsMaterialAsset>();
			try {
				AssertChanges("contact material asset",
					() => collider.PhysicsMaterial = contactMaterial,
					() => collider.PhysicsMaterial = null);
				collider.PhysicsMaterial = contactMaterial;
				var withAsset = RubberDeformationBaker.ComputeBakeHash(collider);
				contactMaterial.ElasticityOverVelocity = AnimationCurve.Linear(0f, 1f, 64f, 0.2f);
				Assert.That(RubberDeformationBaker.ComputeBakeHash(collider),
					Is.Not.EqualTo(withAsset), "velocity LUT source did not change the hash");
				collider.PhysicsMaterial = null;
			} finally {
				Object.DestroyImmediate(contactMaterial);
			}

			AssertChanges("guide elasticity settings",
				() => collider.GuideDistanceElasticityEnabled = true,
				() => collider.GuideDistanceElasticityEnabled = false);
			AssertChanges("guide elasticity scale", () => collider.GuideElasticityScale = 0.5f,
				() => collider.GuideElasticityScale
					= RubberColliderComponent.DefaultGuideElasticityScale);

			AssertChanges("guide slot support properties", () => {
				var slots = fixtureScope.Guide.Slots.ToArray();
				slots[0].RubberSupportFriction = 0.42f;
				fixtureScope.Guide.Slots = slots;
			}, () => {
				var slots = fixtureScope.Guide.Slots.ToArray();
				slots[0].RubberSupportFriction = 0f;
				fixtureScope.Guide.Slots = slots;
			});

			Assert.That(changed.Distinct().Count(), Is.EqualTo(changed.Count),
				"two different inputs produced the same hash");

			// Inputs that do not affect eligibility or outcome must leave the hash alone.
			collider.HitEvent = !collider.HitEvent;
			collider.ZOffset += 5f;
			collider.ShowColliderMesh = !collider.ShowColliderMesh;
			collider.DeformationEnabled = !collider.DeformationEnabled;
			collider.gameObject.name += " renamed";
			Assert.That(RubberDeformationBaker.ComputeBakeHash(collider), Is.EqualTo(baseline));
		}

		#endregion

		#region Collider stream

		[Test]
		public void ShouldNotChangeTheColliderStreamOfAnUnenabledPhysicalRubber()
		{
			using var fixtureScope = new Fixture();
			fixtureScope.Collider.DeformationEnabled = false;
			var before = Stream(fixtureScope);

			var result = RubberDeformationBaker.Bake(fixtureScope.Collider);
			Assert.That(result.Outcome, Is.EqualTo(RubberDeformationBakeOutcome.Deformable),
				result.Reason);

			var after = Stream(fixtureScope);
			Assert.That(after, Is.EqualTo(before));
			Assert.That(before, Is.Not.Empty);
		}

		private static string[] Stream(Fixture fixtureScope)
		{
			var transforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			var references = new ColliderReference(ref transforms, Allocator.Temp);
			try {
				new TestRubberApi(fixtureScope.RubberObject).GenerateColliders(ref references);
				var lines = new List<string>();
				for (var i = 0; i < references.SweptCircleColliders.Length; i++) {
					var collider = references.SweptCircleColliders[i];
					lines.Add($"{collider.V1} {collider.V2} {collider.CordRadius} {collider.ElasticityScale}");
				}
				Assert.That(references.Count, Is.EqualTo(lines.Count));
				return lines.ToArray();
			} finally {
				references.Dispose();
				transforms.Dispose();
			}
		}

		#endregion

		#region Fixtures

		private sealed class Fixture : IDisposable
		{
			internal readonly GameObject RubberObject;
			internal readonly GameObject GuideObject;
			internal readonly RubberComponent Rubber;
			internal readonly RubberColliderComponent Collider;
			internal readonly RubberGuideComponent Guide;
			internal readonly RubberPhysicsMaterialAsset Material;
			internal readonly float FittedLength;

			internal Fixture(RubberPathElement[] path = null)
			{
				path ??= TwoGuidePath();
				FittedLength = path.Sum(element => element.Length);

				GuideObject = new GameObject("Guide");
				Guide = GuideObject.AddComponent<RubberGuideComponent>();

				RubberObject = new GameObject("Rubber");
				RubberObject.SetActive(false);
				Rubber = RubberObject.AddComponent<RubberComponent>();
				Rubber._thickness = (int)SpanThickness;
				var bindingCount = path.Max(element => math.max(element.StartBindingIndex,
					element.EndBindingIndex)) + 1;
				Rubber.SetGuideBindings(Enumerable.Range(0, math.max(1, bindingCount))
					.Select(_ => new RubberGuideBinding(Guide, Guide.Slots[0].Id)).ToArray());
				Rubber.ApplyGuidedBake(path, null, Matrix4x4.identity, default, 1);
				Rubber.RestLength = FittedLength / SpanStretch;

				Material = DefaultMaterial();
				Collider = RubberObject.AddComponent<RubberColliderComponent>();
				Collider.Mode = RubberColliderMode.Physical;
				Collider.DeformationEnabled = true;
				Collider.RubberPhysicsMaterial = Material;
				Collider.Elasticity = 0.8f;
				Collider.Friction = 0.3f;

				Assert.That(Collider.CanUsePhysical, Is.True, "the fixture path is not usable");
			}

			public void Dispose()
			{
				Object.DestroyImmediate(RubberObject);
				Object.DestroyImmediate(GuideObject);
				Object.DestroyImmediate(Material);
			}
		}

		private sealed class TestRubberApi : RubberApi
		{
			internal TestRubberApi(GameObject go) : base(go, null, null)
			{
			}

			internal void GenerateColliders(ref ColliderReference references)
				=> CreateColliders(ref references, float4x4.identity, 0f);
		}

		private static RubberPhysicsMaterialAsset DefaultMaterial()
			=> ScriptableObject.CreateInstance<RubberPhysicsMaterialAsset>();

		private static void Coefficients(RubberCompiledDeformationMaterial material,
			out float massGrams, out float stiffness, out float frequency,
			out float tensionNewtons)
		{
			var area = RubberDeformationBaker.CrossSectionAreaVpx(SpanThickness);
			var tension = material.SampleStress(SpanStretch) * area;
			var unloadedLength = SpanLength / SpanStretch;
			var modalMass = material.LinearDensity * unloadedLength * 0.5f;
			var modalStiffness = tension * math.PI * math.PI / (2f * SpanLength);

			tensionNewtons = RubberDeformationBaker.ForceToNewtons(tension);
			massGrams = modalMass * RubberDeformationBaker.MassUnitKg * 1000f;
			stiffness = modalStiffness * RubberDeformationBaker.MassUnitKg
				/ (RubberDeformationBaker.StepTimeS * RubberDeformationBaker.StepTimeS);
			frequency = RubberDeformationBaker.FrequencyHz(modalStiffness, modalMass);
		}

		private static RubberPathElement[] TwoGuidePath()
		{
			var solved = RubberCircleHullSolver.Solve(new[] {
				new RubberGuideCircle(new float2(-40f, 0f), 12f, 0),
				new RubberGuideCircle(new float2(50f, 10f), 20f, 1),
			}, SpanThickness * 0.5f);
			Assert.That(solved.IsValid, Is.True, solved.Error);
			return solved.Elements;
		}

		/// <summary>
		/// Two cubic half circles of radius 640, so the rest curvature alone blows the
		/// 0.05 unit chord budget at 64 segments.
		/// </summary>
		private static RubberPathElement[] LargeRoundLoop()
		{
			const float radius = 640f;
			const float handle = radius * 4f / 3f;
			return Close(new[] {
				new RubberPathElement {
					Type = RubberPathElementType.FreeSpan,
					Start = new float2(radius, 0f),
					Control1 = new float2(radius, handle),
					Control2 = new float2(-radius, handle),
					End = new float2(-radius, 0f),
				},
				new RubberPathElement {
					Type = RubberPathElementType.FreeSpan,
					Start = new float2(-radius, 0f),
					Control1 = new float2(-radius, -handle),
					Control2 = new float2(radius, -handle),
					End = new float2(radius, 0f),
				},
			});
		}

		/// <summary>
		/// A triangle whose fourth edge is half a thousandth of a unit long: long enough
		/// to pass path validation, short enough to trip the zero-length span guard.
		/// </summary>
		private static RubberPathElement[] MicroEdgeLoop()
		{
			return Close(new[] {
				StraightSpan(new float2(0f, 0f), new float2(200f, 0f)),
				StraightSpan(new float2(200f, 0f), new float2(100f, 150f)),
				StraightSpan(new float2(100f, 150f), new float2(0f, 0.0005f)),
				StraightSpan(new float2(0f, 0.0005f), new float2(0f, 0f)),
			});
		}

		/// <summary>
		/// Two cubic half circles of radius 100 meeting at two shared anchors.
		/// </summary>
		private static RubberPathElement[] TwoAnchorLoop(bool mirrored)
		{
			const float radius = 100f;
			const float handle = radius * 4f / 3f;
			var sign = mirrored ? -1f : 1f;
			return Close(new[] {
				new RubberPathElement {
					Type = RubberPathElementType.FreeSpan,
					Start = new float2(radius * sign, 0f),
					Control1 = new float2(radius * sign, handle),
					Control2 = new float2(-radius * sign, handle),
					End = new float2(-radius * sign, 0f),
				},
				new RubberPathElement {
					Type = RubberPathElementType.FreeSpan,
					Start = new float2(-radius * sign, 0f),
					Control1 = new float2(-radius * sign, -handle),
					Control2 = new float2(radius * sign, -handle),
					End = new float2(radius * sign, 0f),
				},
			});
		}

		private static RubberPathElement[] SquareLoop(bool clockwise)
		{
			var corners = new[] {
				new float2(0f, 0f), new float2(100f, 0f),
				new float2(100f, 100f), new float2(0f, 100f),
			};
			if (clockwise) {
				corners = corners.Reverse().ToArray();
			}
			var elements = new RubberPathElement[4];
			for (var i = 0; i < 4; i++) {
				elements[i] = StraightSpan(corners[i], corners[(i + 1) % 4]);
			}
			return Close(elements);
		}

		private static RubberPathElement[] Close(RubberPathElement[] elements)
		{
			var distance = 0f;
			for (var i = 0; i < elements.Length; i++) {
				elements[i].Length = RubberPathMath.FreeSpanLength(elements[i]);
				elements[i].StartDistance = distance;
				distance += elements[i].Length;
			}
			return elements;
		}

		private static RubberPathElement StraightSpan(float2 start, float2 end)
		{
			var span = new RubberPathElement {
				Type = RubberPathElementType.FreeSpan,
				Start = start,
				End = end,
				Control1 = math.lerp(start, end, 1f / 3f),
				Control2 = math.lerp(start, end, 2f / 3f),
			};
			span.Length = RubberPathMath.FreeSpanLength(span);
			return span;
		}

		private static RubberPathElement CurvedSpan()
		{
			var span = new RubberPathElement {
				Type = RubberPathElementType.FreeSpan,
				Start = new float2(-100f, 0f),
				End = new float2(100f, 0f),
				Control1 = new float2(-60f, 60f),
				Control2 = new float2(60f, 40f),
			};
			span.Length = RubberPathMath.FreeSpanLength(span);
			return span;
		}

		private static RubberPathElement Mirror(RubberPathElement element)
		{
			var mirrored = element;
			mirrored.Start = new float2(-element.Start.x, element.Start.y);
			mirrored.End = new float2(-element.End.x, element.End.y);
			mirrored.Control1 = new float2(-element.Control1.x, element.Control1.y);
			mirrored.Control2 = new float2(-element.Control2.x, element.Control2.y);
			mirrored.Center = new float2(-element.Center.x, element.Center.y);
			mirrored.StartAngleRad = math.PI - element.StartAngleRad;
			mirrored.SweepAngleRad = -element.SweepAngleRad;
			return mirrored;
		}

		/// <summary>
		/// Independent shoelace over a dense sampling of the path, used to check the
		/// baker's coarser element-wise sampling.
		/// </summary>
		private static float ReferenceSignedArea(IReadOnlyList<RubberPathElement> path)
		{
			var area = 0.0;
			foreach (var element in path) {
				const int samples = 512;
				var previous = RubberPathMath.PointAt(element, 0f);
				for (var i = 1; i <= samples; i++) {
					var next = RubberPathMath.PointAt(element, i / (float)samples);
					area += (double)previous.x * next.y - (double)next.x * previous.y;
					previous = next;
				}
			}
			return (float)(area * 0.5);
		}

		#endregion
	}
}
