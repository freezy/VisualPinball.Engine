// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.VPT.Rubber;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Test
{
	/// <summary>
	/// Section 6.3 of the static rebound plan, first bullet: what the physical rubber
	/// generator emits with and without the compliant rebound, and how the dense span
	/// ids are registered.
	/// </summary>
	public class RubberSpanReboundGeneratorTests
	{
		#region Collider stream

		[Test]
		public void PhysicalRubberWithoutTheToggleShouldEmitTodaysStream()
		{
			using var withoutToggle = new Fixture(deformationEnabled: false);
			using var withToggleButNoBake = new Fixture(deformationEnabled: true);

			var baseline = Stream(withoutToggle);
			var fallback = Stream(withToggleButNoBake);

			Assert.That(baseline, Is.Not.Empty);
			Assert.That(fallback, Is.EqualTo(baseline));
			foreach (var line in baseline) {
				Assert.That(line, Does.Contain("span=-1"));
			}
		}

		[Test]
		public void EveryStaticFallbackOutcomeShouldEmitTodaysStream()
		{
			using var baselineScope = new Fixture(deformationEnabled: false);
			var baseline = Stream(baselineScope);

			foreach (var outcome in new[] {
				RubberDeformationBakeOutcome.StaticFallback,
				RubberDeformationBakeOutcome.Invalid,
			}) {
				using var scope = new Fixture(deformationEnabled: true);
				scope.ApplyBake(outcome, reboundEligible: true);
				Assert.That(Stream(scope), Is.EqualTo(baseline), $"outcome {outcome}");
			}

			// a current Deformable bake whose descriptors are not rebound-eligible
			using var curved = new Fixture(deformationEnabled: true);
			curved.ApplyBake(RubberDeformationBakeOutcome.Deformable, reboundEligible: false);
			Assert.That(Stream(curved), Is.EqualTo(baseline), "curved spans stay untagged");

			// a stale bake, by version
			using var stale = new Fixture(deformationEnabled: true);
			stale.ApplyBake(RubberDeformationBakeOutcome.Deformable, reboundEligible: true,
				bakeVersion: RubberDeformationBakeFormat.BakeVersion - 1);
			Assert.That(Stream(stale), Is.EqualTo(baseline), "a stale bake falls back");

			// a kinematic rubber
			using var kinematic = new Fixture(deformationEnabled: true);
			kinematic.Collider._isKinematic = true;
			kinematic.ApplyBake(RubberDeformationBakeOutcome.Deformable, reboundEligible: true);
			Assert.That(Stream(kinematic), Is.EqualTo(baseline), "a kinematic rubber falls back");
		}

		[Test]
		public void LegacyModeShouldIgnoreTheToggleEntirely()
		{
			using var legacy = new Fixture(deformationEnabled: true);
			legacy.Collider.Mode = RubberColliderMode.Legacy;
			legacy.ApplyBake(RubberDeformationBakeOutcome.Deformable, reboundEligible: true);

			var transforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			var references = new ColliderReference(ref transforms, Allocator.Temp);
			try {
				new TestRubberApi(legacy.RubberObject).GenerateColliders(ref references);
				Assert.That(references.SweptCircleColliders.Length, Is.Zero);
				Assert.That(references.Count, Is.GreaterThan(0));
			} finally {
				references.Dispose();
				transforms.Dispose();
			}
		}

		[Test]
		public void ACurrentDeformableBakeShouldTagOnlyItsEligibleFreeSpans()
		{
			using var scope = new Fixture(deformationEnabled: true);
			scope.ApplyBake(RubberDeformationBakeOutcome.Deformable, reboundEligible: true);
			var collector = new RubberSpanReboundCollector();

			var transforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			var references = new ColliderReference(ref transforms, Allocator.Temp);
			try {
				new RubberPhysicalColliderGenerator(new RubberApi(scope.RubberObject, null, null),
					scope.Rubber, float4x4.identity,
					scope.Collider.GuideDistanceElasticitySettings, scope.Collider, collector)
					.GenerateColliders(0f, ref references, 0f);

				var tagged = 0;
				var untagged = 0;
				var previousU1 = new Dictionary<int, float>();
				for (var i = 0; i < references.SweptCircleColliders.Length; i++) {
					var collider = references.SweptCircleColliders[i];
					if (collider.SpanId < 0) {
						untagged++;
						continue;
					}
					tagged++;
					Assert.That(collider.U0, Is.InRange(0f, 1f));
					Assert.That(collider.U1, Is.InRange(0f, 1f));
					Assert.That(collider.U1, Is.GreaterThan(collider.U0));
					if (previousU1.TryGetValue(collider.SpanId, out var previous)) {
						Assert.That(collider.U0, Is.EqualTo(previous).Within(1e-5f),
							"the tags must cover the span without gaps");
					} else {
						Assert.That(collider.U0, Is.EqualTo(0f).Within(1e-6f));
					}
					previousU1[collider.SpanId] = collider.U1;
				}
				foreach (var last in previousU1.Values) {
					Assert.That(last, Is.EqualTo(1f).Within(1e-5f));
				}

				Assert.That(tagged, Is.GreaterThan(0));
				Assert.That(untagged, Is.GreaterThan(0), "supported arcs stay untagged");
				Assert.That(collector.Count, Is.EqualTo(previousU1.Count));
				var itemId = references.SweptCircleColliders[0].Header.ItemId;
				foreach (var entry in collector.Entries) {
					Assert.That(entry.ItemId, Is.EqualTo(itemId));
					Assert.That(entry.Record.ItemId, Is.EqualTo(itemId));
					Assert.That(entry.Record.IsUsable, Is.True);
					Assert.That(entry.Record.Length,
						Is.EqualTo(math.distance(entry.Record.AnchorA, entry.Record.AnchorB))
							.Within(1e-3f));
					Assert.That(math.dot(entry.Record.ChordDirection, entry.Record.OutwardNormal),
						Is.EqualTo(0f).Within(1e-4f));
					Assert.That(entry.Record.InstalledStretch,
						Is.EqualTo(Fixture.Stretch).Within(1e-3f));
					Assert.That(entry.Record.StressTable.Length,
						Is.EqualTo(RubberCompiledDeformationMaterial.LutSampleCount));
				}
			} finally {
				references.Dispose();
				transforms.Dispose();
			}
		}

		[Test]
		public void ACurvedFreeSpanShouldStayUntagged()
		{
			using var scope = new Fixture(deformationEnabled: true, curved: true);
			scope.ApplyBake(RubberDeformationBakeOutcome.Deformable, reboundEligible: false);
			var collector = new RubberSpanReboundCollector();

			var transforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			var references = new ColliderReference(ref transforms, Allocator.Temp);
			try {
				new RubberPhysicalColliderGenerator(new RubberApi(scope.RubberObject, null, null),
					scope.Rubber, float4x4.identity,
					scope.Collider.GuideDistanceElasticitySettings, scope.Collider, collector)
					.GenerateColliders(0f, ref references, 0f);

				Assert.That(references.SweptCircleColliders.Length, Is.GreaterThan(0));
				for (var i = 0; i < references.SweptCircleColliders.Length; i++) {
					Assert.That(references.SweptCircleColliders[i].SpanId, Is.EqualTo(-1));
				}
				Assert.That(collector.Count, Is.Zero);
			} finally {
				references.Dispose();
				transforms.Dispose();
			}
		}

		#endregion

		#region Registration

		[Test]
		public void RegistrationShouldAssignDenseIdsSortedByItemThenPathElement()
		{
			var record = RubberSpanReboundFixture.Record(RubberSpanReboundFixture.DefaultCurve);
			var collector = new RubberSpanReboundCollector();
			collector.Add(20, 0, With(record, 20, 5));
			collector.Add(20, 1, With(record, 20, 2));
			collector.Add(10, 0, With(record, 10, 7));

			var transforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			var colliders = new ColliderReference(ref transforms, Allocator.Temp);
			var kinematicTransforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			var kinematic = new ColliderReference(ref kinematicTransforms, Allocator.Temp, true);
			var spans = new NativeParallelHashMap<int, RubberSpanRebound>(0, Allocator.Temp);
			try {
				colliders.Add(Capsule(20, 0), float4x4.identity);
				colliders.Add(Capsule(20, 1), float4x4.identity);
				colliders.Add(Capsule(10, 0), float4x4.identity);
				colliders.Add(Capsule(10, 3), float4x4.identity); // no record: must be cleared
				colliders.Add(Capsule(10, -1), float4x4.identity);

				collector.Register(ref colliders, ref kinematic, ref spans);

				// item 20's spans sort by path element: element 2 (local 1) before element 5
				Assert.That(colliders.SweptCircleColliders[0].SpanId, Is.EqualTo(2));
				Assert.That(colliders.SweptCircleColliders[1].SpanId, Is.EqualTo(1));
				Assert.That(colliders.SweptCircleColliders[2].SpanId, Is.EqualTo(0));
				Assert.That(colliders.SweptCircleColliders[3].SpanId, Is.EqualTo(-1));
				Assert.That(colliders.SweptCircleColliders[4].SpanId, Is.EqualTo(-1));

				Assert.That(spans.Count(), Is.EqualTo(3));
				Assert.That(spans[0].ItemId, Is.EqualTo(10));
				Assert.That(spans[0].PathElementIndex, Is.EqualTo(7));
				Assert.That(spans[1].PathElementIndex, Is.EqualTo(2));
				Assert.That(spans[2].PathElementIndex, Is.EqualTo(5));
			} finally {
				spans.Dispose();
				kinematic.Dispose();
				kinematicTransforms.Dispose();
				colliders.Dispose();
				transforms.Dispose();
			}
		}

		[Test]
		public void ATaggedCapsuleShouldTakeTheCompliantPathOnlyWithARegisteredSpan()
		{
			var record = RubberSpanReboundFixture.Record(RubberSpanReboundFixture.DefaultCurve);
			var transforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			var references = new ColliderReference(ref transforms, Allocator.Temp);
			var spans = new NativeParallelHashMap<int, RubberSpanRebound>(1, Allocator.Temp);
			var hitEvents = new NativeQueue<EventData>(Allocator.Temp);
			try {
				var length = (float)RubberSpanReboundFixture.LengthVpx;
				references.Add(new SweptCircleCollider(float3.zero, new float3(length, 0f, 0f),
					4f, new ColliderInfo {
						ItemId = 1,
						Material = new PhysicsMaterialData { Elasticity = 1f },
					}, 1f, 0, 0f, 1f), float4x4.identity);
				using var native = new NativeColliders(ref references, Allocator.Temp);
				var writer = hitEvents.AsParallelWriter();

				// oblique hit at a quarter of the span, once without and once with the record
				var collEvent = ObliqueEvent();
				var wallState = new PhysicsState { Colliders = native, RubberSpanRebounds = spans };
				var wallBall = ObliqueBall(0.25f, length);
				native.SweptCircle(0).Collide(ref wallBall, ref writer, in collEvent, ref wallState);

				spans.Add(0, record);
				var compliantState = new PhysicsState {
					Colliders = native,
					RubberSpanRebounds = spans,
				};
				var compliantBall = ObliqueBall(0.25f, length);
				native.SweptCircle(0).Collide(ref compliantBall, ref writer, in collEvent,
					ref compliantState);

				TestContext.Out.WriteLine(
					$"branch|wall={wallBall.Velocity}|compliant={compliantBall.Velocity}");
				Assert.That(math.distance(wallBall.Velocity, compliantBall.Velocity),
					Is.GreaterThan(0.5f), "the compliant branch must change the response");
				Assert.That(native.SweptCircle(0).ContactFraction(in compliantBall),
					Is.EqualTo(0.25f).Within(1e-3f));
			} finally {
				hitEvents.Dispose();
				spans.Dispose();
				references.Dispose();
				transforms.Dispose();
			}
		}

		[Test]
		public void TagsShouldSurviveBothTransformOverloads()
		{
			var source = new SweptCircleCollider(new float3(-10f, 0f, 0f),
				new float3(10f, 0f, 0f), 2f, new ColliderInfo { ItemId = 1 }, 0.35f, 7,
				0.25f, 0.75f);

			var returned = source.Transform(float4x4.Translate(new float3(1f, 2f, 3f)));
			var copied = default(SweptCircleCollider);
			copied.Transform(source, float4x4.Translate(new float3(1f, 2f, 3f)));

			Assert.That(returned.SpanId, Is.EqualTo(7));
			Assert.That(returned.U0, Is.EqualTo(0.25f));
			Assert.That(returned.U1, Is.EqualTo(0.75f));
			Assert.That(copied.SpanId, Is.EqualTo(7));
			Assert.That(copied.U0, Is.EqualTo(0.25f));
			Assert.That(copied.U1, Is.EqualTo(0.75f));
		}

		#endregion

		#region Helpers

		private static RubberSpanRebound With(RubberSpanRebound record, int itemId,
			int pathElementIndex)
		{
			record.ItemId = itemId;
			record.PathElementIndex = pathElementIndex;
			return record;
		}

		private static SweptCircleCollider Capsule(int itemId, int spanId)
			=> new SweptCircleCollider(new float3(-10f, 0f, 0f), new float3(10f, 0f, 0f), 2f,
				new ColliderInfo { ItemId = itemId }, 1f, spanId, 0f, 1f);

		private static BallState ObliqueBall(float u, float length)
		{
			return new BallState {
				Id = 1,
				Position = new float3(u * length, 29f, 0f),
				Velocity = new float3(9f, -15.6f, 0f),
				Radius = 25f,
				Mass = 1f,
			};
		}

		private static CollisionEventData ObliqueEvent()
			=> new CollisionEventData {
				ColliderId = 0,
				HitNormal = new float3(0f, 1f, 0f),
				HitDistance = 0f,
			};

		private static string[] Stream(Fixture fixture)
		{
			var transforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			var references = new ColliderReference(ref transforms, Allocator.Temp);
			try {
				new TestRubberApi(fixture.RubberObject).GenerateColliders(ref references);
				var lines = new List<string>();
				for (var i = 0; i < references.SweptCircleColliders.Length; i++) {
					var collider = references.SweptCircleColliders[i];
					lines.Add($"{collider.V1} {collider.V2} {collider.CordRadius} {collider.ElasticityScale} span={collider.SpanId} u={collider.U0}..{collider.U1}");
				}
				return lines.ToArray();
			} finally {
				references.Dispose();
				transforms.Dispose();
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

		/// <summary>
		/// A three-anchor Physical rubber whose free spans are straight, with the bake
		/// written directly so the runtime side can be exercised without the baker.
		/// </summary>
		private sealed class Fixture : IDisposable
		{
			internal const float Stretch = 1.1f;
			internal const float Thickness = 8f;

			internal readonly GameObject RubberObject;
			internal readonly RubberComponent Rubber;
			internal readonly RubberColliderComponent Collider;

			private readonly GameObject _guideObject;
			private readonly RubberPathElement[] _path;

			internal Fixture(bool deformationEnabled, bool curved = false)
			{
				_path = curved ? CurvedPath() : StraightPath();
				_guideObject = new GameObject("Rebound Guide");
				var guide = _guideObject.AddComponent<RubberGuideComponent>();

				RubberObject = new GameObject("Rebound Rubber");
				RubberObject.SetActive(false);
				Rubber = RubberObject.AddComponent<RubberComponent>();
				Rubber._thickness = (int)Thickness;
				// drag points so the Legacy generator has a spline of its own
				Rubber.DragPoints = new[] {
					new VisualPinball.Engine.Math.DragPointData(-75f, 0f),
					new VisualPinball.Engine.Math.DragPointData(75f, 0f),
					new VisualPinball.Engine.Math.DragPointData(60f, 40f),
					new VisualPinball.Engine.Math.DragPointData(-60f, 40f),
				};
				Rubber.SetGuideBindings(new[] {
					new RubberGuideBinding(guide, guide.Slots[0].Id),
				});
				Rubber.ApplyGuidedBake(_path, null, Matrix4x4.identity, default, 1);
				Collider = RubberObject.AddComponent<RubberColliderComponent>();
				Collider.Mode = RubberColliderMode.Physical;
				Collider.DeformationEnabled = deformationEnabled;
				Collider.Elasticity = 0.8f;
				Collider.Friction = 0.3f;
				if (!curved) {
					Assert.That(Collider.CanUsePhysical, Is.True,
						"the fixture path must be usable");
				}
			}

			internal void ApplyBake(RubberDeformationBakeOutcome outcome, bool reboundEligible,
				int bakeVersion = RubberDeformationBakeFormat.BakeVersion)
			{
				var spans = new List<RubberSpanDescriptor>();
				if (outcome == RubberDeformationBakeOutcome.Deformable) {
					for (var i = 0; i < _path.Length; i++) {
						if (_path[i].Type != RubberPathElementType.FreeSpan) {
							continue;
						}
						spans.Add(new RubberSpanDescriptor {
							PathElementIndex = i,
							InstalledLength = _path[i].Length,
							UnloadedLength = _path[i].Length / Stretch,
							MaxDeflection = 60f,
							SurfaceFriction = 0.3f,
							InwardSign = 1,
							SegmentCount = 8,
							ArcLengthFractions = new[] { 0f, 1f },
							ReboundEligible = reboundEligible,
						});
					}
				}
				Collider.ApplyDeformationBake(Material(), spans.ToArray(),
					Array.Empty<RubberBackedResponseDescriptor>(), outcome, "test bake",
					bakeVersion, new string('a', 64));
			}

			private static RubberCompiledDeformationMaterial Material()
			{
				var count = RubberCompiledDeformationMaterial.LutSampleCount;
				var stress = new float[count];
				var tangent = new float[count];
				var table = RubberSpanReboundFixture.StressTable(
					RubberSpanReboundFixture.DefaultCurve);
				for (var i = 0; i < count; i++) {
					stress[i] = (float)table[i];
					tangent[i] = 1f;
				}
				return new RubberCompiledDeformationMaterial {
					CompilerVersion = RubberDeformationBakeFormat.CompilerVersion,
					LinearDensity = 0.1f,
					DampingRatio = 0.08f,
					SurfaceFriction = 0.3f,
					MaximumStretchRatio = 2f,
					StressLut = stress,
					TangentStiffnessLut = tangent,
				};
			}

			public void Dispose()
			{
				Object.DestroyImmediate(RubberObject);
				Object.DestroyImmediate(_guideObject);
			}
		}

		private static RubberPathElement[] StraightPath()
		{
			return Close(new[] {
				Straight(new float2(-75f, 0f), new float2(75f, 0f)),
				Arc(new float2(75f, 0f), new float2(60f, 40f)),
				Straight(new float2(60f, 40f), new float2(-60f, 40f)),
				Arc(new float2(-60f, 40f), new float2(-75f, 0f)),
			});
		}

		private static RubberPathElement[] CurvedPath()
		{
			return Close(new[] {
				Curved(new float2(-75f, 0f), new float2(75f, 0f)),
				Arc(new float2(75f, 0f), new float2(60f, 40f)),
				Curved(new float2(60f, 40f), new float2(-60f, 40f)),
				Arc(new float2(-60f, 40f), new float2(-75f, 0f)),
			});
		}

		private static RubberPathElement Straight(float2 start, float2 end)
		{
			return new RubberPathElement {
				Type = RubberPathElementType.FreeSpan,
				Start = start,
				End = end,
				Control1 = math.lerp(start, end, 1f / 3f),
				Control2 = math.lerp(start, end, 2f / 3f),
			};
		}

		private static RubberPathElement Curved(float2 start, float2 end)
		{
			var delta = end - start;
			var normal = new float2(-delta.y, delta.x) * 0.25f;
			return new RubberPathElement {
				Type = RubberPathElementType.FreeSpan,
				Start = start,
				End = end,
				Control1 = math.lerp(start, end, 1f / 3f) + normal,
				Control2 = math.lerp(start, end, 2f / 3f) + normal,
			};
		}

		private static RubberPathElement Arc(float2 start, float2 end)
		{
			var center = (start + end) * 0.5f;
			var radius = math.distance(start, end) * 0.5f;
			return new RubberPathElement {
				Type = RubberPathElementType.SupportedArc,
				Start = start,
				End = end,
				Center = center,
				Radius = radius,
				StartAngleRad = math.atan2(start.y - center.y, start.x - center.x),
				SweepAngleRad = math.PI,
			};
		}

		private static RubberPathElement[] Close(RubberPathElement[] elements)
		{
			var distance = 0f;
			for (var i = 0; i < elements.Length; i++) {
				elements[i].Length = elements[i].Type == RubberPathElementType.SupportedArc
					? math.abs(elements[i].SweepAngleRad) * elements[i].Radius
					: RubberPathMath.FreeSpanLength(elements[i]);
				elements[i].StartDistance = distance;
				distance += elements[i].Length;
			}
			return elements;
		}

		#endregion
	}
}
