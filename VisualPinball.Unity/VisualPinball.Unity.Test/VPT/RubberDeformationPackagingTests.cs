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
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Test
{
	public class RubberDeformationPackagingTests
	{
		[Test]
		public void ShouldRoundTripEveryDeformationField()
		{
			var sourceObject = new GameObject("Source Rubber Collider");
			var targetObject = new GameObject("Target Rubber Collider");
			sourceObject.SetActive(false);
			targetObject.SetActive(false);
			try {
				sourceObject.AddComponent<RubberComponent>();
				var source = sourceObject.AddComponent<RubberColliderComponent>();
				source.Mode = RubberColliderMode.Physical;
				source.DeformationEnabled = true;
				source.DeflectionClearance = 37.5f;
				source.ApplyDeformationBake(Material(), new[] { Span(), SecondSpan() },
					new[] { BackedResponse() }, RubberDeformationBakeOutcome.Deformable,
					"two spans compiled", 1, Hash('a'));

				targetObject.AddComponent<RubberComponent>();
				var target = targetObject.AddComponent<RubberColliderComponent>();
				target.Unpack(source.Pack());

				Assert.That(target.DeformationEnabled, Is.True);
				Assert.That(target.DeflectionClearance, Is.EqualTo(37.5f));
				Assert.That(target.BakeOutcome,
					Is.EqualTo(RubberDeformationBakeOutcome.Deformable));
				Assert.That(target.BakeReason, Is.EqualTo("two spans compiled"));
				Assert.That(target.DeformationBakeVersion, Is.EqualTo(1));
				Assert.That(target.DeformationBakeHash, Is.EqualTo(Hash('a')));

				var material = target.CompiledDeformationMaterial;
				Assert.That(material.CompilerVersion, Is.EqualTo(7));
				Assert.That(material.LinearDensity, Is.EqualTo(0.125f));
				Assert.That(material.DampingRatio, Is.EqualTo(0.08f));
				Assert.That(material.SurfaceFriction, Is.EqualTo(0.31f));
				Assert.That(material.MaximumStretchRatio, Is.EqualTo(2.25f));
				Assert.That(material.StressLut,
					Has.Length.EqualTo(RubberCompiledDeformationMaterial.LutSampleCount));
				Assert.That(material.StressLut[3], Is.EqualTo(3f * 0.5f));
				Assert.That(material.TangentStiffnessLut[3], Is.EqualTo(3f * 1.5f));

				Assert.That(target.SpanDescriptors.Count, Is.EqualTo(2));
				var span = target.SpanDescriptors[0];
				var expected = Span();
				Assert.That(span.PathElementIndex, Is.EqualTo(expected.PathElementIndex));
				Assert.That(span.StartBindingIndex, Is.EqualTo(expected.StartBindingIndex));
				Assert.That(span.EndBindingIndex, Is.EqualTo(expected.EndBindingIndex));
				Assert.That(span.InstalledLength, Is.EqualTo(expected.InstalledLength));
				Assert.That(span.UnloadedLength, Is.EqualTo(expected.UnloadedLength));
				Assert.That(span.InstalledTension, Is.EqualTo(expected.InstalledTension));
				Assert.That(span.TangentStiffness, Is.EqualTo(expected.TangentStiffness));
				Assert.That(span.ModalMass, Is.EqualTo(expected.ModalMass));
				Assert.That(span.ModalStiffness, Is.EqualTo(expected.ModalStiffness));
				Assert.That(span.ModalDamping, Is.EqualTo(expected.ModalDamping));
				Assert.That(span.MaxDeflection, Is.EqualTo(expected.MaxDeflection));
				Assert.That(span.SurfaceFriction, Is.EqualTo(expected.SurfaceFriction));
				Assert.That(span.InwardSign, Is.EqualTo(expected.InwardSign));
				Assert.That(span.SegmentCount, Is.EqualTo(expected.SegmentCount));
				Assert.That(span.ArcLengthFractions, Is.EqualTo(expected.ArcLengthFractions));
				Assert.That(target.SpanDescriptors[1].InwardSign, Is.EqualTo((sbyte)-1));

				Assert.That(target.BackedResponseDescriptors.Count, Is.EqualTo(1));
				var backed = target.BackedResponseDescriptors[0];
				var expectedBacked = BackedResponse();
				Assert.That(backed.BindingIndex, Is.EqualTo(expectedBacked.BindingIndex));
				Assert.That(backed.PathElementIndex, Is.EqualTo(-1));
				Assert.That(backed.Material, Is.EqualTo(expectedBacked.Material));
				Assert.That(backed.ElasticityOverVelocityLut,
					Is.EqualTo(expectedBacked.ElasticityOverVelocityLut));
				Assert.That(backed.FrictionOverVelocityLut,
					Is.EqualTo(expectedBacked.FrictionOverVelocityLut));
				Assert.That(backed.ElasticityScale, Is.EqualTo(expectedBacked.ElasticityScale));
			} finally {
				Object.DestroyImmediate(sourceObject);
				Object.DestroyImmediate(targetObject);
			}
		}

		[Test]
		public void ShouldLoadVersionTwoPayloadsWithDeformationDisabled()
		{
			var gameObject = new GameObject("Version 2 Rubber Collider");
			gameObject.SetActive(false);
			try {
				gameObject.AddComponent<RubberComponent>();
				var collider = gameObject.AddComponent<RubberColliderComponent>();
				collider.DeformationEnabled = true;
				collider.DeflectionClearance = 12f;
				collider.ApplyDeformationBake(Material(), new[] { Span() },
					new[] { BackedResponse() }, RubberDeformationBakeOutcome.Deformable,
					"stale", 1, Hash('b'));

				var bytes = PackageApi.Packer.Pack(new RubberColliderPackable {
					Version = 2,
					Mode = (int)RubberColliderMode.Physical,
					GuideDistanceElasticityEnabled = true,
					GuideElasticityScale = 0.5f,
					GuideInfluenceLength = 60f,
					MaximumResponseSegmentLength = 15f,
				});
				collider.Unpack(bytes);

				Assert.That(collider.Mode, Is.EqualTo(RubberColliderMode.Physical));
				Assert.That(collider.GuideDistanceElasticityEnabled, Is.True);
				Assert.That(collider.DeformationEnabled, Is.False);
				Assert.That(collider.DeflectionClearance,
					Is.EqualTo(RubberColliderComponent.DefaultDeflectionClearance));
				Assert.That(collider.SpanDescriptors, Is.Empty);
				Assert.That(collider.BackedResponseDescriptors, Is.Empty);
				Assert.That(collider.BakeOutcome,
					Is.EqualTo(RubberDeformationBakeOutcome.Invalid));
				Assert.That(collider.BakeReason,
					Is.EqualTo(RubberColliderComponent.NotBakedReason));
				Assert.That(collider.DeformationBakeVersion, Is.Zero);
				Assert.That(collider.DeformationBakeHash, Is.Empty);
				Assert.That(collider.CompiledDeformationMaterial.IsPopulated, Is.False);
			} finally {
				Object.DestroyImmediate(gameObject);
			}
		}

		[Test]
		public void ShouldSanitizeMalformedDeformationPayloads()
		{
			var gameObject = new GameObject("Malformed Rubber Collider");
			gameObject.SetActive(false);
			try {
				gameObject.AddComponent<RubberComponent>();
				var collider = gameObject.AddComponent<RubberColliderComponent>();

				var bytes = PackageApi.Packer.Pack(new RubberColliderPackable {
					Version = 3,
					DeformationEnabled = true,
					DeflectionClearance = float.NaN,
					SpanDescriptors = null,
					BackedResponseDescriptors = null,
					BakeOutcome = 99,
					BakeReason = null,
					DeformationBakeVersion = -4,
					DeformationBakeHash = "not a hash",
				});
				collider.Unpack(bytes);

				Assert.That(collider.DeformationEnabled, Is.True);
				Assert.That(collider.DeflectionClearance,
					Is.EqualTo(RubberColliderComponent.DefaultDeflectionClearance));
				Assert.That(collider.SpanDescriptors, Is.Empty);
				Assert.That(collider.BackedResponseDescriptors, Is.Empty);
				Assert.That(collider.BakeOutcome,
					Is.EqualTo(RubberDeformationBakeOutcome.Invalid));
				Assert.That(collider.BakeReason,
					Is.EqualTo(RubberColliderComponent.NotBakedReason));
				Assert.That(collider.DeformationBakeHash, Is.Empty);
				Assert.That(collider.DeformationBakeVersion, Is.Zero);

				var negative = PackageApi.Packer.Pack(new RubberColliderPackable {
					Version = 3,
					DeflectionClearance = -12f,
					BakeOutcome = (int)RubberDeformationBakeOutcome.StaticFallback,
					BakeReason = "fallback",
					DeformationBakeVersion = 1,
					DeformationBakeHash = new string('A', 64),
					SpanDescriptors = new[] {
						new RubberSpanDescriptorPackable { InwardSign = 42, SegmentCount = 8 },
					},
				});
				collider.Unpack(negative);

				Assert.That(collider.DeflectionClearance,
					Is.EqualTo(RubberColliderComponent.DefaultDeflectionClearance));
				Assert.That(collider.BakeOutcome,
					Is.EqualTo(RubberDeformationBakeOutcome.StaticFallback));
				// Uppercase hex is not canonical, so the bake counts as absent.
				Assert.That(collider.DeformationBakeHash, Is.Empty);
				Assert.That(collider.DeformationBakeVersion, Is.Zero);
				Assert.That(collider.SpanDescriptors[0].InwardSign, Is.EqualTo((sbyte)1));
				Assert.That(collider.SpanDescriptors[0].ArcLengthFractions, Is.Empty);

				var tiny = PackageApi.Packer.Pack(new RubberColliderPackable {
					Version = 3,
					DeflectionClearance = 0.01f,
				});
				collider.Unpack(tiny);
				Assert.That(collider.DeflectionClearance,
					Is.EqualTo(RubberColliderComponent.MinimumDeflectionClearance));
			} finally {
				Object.DestroyImmediate(gameObject);
			}
		}

		[Test]
		public void ShouldRoundTripTheDeformationRendererShell()
		{
			var sourceObject = new GameObject("Source Renderer");
			var targetObject = new GameObject("Target Renderer");
			try {
				var source = sourceObject.AddComponent<RubberDeformationRenderer>();
				source.UpdateThreshold = 0.25f;
				source.RecalculateNormals = false;
				source.IncludeSupportedArcs = true;

				var target = targetObject.AddComponent<RubberDeformationRenderer>();
				target.Unpack(source.Pack());

				Assert.That(target.UpdateThreshold, Is.EqualTo(0.25f));
				Assert.That(target.RecalculateNormals, Is.False);
				Assert.That(target.IncludeSupportedArcs, Is.True);
				Assert.That(RubberDeformationRenderer.RuntimeAvailable, Is.False);

				target.UpdateThreshold = float.NaN;
				Assert.That(target.UpdateThreshold,
					Is.EqualTo(RubberDeformationRenderer.DefaultUpdateThreshold));
				target.Unpack(null);
				Assert.That(target.UpdateThreshold,
					Is.EqualTo(RubberDeformationRenderer.DefaultUpdateThreshold));
			} finally {
				Object.DestroyImmediate(sourceObject);
				Object.DestroyImmediate(targetObject);
			}
		}

		[Test]
		public void ShouldKeepTheGuideDistanceSettingsAcrossAVersionThreeRoundTrip()
		{
			var sourceObject = new GameObject("Source Rubber Collider");
			var targetObject = new GameObject("Target Rubber Collider");
			sourceObject.SetActive(false);
			targetObject.SetActive(false);
			try {
				sourceObject.AddComponent<RubberComponent>();
				var source = sourceObject.AddComponent<RubberColliderComponent>();
				source.GuideDistanceElasticityEnabled = true;
				source.GuideElasticityScale = 0.35f;
				source.GuideInfluenceLength = 75f;
				source.MaximumResponseSegmentLength = 12f;
				source.HitEvent = true;
				source.ZOffset = 3f;
				source._isKinematic = true;

				targetObject.AddComponent<RubberComponent>();
				var target = targetObject.AddComponent<RubberColliderComponent>();
				target.Unpack(source.Pack());

				Assert.That(target.GuideDistanceElasticityEnabled, Is.True);
				Assert.That(target.GuideElasticityScale, Is.EqualTo(0.35f));
				Assert.That(target.GuideInfluenceLength, Is.EqualTo(75f));
				Assert.That(target.MaximumResponseSegmentLength, Is.EqualTo(12f));
				Assert.That(target.HitEvent, Is.True);
				Assert.That(target.ZOffset, Is.EqualTo(3f));
				Assert.That(target.IsKinematic, Is.True);
			} finally {
				Object.DestroyImmediate(sourceObject);
				Object.DestroyImmediate(targetObject);
			}
		}

		private static RubberCompiledDeformationMaterial Material()
		{
			var count = RubberCompiledDeformationMaterial.LutSampleCount;
			return new RubberCompiledDeformationMaterial {
				CompilerVersion = 7,
				LinearDensity = 0.125f,
				DampingRatio = 0.08f,
				SurfaceFriction = 0.31f,
				MaximumStretchRatio = 2.25f,
				StressLut = Enumerable.Range(0, count).Select(i => i * 0.5f).ToArray(),
				TangentStiffnessLut = Enumerable.Range(0, count).Select(i => i * 1.5f).ToArray(),
			};
		}

		private static RubberSpanDescriptor Span()
		{
			return new RubberSpanDescriptor {
				PathElementIndex = 2,
				StartBindingIndex = 0,
				EndBindingIndex = 1,
				InstalledLength = 150f,
				UnloadedLength = 136.36f,
				InstalledTension = 0.0025f,
				TangentStiffness = 0.05f,
				ModalMass = new float4(1f, 2f, 3f, 4f),
				ModalStiffness = new float4(5f, 6f, 7f, 8f),
				ModalDamping = new float4(9f, 10f, 11f, 12f),
				MaxDeflection = 60f,
				SurfaceFriction = 0.3f,
				InwardSign = 1,
				SegmentCount = 20,
				ArcLengthFractions = Enumerable.Range(0, 21).Select(i => i / 20f).ToArray(),
			};
		}

		private static RubberSpanDescriptor SecondSpan()
		{
			var span = Span();
			span.PathElementIndex = 4;
			span.InwardSign = -1;
			span.SegmentCount = 16;
			span.ArcLengthFractions = Enumerable.Range(0, 17).Select(i => i / 16f).ToArray();
			return span;
		}

		private static RubberBackedResponseDescriptor BackedResponse()
		{
			return new RubberBackedResponseDescriptor {
				BindingIndex = 1,
				PathElementIndex = -1,
				Material = new PhysicsMaterialData {
					Elasticity = 0.8f,
					ElasticityFalloff = 0.4f,
					Friction = 0.3f,
					ScatterAngleRad = 0.05f,
					UseElasticityOverVelocity = true,
					UseFrictionOverVelocity = true,
				},
				ElasticityOverVelocityLut = Enumerable
					.Range(0, RubberBackedResponseDescriptor.VelocityLutSampleCount)
					.Select(i => 1f - i / 200f).ToArray(),
				FrictionOverVelocityLut = Enumerable
					.Range(0, RubberBackedResponseDescriptor.VelocityLutSampleCount)
					.Select(i => 0.1f + i / 500f).ToArray(),
				ElasticityScale = 0.45f,
			};
		}

		private static string Hash(char filler) => new(filler, 64);
	}
}
