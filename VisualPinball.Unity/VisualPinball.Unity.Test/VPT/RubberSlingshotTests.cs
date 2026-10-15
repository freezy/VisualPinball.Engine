// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using VisualPinball.Unity.Editor;

namespace VisualPinball.Unity.Test
{
	public class RubberSlingshotTests
	{
		[Test]
		public void ShouldResolveStableSpanBindingInBothDirectionsAfterRebake()
		{
			var rubberObject = new GameObject("Rubber");
			var guideObjects = new[] {
				new GameObject("Guide A"),
				new GameObject("Guide B"),
				new GameObject("Guide C"),
			};
			try {
				guideObjects[0].transform.position = new Vector3(-0.1f, 0f, -0.05f);
				guideObjects[1].transform.position = new Vector3(0.1f, 0f, -0.05f);
				guideObjects[2].transform.position = new Vector3(0f, 0f, 0.1f);
				var guides = guideObjects.Select(AddGuide).ToArray();
				var rubber = rubberObject.AddComponent<RubberComponent>();
				rubberObject.AddComponent<RubberColliderComponent>();
				rubber.SetGuideBindings(guides.Select(guide =>
					new RubberGuideBinding(guide, guide.Slots[0].Id)));
				Assert.That(RubberAutofit.TryBake(rubber, out _, out var bakeError),
					Is.True, bakeError);
				var element = rubber.BakedPath.First(path => path.Type == RubberPathElementType.FreeSpan);
				var forward = new RubberSpanBinding(
					rubber.GuideBindings[element.StartBindingIndex],
					rubber.GuideBindings[element.EndBindingIndex]);

				Assert.That(RubberSpanResolver.TryResolve(rubber, forward,
					out var resolvedForward, out var forwardError), Is.True, forwardError);
				Assert.That(resolvedForward.IsReversed, Is.False);
				Assert.That(resolvedForward.ToPathCoordinate(0.2f), Is.EqualTo(0.2f));

				var reversed = new RubberSpanBinding(forward.EndSupport, forward.StartSupport);
				Assert.That(RubberSpanResolver.TryResolve(rubber, reversed,
					out var resolvedReversed, out var reversedError), Is.True, reversedError);
				Assert.That(resolvedReversed.PathElementIndex,
					Is.EqualTo(resolvedForward.PathElementIndex));
				Assert.That(resolvedReversed.IsReversed, Is.True);
				Assert.That(resolvedReversed.ToPathCoordinate(0.2f), Is.EqualTo(0.8f));

				// the inside is the same side whichever way the span is bound, and it
				// is the side the rest of the rubber lies on
				Assert.That(math.distance(resolvedReversed.InwardAt(0.2f),
					resolvedForward.InwardAt(0.8f)), Is.LessThan(1e-5f));
				var centroid = rubber.BakedPath.Aggregate(float2.zero,
					(sum, path) => sum + path.Start) / rubber.BakedPath.Count;
				var middle = resolvedForward.PointAt(0.5f);
				Assert.That(math.dot(resolvedForward.InwardAt(0.5f), centroid - middle),
					Is.GreaterThan(0f));

				guideObjects[2].transform.position += new Vector3(0.01f, 0f, 0f);
				Assert.That(RubberAutofit.TryBake(rubber, out _, out bakeError),
					Is.True, bakeError);
				Assert.That(RubberSpanResolver.TryResolve(rubber, reversed,
					out var afterRebake, out var rebakeError), Is.True, rebakeError);
				Assert.That(afterRebake.IsReversed, Is.True);
			} finally {
				UnityEngine.Object.DestroyImmediate(rubberObject);
				foreach (var guideObject in guideObjects) {
					UnityEngine.Object.DestroyImmediate(guideObject);
				}
			}
		}

