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
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Math;

namespace VisualPinball.Unity.Test
{
	public class RubberAutofitTests
	{
		[Test]
		public void ShouldRejectEmptyGuideSet()
		{
			var result = RubberCircleHullSolver.Solve(Array.Empty<RubberGuideCircle>(), 4f);

			Assert.That(result.IsValid, Is.False);
			Assert.That(result.Error, Does.Contain("At least one"));
		}

		[Test]
		public void ShouldWrapOneGuideWithFullSupportedArc()
		{
			var result = RubberCircleHullSolver.Solve(new[] {
				new RubberGuideCircle(new float2(10f, 20f), 12f, 3),
			}, 4f);

			Assert.That(result.IsValid, Is.True, result.Error);
			Assert.That(result.Elements, Has.Length.EqualTo(1));
			Assert.That(result.Elements[0].Type, Is.EqualTo(RubberPathElementType.SupportedArc));
			Assert.That(result.Elements[0].Radius, Is.EqualTo(16f).Within(1e-5f));
			Assert.That(result.Elements[0].SweepAngleRad, Is.EqualTo(2f * math.PI).Within(1e-5f));
			Assert.That(result.SupportingBindingIndices, Is.EqualTo(new[] { 3 }));
		}

		[Test]
		public void ShouldWrapTwoUnequalGuidesWithTwoStraightTangents()
		{
			var result = RubberCircleHullSolver.Solve(new[] {
				new RubberGuideCircle(new float2(-40f, 0f), 12f, 0),
				new RubberGuideCircle(new float2(50f, 10f), 20f, 1),
			}, 4f);

			Assert.That(result.IsValid, Is.True, result.Error);
			Assert.That(result.Elements.Count(element => element.Type == RubberPathElementType.FreeSpan),
				Is.EqualTo(2));
			Assert.That(result.Elements.Count(element => element.Type == RubberPathElementType.SupportedArc),
				Is.EqualTo(2));
			Assert.That(result.SupportingBindingIndices, Is.EqualTo(new[] { 0, 1 }));
			AssertClosed(result.Elements);
		}

		[Test]
		public void RoundnessShouldShrinkContactArcsAndCurveFreeSpans()
		{
			var guides = new[] {
				new RubberGuideCircle(new float2(-40f, 0f), 12f, 0),
				new RubberGuideCircle(new float2(50f, 10f), 20f, 1),
			};
			var taut = RubberCircleHullSolver.Solve(guides, 4f);
			var rounded = RubberCircleHullSolver.Solve(guides, 4f,
				RubberAutofit.SolverEpsilonVpx, 0.6f);

			Assert.That(taut.IsValid, Is.True, taut.Error);
			Assert.That(rounded.IsValid, Is.True, rounded.Error);
			var tautSpans = taut.Elements
				.Where(element => element.Type == RubberPathElementType.FreeSpan)
				.OrderBy(element => element.StartBindingIndex)
				.ThenBy(element => element.EndBindingIndex)
				.ToArray();
			var roundedSpans = rounded.Elements
				.Where(element => element.Type == RubberPathElementType.FreeSpan)
				.OrderBy(element => element.StartBindingIndex)
				.ThenBy(element => element.EndBindingIndex)
				.ToArray();
			Assert.That(roundedSpans, Has.Length.EqualTo(tautSpans.Length));
			for (var i = 0; i < roundedSpans.Length; i++) {
				Assert.That(RubberPathMath.IsStraightFreeSpan(roundedSpans[i]), Is.False);
				Assert.That(math.distance(roundedSpans[i].Start, tautSpans[i].Start),
					Is.GreaterThan(1e-3f));
				Assert.That(math.distance(roundedSpans[i].End, tautSpans[i].End),
					Is.GreaterThan(1e-3f));
				AssertGuideContact(roundedSpans[i].Start,
					guides[roundedSpans[i].StartBindingIndex], 4f);
				AssertGuideContact(roundedSpans[i].End,
					guides[roundedSpans[i].EndBindingIndex], 4f);
			}

			var tautArcs = taut.Elements
				.Where(element => element.Type == RubberPathElementType.SupportedArc)
				.OrderBy(element => element.StartBindingIndex)
				.ToArray();
			var roundedArcs = rounded.Elements
				.Where(element => element.Type == RubberPathElementType.SupportedArc)
				.OrderBy(element => element.StartBindingIndex)
				.ToArray();
			for (var i = 0; i < roundedArcs.Length; i++) {
				Assert.That(math.distance(roundedArcs[i].Center, tautArcs[i].Center),
					Is.LessThan(1e-4f));
				Assert.That(roundedArcs[i].Radius,
					Is.EqualTo(tautArcs[i].Radius).Within(1e-4f));
				Assert.That(roundedArcs[i].SweepAngleRad,
					Is.EqualTo(tautArcs[i].SweepAngleRad * (1f - 0.6f * 0.6f))
						.Within(1e-4f));
				Assert.That(roundedArcs[i].SweepAngleRad,
					Is.GreaterThan(tautArcs[i].SweepAngleRad * (1f - 0.6f)));
			}
			AssertClosed(rounded.Elements);
			AssertOutsideGuides(rounded.Elements, guides, 4f);
		}

