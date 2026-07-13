// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
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
				var collider = rubberObject.AddComponent<RubberColliderComponent>();
				collider.Mode = RubberColliderMode.Physical;
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
				var collider = rubberObject.AddComponent<RubberColliderComponent>();
				collider.Mode = RubberColliderMode.Physical;
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
		public void ShouldValidateAndInvertActuatorFluxLut()
		{
			var actuator = ScriptableObject.CreateInstance<SlingshotActuatorAsset>();
			try {
				actuator.CurrentSampleCount = 3;
				actuator.StrokeSampleCount = 2;
				actuator.MaximumCurrentAmps = 10f;
				actuator.ForceNewtonLut = new[] { 0f, 0f, 10f, 8f, 20f, 16f };
				actuator.FluxLinkageWeberTurnLut = new[] {
					0f, 0f,
					0.01f, 0.008f,
					0.02f, 0.016f,
				};

				Assert.That(actuator.ValidateData(), Is.Empty);
				Assert.That(actuator.TryGetCurrent(0.012f, 0.5f, out var current), Is.True);
				Assert.That(current, Is.EqualTo(6.6666665f).Within(1e-5f));

				actuator.FluxLinkageWeberTurnLut[4] = 0.005f;
				Assert.That(actuator.ValidateData(), Has.Some.Contains("monotone"));
			} finally {
				UnityEngine.Object.DestroyImmediate(actuator);
			}
		}

		[Test]
		public void ShouldRoundTripEveryActuatorFieldAndRejectInvalidPayload()
		{
			var source = ScriptableObject.CreateInstance<SlingshotActuatorAsset>();
			var target = ScriptableObject.CreateInstance<SlingshotActuatorAsset>();
			try {
				source.SupplyVoltage = 42f;
				source.CoilResistanceOhm = 3.2f;
				source.TurnOffClampVoltage = 47f;
				source.StrokeMeters = 0.03f;
				source.MaximumCurrentAmps = 11f;
				source.EffectiveMovingMassKg = 0.09f;
				source.ReturnSpringNewtonPerMeter = 275f;
				source.ReturnSpringPreloadNewton = 6f;
				source.ViscousDampingNewtonSecondPerMeter = 1.25f;
				source.EndStopStiffnessNewtonPerMeter = 51000f;
				source.EndStopDampingNewtonSecondPerMeter = 21f;
				source.ForceNewtonLut = new[] { 0f, 0f, 25f, 20f };
				source.FluxLinkageWeberTurnLut = new[] { 0f, 0f, 0.03f, 0.02f };
				var packer = new SlingshotActuatorPacker();
				var bytes = MetaPackable.PackMeta(packer.Pack(12, source, null));

				packer.Unpack(bytes, target, null);

				Assert.That(target.SupplyVoltage, Is.EqualTo(42f));
				Assert.That(target.CoilResistanceOhm, Is.EqualTo(3.2f));
				Assert.That(target.TurnOffClampVoltage, Is.EqualTo(47f));
				Assert.That(target.StrokeMeters, Is.EqualTo(0.03f));
				Assert.That(target.MaximumCurrentAmps, Is.EqualTo(11f));
				Assert.That(target.EffectiveMovingMassKg, Is.EqualTo(0.09f));
				Assert.That(target.ReturnSpringNewtonPerMeter, Is.EqualTo(275f));
				Assert.That(target.ReturnSpringPreloadNewton, Is.EqualTo(6f));
				Assert.That(target.ViscousDampingNewtonSecondPerMeter, Is.EqualTo(1.25f));
				Assert.That(target.EndStopStiffnessNewtonPerMeter, Is.EqualTo(51000f));
				Assert.That(target.EndStopDampingNewtonSecondPerMeter, Is.EqualTo(21f));
				Assert.That(target.CurrentSampleCount, Is.EqualTo(2));
				Assert.That(target.StrokeSampleCount, Is.EqualTo(2));
				Assert.That(target.ForceNewtonLut, Is.EqualTo(source.ForceNewtonLut));
				Assert.That(target.FluxLinkageWeberTurnLut,
					Is.EqualTo(source.FluxLinkageWeberTurnLut));

				var invalid = SlingshotActuatorPackable.From(13, source);
				invalid.FluxLinkageWeberTurnLut = new[] { 0f };
				var invalidBytes = MetaPackable.PackMeta(invalid);
				Assert.Throws<InvalidDataException>(() => packer.Unpack(invalidBytes, target, null));
			} finally {
				UnityEngine.Object.DestroyImmediate(source);
				UnityEngine.Object.DestroyImmediate(target);
			}
		}

		[Test]
		public void ShouldImportRectangularActuatorCsvInCurrentMajorOrder()
		{
			const string csv = "current_amps,stroke_meters,force_newton,flux_weber_turn\n"
				+ "10,0.02,4,0.04\n"
				+ "0,0,1,0\n"
				+ "10,0,3,0.05\n"
				+ "0,0.02,2,0\n";

			var parsed = SlingshotActuatorCsv.TryParse(csv, out var data, out var error);

			Assert.That(parsed, Is.True, error);
			Assert.That(data.CurrentSampleCount, Is.EqualTo(2));
			Assert.That(data.StrokeSampleCount, Is.EqualTo(2));
			Assert.That(data.ForceNewton, Is.EqualTo(new[] { 1f, 2f, 3f, 4f }));
			Assert.That(data.FluxLinkageWeberTurn,
				Is.EqualTo(new[] { 0f, 0f, 0.05f, 0.04f }));
		}

		[Test]
		public void ShouldRoundTripEverySlingshotScalarFieldWithoutRuntime()
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
				source.SwitchZones = new[] {
					new SlingshotSwitchZone {
						Position01 = 0.25f,
						CloseDeflection = 5f,
						OpenDeflection = 2f,
						MinimumClosedTimeMs = 9f,
					},
					new SlingshotSwitchZone {
						Position01 = 0.75f,
						CloseDeflection = 6f,
						OpenDeflection = 3f,
						MinimumClosedTimeMs = 10f,
					},
				};
				source.ArmContactPosition01 = 0.45f;
				source.ArmRestClearance = 1.5f;
				source.ArmTipTravel = 19f;
				source.VisualRotationAxis = Axis.Z;
				source.VisualRestAngle = -3f;
				source.VisualFiredAngle = 21f;

				target.Unpack(source.Pack());

				Assert.That(target.Span.StartSupport.SlotId, Is.EqualTo(startSlot));
				Assert.That(target.Span.EndSupport.SlotId, Is.EqualTo(endSlot));
				Assert.That(target.SwitchZones.Select(zone => zone.Position01),
					Is.EqualTo(new[] { 0.25f, 0.75f }));
				Assert.That(target.SwitchZones.Select(zone => zone.CloseDeflection),
					Is.EqualTo(new[] { 5f, 6f }));
				Assert.That(target.SwitchZones.Select(zone => zone.OpenDeflection),
					Is.EqualTo(new[] { 2f, 3f }));
				Assert.That(target.SwitchZones.Select(zone => zone.MinimumClosedTimeMs),
					Is.EqualTo(new[] { 9f, 10f }));
				Assert.That(target.ArmContactPosition01, Is.EqualTo(0.45f));
				Assert.That(target.ArmRestClearance, Is.EqualTo(1.5f));
				Assert.That(target.ArmTipTravel, Is.EqualTo(19f));
				Assert.That(target.VisualRotationAxis, Is.EqualTo(Axis.Z));
				Assert.That(target.VisualRestAngle, Is.EqualTo(-3f));
				Assert.That(target.VisualFiredAngle, Is.EqualTo(21f));
				Assert.That(RubberSlingshotComponent.RuntimeAvailable, Is.False);
			} finally {
				UnityEngine.Object.DestroyImmediate(sourceObject);
				UnityEngine.Object.DestroyImmediate(targetObject);
			}
		}

		private static RubberGuideComponent AddGuide(GameObject gameObject)
		{
			var guide = gameObject.AddComponent<RubberGuideComponent>();
			guide.AddSlot(RubberGuideSlot.Create("Default", 0.01f));
			return guide;
		}
	}
}