		[Test]
		public void ShouldRejectAmbiguousTwoPostSpanBinding()
		{
			var rubberObject = new GameObject("Rubber");
			var guideAObject = new GameObject("Guide A");
			var guideBObject = new GameObject("Guide B");
			try {
				guideAObject.transform.position = new Vector3(-0.1f, 0f, 0f);
				guideBObject.transform.position = new Vector3(0.1f, 0f, 0f);
				var guideA = AddGuide(guideAObject);
				var guideB = AddGuide(guideBObject);
				var rubber = rubberObject.AddComponent<RubberComponent>();
				rubberObject.AddComponent<RubberColliderComponent>();
				rubber.SetGuideBindings(new[] {
					new RubberGuideBinding(guideA, guideA.Slots[0].Id),
					new RubberGuideBinding(guideB, guideB.Slots[0].Id),
				});
				Assert.That(RubberAutofit.TryBake(rubber, out _, out var bakeError),
					Is.True, bakeError);

				var result = RubberSpanResolver.TryResolve(rubber,
					new RubberSpanBinding(rubber.GuideBindings[0], rubber.GuideBindings[1]),
					out _, out var error);

				Assert.That(result, Is.False);
				Assert.That(error, Does.Contain("more than one"));
			} finally {
				UnityEngine.Object.DestroyImmediate(rubberObject);
				UnityEngine.Object.DestroyImmediate(guideAObject);
				UnityEngine.Object.DestroyImmediate(guideBObject);
			}
		}

		[Test]
		public void ShouldEvaluateRoundedSpanInAuthoredDirection()
		{
			var element = new RubberPathElement {
				Type = RubberPathElementType.FreeSpan,
				Start = new float2(0f, 0f),
				Control1 = new float2(0f, 10f),
				Control2 = new float2(10f, 10f),
				End = new float2(10f, 0f),
			};
			var forward = new ResolvedRubberSpan(0, false, element);
			var reversed = new ResolvedRubberSpan(0, true, element);

			Assert.That(forward.PointAt(0.5f), Is.EqualTo(new float2(5f, 7.5f)));
			Assert.That(math.distance(forward.PointAt(0.5f),
				math.lerp(element.Start, element.End, 0.5f)), Is.GreaterThan(1f));
			Assert.That(reversed.PointAt(0.25f), Is.EqualTo(forward.PointAt(0.75f)));
			Assert.That(math.distance(reversed.TangentAt(0.25f),
				-forward.TangentAt(0.75f)), Is.LessThan(1e-5f));
		}

		[Test]
		public void IncompleteUnpackedSlingshotShouldWarn()
		{
			var gameObject = new GameObject("Incomplete Slingshot");
			try {
				var component = gameObject.AddComponent<RubberSlingshotComponent>();
				LogAssert.Expect(LogType.Warning,
					new Regex("Rubber slingshot \"Incomplete Slingshot\" was unpacked with an incomplete configuration"));

				component.ValidateAfterUnpack();

				Assert.That(component.enabled, Is.True);
			} finally {
				UnityEngine.Object.DestroyImmediate(gameObject);
			}
		}

		[Test]
		public void ASlingshotWithoutWallShouldSayNothingKicks()
		{
			var gameObject = new GameObject("Slingshot");
			try {
				var component = gameObject.AddComponent<RubberSlingshotComponent>();

				Assert.That(component.ValidateConfiguration(),
					Has.Some.Contains("no slingshot wall"));
			} finally {
				UnityEngine.Object.DestroyImmediate(gameObject);
			}
		}

		[Test]
		public void TheSlingshotShouldRequireTheRendererThatAnimatesItsCord()
		{
			var gameObject = new GameObject("Slingshot");
			try {
				var component = gameObject.AddComponent<RubberSlingshotComponent>();

				Assert.That(gameObject.GetComponent<RubberDeformationRenderer>(), Is.Not.Null);
				Assert.That(component.Rubber, Is.Null);
			} finally {
				UnityEngine.Object.DestroyImmediate(gameObject);
			}
		}

		[Test]
		public void EmptySlingshotReferencesPayloadShouldPreserveExistingReferences()
		{
			var rubberObject = new GameObject("Rubber");
			try {
				var rubber = rubberObject.AddComponent<RubberComponent>();
				var component = rubberObject.AddComponent<RubberSlingshotComponent>();
				var wall = rubberObject.AddComponent<SurfaceComponent>();
				component.SlingshotWall = wall;

				RubberSlingshotReferencesPackable.Unpack(Array.Empty<byte>(), component, null);

				Assert.That(component.Rubber, Is.SameAs(rubber));
				Assert.That(component.SlingshotWall, Is.SameAs(wall));
			} finally {
				UnityEngine.Object.DestroyImmediate(rubberObject);
			}
		}