		[Test]
		public void FullRoundnessShouldKeepOneContactAnchorPerSupportingGuide()
		{
			const float cordRadius = 4f;
			var guides = new[] {
				new RubberGuideCircle(new float2(-50f, 0f), 10f, 0),
				new RubberGuideCircle(new float2(50f, 0f), 10f, 1),
				new RubberGuideCircle(new float2(0f, 20f), 10f, 2),
			};

			var rounded = RubberCircleHullSolver.Solve(guides, cordRadius,
				RubberAutofit.SolverEpsilonVpx, 1f);

			Assert.That(rounded.IsValid, Is.True, rounded.Error);
			Assert.That(rounded.Elements, Has.None.Matches<RubberPathElement>(element =>
				element.Type == RubberPathElementType.SupportedArc));
			Assert.That(rounded.SupportingBindingIndices, Is.EqualTo(new[] { 0, 1, 2 }));
			Assert.That(rounded.Elements, Has.All.Matches<RubberPathElement>(element =>
				!RubberPathMath.IsStraightFreeSpan(element)));
			foreach (var guide in guides) {
				var contacts = new List<float2>();
				foreach (var span in rounded.Elements) {
					if (span.StartBindingIndex == guide.BindingIndex) {
						contacts.Add(span.Start);
					}
					if (span.EndBindingIndex == guide.BindingIndex) {
						contacts.Add(span.End);
					}
				}
				Assert.That(contacts, Has.Count.EqualTo(2));
				Assert.That(math.distance(contacts[0], contacts[1]), Is.LessThan(1e-4f));
				AssertGuideContact(contacts[0], guide, cordRadius);
			}
			AssertClosed(rounded.Elements);
			AssertOutsideGuides(rounded.Elements, guides, cordRadius);
			Assert.That(RubberPathSplineBaker.TryCreateDragPoints(rounded.Elements,
				Matrix4x4.identity, 0.05f, out _, out var deviation, out var error),
				Is.True, error);
			Assert.That(deviation, Is.LessThanOrEqualTo(0.05f));
		}

		[Test]
		public void NearlyStraightTangentsShouldNotWrapToHalfTurnHandles()
		{
			const float chordLength = 30f;
			var handleLength = RubberCircleHullSolver.HandleLength(chordLength,
				new float2(1f, 0f), math.normalize(new float2(1f, -1e-7f)), 0.5f);

			Assert.That(handleLength, Is.EqualTo(chordLength / 3f).Within(1e-4f));
		}

		[Test]
		public void ShouldExcludeContainedAndDuplicateGuidesDeterministically()
		{
			var result = RubberCircleHullSolver.Solve(new[] {
				new RubberGuideCircle(new float2(0f), 20f, 4),
				new RubberGuideCircle(new float2(0f), 20f, 1),
				new RubberGuideCircle(new float2(3f, 0f), 4f, 2),
			}, 2f);

			Assert.That(result.IsValid, Is.True, result.Error);
			Assert.That(result.SupportingBindingIndices, Is.EqualTo(new[] { 1 }));
			Assert.That(result.EnclosedBindingIndices, Is.EqualTo(new[] { 2, 4 }));
		}

		[Test]
		public void ShouldTreatCollinearMiddleGuideAsNonSupporting()
		{
			var result = RubberCircleHullSolver.Solve(new[] {
				new RubberGuideCircle(new float2(-50f, 0f), 10f, 0),
				new RubberGuideCircle(new float2(0f, 0f), 10f, 1),
				new RubberGuideCircle(new float2(50f, 0f), 10f, 2),
			}, 4f);

			Assert.That(result.IsValid, Is.True, result.Error);
			Assert.That(result.SupportingBindingIndices, Is.EqualTo(new[] { 0, 2 }));
			Assert.That(result.EnclosedBindingIndices, Does.Contain(1));
			AssertClosed(result.Elements);
		}

		[Test]
		public void ShouldProduceSamePathForShuffledInput()
		{
			var guides = new[] {
				new RubberGuideCircle(new float2(-60f, -10f), 12f, 0),
				new RubberGuideCircle(new float2(20f, 50f), 9f, 1),
				new RubberGuideCircle(new float2(70f, -20f), 15f, 2),
				new RubberGuideCircle(new float2(0f, 0f), 3f, 3),
			};
			var shuffled = new[] { guides[2], guides[0], guides[3], guides[1] };

			var expected = RubberCircleHullSolver.Solve(guides, 4f);
			var actual = RubberCircleHullSolver.Solve(shuffled, 4f);

			Assert.That(expected.IsValid, Is.True, expected.Error);
			Assert.That(actual.IsValid, Is.True, actual.Error);
			Assert.That(actual.Elements, Has.Length.EqualTo(expected.Elements.Length));
			for (var i = 0; i < expected.Elements.Length; i++) {
				AssertElementEqual(expected.Elements[i], actual.Elements[i], i);
			}
		}

		[Test]
		public void ShouldBakeSplineWithinToleranceAndKeepFreeSpansStraight()
		{
			var fit = RubberCircleHullSolver.Solve(new[] {
				new RubberGuideCircle(new float2(-45f, 0f), 13f, 0),
				new RubberGuideCircle(new float2(55f, 8f), 18f, 1),
			}, 4f);
			Assert.That(fit.IsValid, Is.True, fit.Error);

			var baked = RubberPathSplineBaker.TryCreateDragPoints(fit.Elements,
				Matrix4x4.identity, 0.05f, out var dragPoints, out var deviation, out var error);

			Assert.That(baked, Is.True, error);
			Assert.That(deviation, Is.LessThanOrEqualTo(0.05f));
			var vertices = DragPoint.GetRgVertex<RenderVertex2D,
				CatmullCurve2DCatmullCurveFactory>(dragPoints, true, 0.0001f);
			foreach (var span in fit.Elements.Where(element => element.Type == RubberPathElementType.FreeSpan)) {
				AssertRenderedSpanIsStraight(vertices, span);
			}
		}

		[Test]
		public void ShouldBakeRoundedLoopWithinTolerance()
		{
			var fit = RubberCircleHullSolver.Solve(new[] {
				new RubberGuideCircle(new float2(-45f, 0f), 13f, 0),
				new RubberGuideCircle(new float2(55f, 8f), 18f, 1),
			}, 4f, RubberAutofit.SolverEpsilonVpx, 0.75f);
			Assert.That(fit.IsValid, Is.True, fit.Error);

			var baked = RubberPathSplineBaker.TryCreateDragPoints(fit.Elements,
				Matrix4x4.identity, 0.05f, out var dragPoints, out var deviation, out var error);

			Assert.That(baked, Is.True, error);
			Assert.That(deviation, Is.LessThanOrEqualTo(0.05f));
			Assert.That(dragPoints, Has.All.Matches<DragPointData>(point => point.IsSmooth));
		}