		[Test]
		public void UnavailableCoilShouldBeIgnoredByEventTranslator()
		{
			var slingshotObject = new GameObject("Slingshot");
			var visualObject = new GameObject("Visual");
			try {
				slingshotObject.AddComponent<RubberSlingshotComponent>();
				visualObject.transform.SetParent(slingshotObject.transform, false);
				var translator = visualObject.AddComponent<EventTransformComponent>();
				var flags = BindingFlags.Instance | BindingFlags.NonPublic;
				var type = typeof(EventTransformComponent);
				var awake = type.GetMethod("Awake", flags);
				var start = type.GetMethod("Start", flags);
				var onDestroy = type.GetMethod("OnDestroy", flags);

				Assert.That(awake, Is.Not.Null);
				Assert.That(start, Is.Not.Null);
				Assert.That(onDestroy, Is.Not.Null);
				Assert.DoesNotThrow(() => awake.Invoke(translator, null));
				Assert.DoesNotThrow(() => start.Invoke(translator, null));
				Assert.DoesNotThrow(() => onDestroy.Invoke(translator, null));
			} finally {
				UnityEngine.Object.DestroyImmediate(visualObject);
				UnityEngine.Object.DestroyImmediate(slingshotObject);
			}
		}

		[Test]
		public void ShouldRoundTripEverySlingshotScalarField()
		{
			var sourceObject = new GameObject("Source Slingshot");
			var targetObject = new GameObject("Target Slingshot");
			try {
				var source = sourceObject.AddComponent<RubberSlingshotComponent>();
				var target = targetObject.AddComponent<RubberSlingshotComponent>();
				var startSlot = new SerializedGuid(1, 2);
				var endSlot = new SerializedGuid(3, 4);
				source.Span = new RubberSpanBinding(
					new RubberGuideBinding(null, startSlot),
					new RubberGuideBinding(null, endSlot));
				source.ArmContactPosition01 = 0.45f;
				source.ArmRestClearance = 1.5f;
				source.ArmTipTravel = 19f;
				source.SelfWired = false;
				source.SelfWirePulseMs = 23f;

				target.Unpack(source.Pack());

				Assert.That(target.Span.StartSupport.SlotId, Is.EqualTo(startSlot));
				Assert.That(target.Span.EndSupport.SlotId, Is.EqualTo(endSlot));
				Assert.That(target.ArmContactPosition01, Is.EqualTo(0.45f));
				Assert.That(target.ArmRestClearance, Is.EqualTo(1.5f));
				Assert.That(target.ArmTipTravel, Is.EqualTo(19f));
				Assert.That(target.SelfWired, Is.False);
				Assert.That(target.SelfWirePulseMs, Is.EqualTo(23f));
			} finally {
				UnityEngine.Object.DestroyImmediate(sourceObject);
				UnityEngine.Object.DestroyImmediate(targetObject);
			}
		}

		[Test]
		public void ASanitizedPulseShouldFallBackToItsDefault()
		{
			var gameObject = new GameObject("Sanitizing Slingshot");
			try {
				var component = gameObject.AddComponent<RubberSlingshotComponent>();
				component.SelfWirePulseMs = -1f;
				Assert.That(component.SelfWirePulseMs,
					Is.EqualTo(RubberSlingshotComponent.DefaultSelfWirePulseMs));

				component.SelfWirePulseMs = float.NaN;
				Assert.That(component.SelfWirePulseMs,
					Is.EqualTo(RubberSlingshotComponent.DefaultSelfWirePulseMs));
			} finally {
				UnityEngine.Object.DestroyImmediate(gameObject);
			}
		}

		private static RubberGuideComponent AddGuide(GameObject gameObject)
		{
			var guide = gameObject.AddComponent<RubberGuideComponent>();
			guide.Slots = new[] { RubberGuideSlot.Create("Default", 0.01f) };
			return guide;
		}
	}
}