		[Test]
		public void ShouldResolveUnityGuideSlotsAndDetectStaleBake()
		{
			var rubberObject = new GameObject("Rubber");
			var guideAObject = new GameObject("Guide A");
			var guideBObject = new GameObject("Guide B");
			try {
				guideBObject.transform.position = new Vector3(0.1f, 0f, 0f);
				var rubber = rubberObject.AddComponent<RubberComponent>();
				var guideA = AddGuide(guideAObject, 0.01f);
				var guideB = AddGuide(guideBObject, 0.012f);
				rubber.SetGuideBindings(new[] {
					new RubberGuideBinding(guideA, guideA.Slots[0].Id),
					new RubberGuideBinding(guideB, guideB.Slots[0].Id),
				});

				var baked = RubberAutofit.TryBake(rubber, out var fit, out var error);

				Assert.That(baked, Is.True, error);
				Assert.That(fit.IsValid, Is.True, fit.Error);
				Assert.That(RubberAutofit.GetStatus(rubber).IsValid, Is.True);
				rubber.Roundness = 0.4f;
				var roundnessStatus = RubberAutofit.GetStatus(rubber);
				Assert.That(roundnessStatus.IsValid, Is.False);
				Assert.That(roundnessStatus.IsStale, Is.True);
				Assert.That(RubberAutofit.TryBake(rubber, out _, out error), Is.True, error);
				guideBObject.transform.position += new Vector3(0.01f, 0f, 0f);
				var status = RubberAutofit.GetStatus(rubber);
				Assert.That(status.IsValid, Is.False);
				Assert.That(status.IsStale, Is.True);
			} finally {
				UnityEngine.Object.DestroyImmediate(rubberObject);
				UnityEngine.Object.DestroyImmediate(guideAObject);
				UnityEngine.Object.DestroyImmediate(guideBObject);
			}
		}

		[Test]
		public void ShouldRequireExplicitAlignmentBeforeBaking()
		{
			var rubberObject = new GameObject("Rubber");
			var guideAObject = new GameObject("Guide A");
			var guideBObject = new GameObject("Guide B");
			try {
				rubberObject.transform.position = new Vector3(0f, 0.0053f, 0f);
				guideBObject.transform.position = new Vector3(0.1f, 0f, 0f);
				var rubber = rubberObject.AddComponent<RubberComponent>();
				var guideA = AddGuide(guideAObject, 0.01f);
				var guideB = AddGuide(guideBObject, 0.012f);
				rubber.SetGuideBindings(new[] {
					new RubberGuideBinding(guideA, guideA.Slots[0].Id),
					new RubberGuideBinding(guideB, guideB.Slots[0].Id),
				});

				var bakedBeforeAlignment = RubberAutofit.TryBake(rubber, out _, out var error);

				Assert.That(bakedBeforeAlignment, Is.False);
				Assert.That(error, Does.Contain("outside the guide plane"));
				Assert.That(RubberGuideResolver.TryGetGuidePlaneAlignedPosition(rubber,
					out var alignedPosition), Is.True);
				rubberObject.transform.position = alignedPosition;
				Assert.That(RubberAutofit.TryBake(rubber, out _, out error), Is.True, error);
				Assert.That(rubberObject.transform.position.y, Is.Zero.Within(1e-6f));
				Assert.That(RubberAutofit.GetStatus(rubber).IsValid, Is.True);
			} finally {
				UnityEngine.Object.DestroyImmediate(rubberObject);
				UnityEngine.Object.DestroyImmediate(guideAObject);
				UnityEngine.Object.DestroyImmediate(guideBObject);
			}
		}

		[Test]
		public void FailedBakeShouldNotMoveRubberOrigin()
		{
			var rubberObject = new GameObject("Rubber");
			var guideObject = new GameObject("Guide");
			try {
				var originalPosition = new Vector3(0.01f, 0f, -0.02f);
				rubberObject.transform.position = originalPosition;
				var rubber = rubberObject.AddComponent<RubberComponent>();
				var guide = AddGuide(guideObject, 0.01f);
				rubber.SetGuideBindings(new[] {
					new RubberGuideBinding(guide, guide.Slots[0].Id),
					new RubberGuideBinding(guide, SerializedGuid.New()),
				});

				var baked = RubberAutofit.TryBake(rubber, out _, out var error);

				Assert.That(baked, Is.False);
				Assert.That(error, Does.Contain("missing slot"));
				Assert.That(rubberObject.transform.position, Is.EqualTo(originalPosition));
			} finally {
				UnityEngine.Object.DestroyImmediate(rubberObject);
				UnityEngine.Object.DestroyImmediate(guideObject);
			}
		}

		[Test]
		public void ShouldRejectNonuniformGuideProfileScale()
		{
			var rubberObject = new GameObject("Rubber");
			var guideObject = new GameObject("Guide");
			try {
				var rubber = rubberObject.AddComponent<RubberComponent>();
				var guide = AddGuide(guideObject, 0.01f);
				guideObject.transform.localScale = new Vector3(2f, 1f, 1f);
				rubber.SetGuideBindings(new[] {
					new RubberGuideBinding(guide, guide.Slots[0].Id),
				});

				var resolution = RubberGuideResolver.Resolve(rubber);

				Assert.That(resolution.IsValid, Is.False);
				Assert.That(resolution.Error, Does.Contain("nonuniform"));
			} finally {
				UnityEngine.Object.DestroyImmediate(rubberObject);
				UnityEngine.Object.DestroyImmediate(guideObject);
			}
		}

		[Test]
		public void FailedRebindShouldRetainLastBakedGeometry()
		{
			var rubberObject = new GameObject("Rubber");
			var guideObject = new GameObject("Guide");
			try {
				var rubber = rubberObject.AddComponent<RubberComponent>();
				var guide = AddGuide(guideObject, 0.01f);
				rubber.SetGuideBindings(new[] {
					new RubberGuideBinding(guide, guide.Slots[0].Id),
				});
				Assert.That(RubberAutofit.TryBake(rubber, out _, out var bakeError),
					Is.True, bakeError);
				var previousPath = rubber.BakedPath.ToArray();

				rubber.SetGuideBindings(new[] {
					new RubberGuideBinding(guide, SerializedGuid.New()),
				});
				var baked = RubberAutofit.TryBake(rubber, out _, out var error);

				Assert.That(baked, Is.False);
				Assert.That(error, Does.Contain("missing slot"));
				Assert.That(rubber.BakedPath, Is.EqualTo(previousPath));
				Assert.That(RubberAutofit.GetStatus(rubber).IsValid, Is.False);
			} finally {
				UnityEngine.Object.DestroyImmediate(rubberObject);
				UnityEngine.Object.DestroyImmediate(guideObject);
			}
		}

		[Test]
		public void ShouldRepairSeveralMissingSlotsOneBindingAtATime()
		{
			var rubberObject = new GameObject("Rubber");
			var guideAObject = new GameObject("Guide A");
			var guideBObject = new GameObject("Guide B");
			try {
				guideBObject.transform.localPosition = new Vector3(0.1f, 0f, 0f);
				var rubber = rubberObject.AddComponent<RubberComponent>();
				var guideA = AddGuide(guideAObject, 0.01f);
				var guideB = AddGuide(guideBObject, 0.012f);
				var originalBindings = new[] {
					new RubberGuideBinding(guideA, guideA.Slots[0].Id),
					new RubberGuideBinding(guideB, guideB.Slots[0].Id),
				};
				rubber.SetGuideBindings(originalBindings);
				Assert.That(RubberAutofit.TryBake(rubber, out _, out var bakeError),
					Is.True, bakeError);
				var previousPath = rubber.BakedPath.ToArray();
				var previousDragPoints = rubber.DragPoints.Select(point => point.Center).ToArray();

				guideA.Slots = new[] { RubberGuideSlot.Create("Replacement", 0.01f) };
				guideB.Slots = new[] { RubberGuideSlot.Create("Replacement", 0.012f) };
				var repairedBindings = originalBindings.ToArray();
				repairedBindings[0].SlotId = guideA.Slots[0].Id;

				var firstApplied = RubberAutofit.TryReplaceOrRepairGuideBindings(rubber,
					repairedBindings, out var firstBaked, out _, out var firstError);

				Assert.That(firstApplied, Is.True, firstError);
				Assert.That(firstBaked, Is.False);
				Assert.That(firstError, Does.Contain("missing slot"));
				Assert.That(rubber.GuideBindings[0].SlotId,
					Is.EqualTo(guideA.Slots[0].Id));
				Assert.That(rubber.GuideBindings[1].SlotId,
					Is.EqualTo(originalBindings[1].SlotId));
				Assert.That(rubber.BakedPath, Is.EqualTo(previousPath));
				Assert.That(rubber.DragPoints.Select(point => point.Center),
					Is.EqualTo(previousDragPoints));

				repairedBindings[1].SlotId = guideB.Slots[0].Id;
				var secondApplied = RubberAutofit.TryReplaceOrRepairGuideBindings(rubber,
					repairedBindings, out var secondBaked, out _, out var secondError);

				Assert.That(secondApplied, Is.True, secondError);
				Assert.That(secondBaked, Is.True);
				Assert.That(rubber.GuideBindings.Select(binding => binding.SlotId),
					Is.EqualTo(new[] { guideA.Slots[0].Id, guideB.Slots[0].Id }));
				Assert.That(rubber.HasValidGuidedPath, Is.True);
			} finally {
				UnityEngine.Object.DestroyImmediate(rubberObject);
				UnityEngine.Object.DestroyImmediate(guideAObject);
				UnityEngine.Object.DestroyImmediate(guideBObject);
			}
		}

		[Test]
		public void FailedGuidedReplacementShouldRestoreCurrentBake()
		{
			var rubberObject = new GameObject("Rubber");
			var guideObject = new GameObject("Guide");
			try {
				var rubber = rubberObject.AddComponent<RubberComponent>();
				var guide = AddGuide(guideObject, 0.01f);
				var binding = new RubberGuideBinding(guide, guide.Slots[0].Id);
				rubber.SetGuideBindings(new[] { binding });
				Assert.That(RubberAutofit.TryBake(rubber, out _, out var bakeError),
					Is.True, bakeError);
				var previousPath = rubber.BakedPath.ToArray();
				var previousVersion = rubber.BakeVersion;
				var previousHash = rubber.BakeInputHash;
				var previousFrame = rubber.BakeFrameToLocal;

				var replaced = RubberAutofit.TryReplaceGuideBindings(rubber, new[] {
					new RubberGuideBinding(guide, SerializedGuid.New()),
				}, out _, out var error);

				Assert.That(replaced, Is.False);
				Assert.That(error, Does.Contain("missing slot"));
				Assert.That(rubber.PathSource, Is.EqualTo(RubberPathSource.Guides));
				Assert.That(rubber.GuideBindings.Count, Is.EqualTo(1));
				Assert.That(rubber.GuideBindings[0].Guide, Is.SameAs(binding.Guide));
				Assert.That(rubber.GuideBindings[0].SlotId, Is.EqualTo(binding.SlotId));
				Assert.That(rubber.BakedPath, Is.EqualTo(previousPath));
				Assert.That(rubber.BakeVersion, Is.EqualTo(previousVersion));
				Assert.That(rubber.BakeInputHash, Is.EqualTo(previousHash));
				Assert.That(rubber.BakeFrameToLocal, Is.EqualTo(previousFrame));
				Assert.That(RubberAutofit.GetStatus(rubber).IsValid, Is.True);
			} finally {
				UnityEngine.Object.DestroyImmediate(rubberObject);
				UnityEngine.Object.DestroyImmediate(guideObject);
			}
		}

		[Test]
		public void GuidedReplacementShouldRejectSplineRubberWithoutMutation()
		{
			var rubberObject = new GameObject("Rubber");
			var guideObject = new GameObject("Guide");
			try {
				var rubber = rubberObject.AddComponent<RubberComponent>();
				var guide = AddGuide(guideObject, 0.01f);

				var replaced = RubberAutofit.TryReplaceGuideBindings(rubber, new[] {
					new RubberGuideBinding(guide, guide.Slots[0].Id),
				}, out _, out var error);

				Assert.That(replaced, Is.False);
				Assert.That(error, Does.Contain("guided rubber"));
				Assert.That(rubber.PathSource, Is.EqualTo(RubberPathSource.Spline));
				Assert.That(rubber.GuideBindings, Is.Empty);
				Assert.That(rubber.BakedPath, Is.Empty);
			} finally {
				UnityEngine.Object.DestroyImmediate(rubberObject);
				UnityEngine.Object.DestroyImmediate(guideObject);
			}
		}

		[Test]
		public void DetachingGuidedRubberShouldKeepSampledSpline()
		{
			var rubberObject = new GameObject("Rubber");
			var guideObject = new GameObject("Guide");
			try {
				var rubber = rubberObject.AddComponent<RubberComponent>();
				var collider = rubberObject.AddComponent<RubberColliderComponent>();
				collider.Mode = RubberColliderMode.Physical;
				var guide = AddGuide(guideObject, 0.01f);
				rubber.SetGuideBindings(new[] {
					new RubberGuideBinding(guide, guide.Slots[0].Id),
				});
				Assert.That(RubberAutofit.TryBake(rubber, out _, out var bakeError),
					Is.True, bakeError);
				var sampledPoints = rubber.DragPoints.Select(point => point.Center).ToArray();

				rubber.DetachFromGuides();
				rubber.RebuildMeshes();

				Assert.That(rubber.PathSource, Is.EqualTo(RubberPathSource.Spline));
				Assert.That(rubber.GuideBindings, Is.Empty);
				Assert.That(rubber.BakedPath, Is.Empty);
				Assert.That(rubber.DragPoints.Select(point => point.Center), Is.EqualTo(sampledPoints));
				Assert.That(collider.Mode, Is.EqualTo(RubberColliderMode.Legacy));
			} finally {
				UnityEngine.Object.DestroyImmediate(rubberObject);
				UnityEngine.Object.DestroyImmediate(guideObject);
			}
		}

		[Test]
		public void FailedSplineConversionShouldRestoreManualAuthority()
		{
			var rubberObject = new GameObject("Rubber");
			var guideObject = new GameObject("Guide");
			try {
				var rubber = rubberObject.AddComponent<RubberComponent>();
				rubber.DragPoints = new[] {
					new DragPointData(-10f, -10f),
					new DragPointData(-10f, 10f),
					new DragPointData(10f, 10f),
					new DragPointData(10f, -10f),
				};
				rubber.RestLength = 123f;
				var originalPoints = rubber.DragPoints.Select(point => point.Center).ToArray();
				var guide = AddGuide(guideObject, 0.01f);

				var converted = RubberAutofit.TryConvertToGuides(rubber, new[] {
					new RubberGuideBinding(guide, SerializedGuid.New()),
				}, out _, out var error);

				Assert.That(converted, Is.False);
				Assert.That(error, Does.Contain("missing slot"));
				Assert.That(rubber.PathSource, Is.EqualTo(RubberPathSource.Spline));
				Assert.That(rubber.GuideBindings, Is.Empty);
				Assert.That(rubber.BakedPath, Is.Empty);
				Assert.That(rubber.RestLength, Is.EqualTo(123f));
				Assert.That(rubber.DragPoints.Select(point => point.Center),
					Is.EqualTo(originalPoints));
			} finally {
				UnityEngine.Object.DestroyImmediate(rubberObject);
				UnityEngine.Object.DestroyImmediate(guideObject);
			}
		}

		[Test]
		public void ShouldRejectSelfIntersectingCompiledPath()
		{
			var points = new[] {
				new float2(-10f, -10f),
				new float2(10f, 10f),
				new float2(-10f, 10f),
				new float2(10f, -10f),
			};
			var elements = new RubberPathElement[points.Length];
			var distance = 0f;
			for (var i = 0; i < points.Length; i++) {
				var end = points[(i + 1) % points.Length];
				var delta = end - points[i];
				var length = math.distance(points[i], end);
				elements[i] = new RubberPathElement {
					Type = RubberPathElementType.FreeSpan,
					Start = points[i],
					End = end,
					Control1 = points[i] + delta / 3f,
					Control2 = points[i] + delta * (2f / 3f),
					StartDistance = distance,
					Length = length,
				};
				distance += length;
			}

			var valid = RubberPathValidator.TryValidate(elements,
				RubberAutofit.SolverEpsilonVpx, out var error);

			Assert.That(valid, Is.False);
			Assert.That(error, Does.Contain("intersects itself"));
		}

		private static RubberGuideComponent AddGuide(GameObject gameObject, float radius)
		{
			var guide = gameObject.AddComponent<RubberGuideComponent>();
			guide.Slots = new[] { RubberGuideSlot.Create("Default", radius) };
			return guide;
		}

		private static void AssertClosed(RubberPathElement[] elements)
		{
			for (var i = 0; i < elements.Length; i++) {
				Assert.That(math.distance(elements[i].End, elements[(i + 1) % elements.Length].Start),
					Is.LessThan(1e-3f), $"path junction {i}");
			}
		}

		private static void AssertElementEqual(RubberPathElement expected,
			RubberPathElement actual, int index)
		{
			Assert.That(actual.Type, Is.EqualTo(expected.Type), $"element {index} type");
			Assert.That(math.distance(actual.Start, expected.Start), Is.LessThan(1e-4f),
				$"element {index} start");
			Assert.That(math.distance(actual.End, expected.End), Is.LessThan(1e-4f),
				$"element {index} end");
			Assert.That(math.distance(actual.Control1, expected.Control1), Is.LessThan(1e-4f),
				$"element {index} first control");
			Assert.That(math.distance(actual.Control2, expected.Control2), Is.LessThan(1e-4f),
				$"element {index} second control");
			Assert.That(actual.StartBindingIndex, Is.EqualTo(expected.StartBindingIndex),
				$"element {index} start binding");
			Assert.That(actual.EndBindingIndex, Is.EqualTo(expected.EndBindingIndex),
				$"element {index} end binding");
		}

		private static void AssertRenderedSpanIsStraight(RenderVertex2D[] vertices,
			RubberPathElement span)
		{
			var start = FindVertex(vertices, span.Start);
			var end = FindVertex(vertices, span.End);
			Assert.That(start, Is.GreaterThanOrEqualTo(0), "free-span start control point");
			Assert.That(end, Is.GreaterThanOrEqualTo(0), "free-span end control point");
			var delta = span.End - span.Start;
			for (var i = start;; i = (i + 1) % vertices.Length) {
				var point = new float2(vertices[i].X, vertices[i].Y);
				var cross = math.abs(delta.x * (point.y - span.Start.y)
					- delta.y * (point.x - span.Start.x));
				Assert.That(cross, Is.LessThan(1e-3f));
				if (i == end) {
					break;
				}
			}
		}

		private static int FindVertex(RenderVertex2D[] vertices, float2 point)
		{
			for (var i = 0; i < vertices.Length; i++) {
				if (math.distance(new float2(vertices[i].X, vertices[i].Y), point) < 1e-4f) {
					return i;
				}
			}
			return -1;
		}

		private static void AssertGuideContact(float2 point, RubberGuideCircle guide,
			float cordRadius)
		{
			Assert.That(math.distance(point, guide.Center),
				Is.EqualTo(guide.Radius + cordRadius).Within(1e-3f),
				$"guide binding {guide.BindingIndex} contact");
		}

		private static void AssertOutsideGuides(RubberPathElement[] elements,
			RubberGuideCircle[] guides, float cordRadius)
		{
			foreach (var element in elements) {
				var samples = element.Type == RubberPathElementType.SupportedArc
					? math.max(2, (int)math.ceil(element.SweepAngleRad / (math.PI / 64f)))
					: math.max(2, RubberPathMath.FreeSpanSubdivisionCount(element, 0.02f));
				for (var sample = 0; sample <= samples; sample++) {
					var point = RubberPathMath.PointAt(element, sample / (float)samples);
					foreach (var guide in guides) {
						Assert.That(math.distance(point, guide.Center),
							Is.GreaterThanOrEqualTo(guide.Radius + cordRadius - 0.05f),
							$"path element crosses guide binding {guide.BindingIndex}");
					}
				}
			}
		}
	}
}
