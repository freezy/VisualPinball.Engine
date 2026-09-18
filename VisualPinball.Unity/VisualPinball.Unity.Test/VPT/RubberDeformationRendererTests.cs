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
using System.Linq;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Test
{
	/// <summary>
	/// Section 4 of the static-rebound plan, the renderer: the V profile, the vertex
	/// binding of a synthetic tube around a known straight span, the displacement, the
	/// twang decay with its age gate, the kick overriding the twang, and the packable.
	/// </summary>
	public class RubberDeformationRendererTests
	{
		private const int Thickness = 8;

		/// <summary>Length of every synthetic span, in VPX units.</summary>
		private const float SpanLengthVpx = 100f;

		/// <summary>Distance between the parallel synthetic spans, in VPX units.</summary>
		private const float SpanSpacingVpx = 50f;

		/// <summary>Ring positions of the synthetic tube, as arc-length fractions.</summary>
		private static readonly float[] RingFractions = { 0f, 0.25f, 0.5f, 0.75f, 1f };

		#region Profile

		[Test]
		public void ShouldEvaluateTheVProfileAtItsApexAndItsPinnedEnds()
		{
			Assert.That(RubberDeformationRenderer.ProfileAt(0f, 0.5f, 6f), Is.EqualTo(0f));
			Assert.That(RubberDeformationRenderer.ProfileAt(1f, 0.5f, 6f), Is.EqualTo(0f));
			Assert.That(RubberDeformationRenderer.ProfileAt(0.5f, 0.5f, 6f),
				Is.EqualTo(6f).Within(1e-5f));
			Assert.That(RubberDeformationRenderer.ProfileAt(0.25f, 0.25f, -4f),
				Is.EqualTo(-4f).Within(1e-5f));
			// halfway up the short leg and halfway down the long one
			Assert.That(RubberDeformationRenderer.ProfileAt(0.125f, 0.25f, 8f),
				Is.EqualTo(4f).Within(1e-5f));
			Assert.That(RubberDeformationRenderer.ProfileAt(0.625f, 0.25f, 8f),
				Is.EqualTo(4f).Within(1e-5f));
		}

		[Test]
		public void ShouldRiseAndFallMonotonicallyAroundTheApex()
		{
			const float apex = 0.35f;
			const float depth = 5f;
			var previous = float.NegativeInfinity;
			for (var i = 0; i <= 35; i++) {
				var value = RubberDeformationRenderer.ProfileAt(i / 100f, apex, depth);
				Assert.That(value, Is.GreaterThanOrEqualTo(previous));
				previous = value;
			}
			previous = float.PositiveInfinity;
			for (var i = 35; i <= 100; i++) {
				var value = RubberDeformationRenderer.ProfileAt(i / 100f, apex, depth);
				Assert.That(value, Is.LessThanOrEqualTo(previous));
				previous = value;
			}
		}

		[Test]
		public void ShouldClampTheApexIntoTheEndBlendInterval()
		{
			// an apex outside [0.02, 0.98] is clamped, so the profile never divides by zero
			Assert.That(RubberDeformationRenderer.ProfileAt(0.5f, 0f, 6f),
				Is.EqualTo(RubberDeformationRenderer.ProfileAt(0.5f,
					RubberDeformationRenderer.MinimumApex, 6f)));
			Assert.That(RubberDeformationRenderer.ProfileAt(0.5f, 1f, 6f),
				Is.EqualTo(RubberDeformationRenderer.ProfileAt(0.5f,
					RubberDeformationRenderer.MaximumApex, 6f)));
			Assert.That(RubberDeformationRenderer.ProfileAt(float.NaN, 0.5f, 6f),
				Is.EqualTo(0f));
		}

		#endregion

		#region Binding

		[Test]
		public void ShouldBindTheTubeVerticesOfAStraightSpan()
		{
			using var fixture = Fixture.Create();
			Assert.That(fixture.Renderer.SpanCount, Is.EqualTo(1));
			// every ring vertex is inside one cord diameter, the far pair is not
			Assert.That(fixture.Renderer.BindingCount,
				Is.EqualTo(RingFractions.Length * 4));

			foreach (var binding in fixture.Renderer.Bindings) {
				Assert.That(binding.SpanIndex, Is.Zero);
				var expected = RingFractions[binding.VertexIndex / 4];
				Assert.That(binding.Alpha, Is.EqualTo(expected).Within(1e-4f),
					$"vertex {binding.VertexIndex} sits at arc-length fraction {expected}");
				// the inward direction of a bake-plane +y normal is -z in mesh space
				Assert.That((Vector3)binding.Inward,
					Is.EqualTo(new Vector3(0f, 0f, -1f)).Using(Vector3Comparer));
			}

			var bound = new HashSet<int>(fixture.Renderer.Bindings.Select(b => b.VertexIndex));
			for (var i = RingFractions.Length * 4; i < fixture.RestVertices.Length; i++) {
				Assert.That(bound.Contains(i), Is.False,
					$"vertex {i} is more than one cord diameter away and must stay free");
			}
		}

		[Test]
		public void ShouldBindEveryVertexToItsNearestChord()
		{
			using var fixture = Fixture.Create(spanCount: 2);
			Assert.That(fixture.Renderer.SpanCount, Is.EqualTo(2));
			var ringVertices = RingFractions.Length * 4;
			foreach (var binding in fixture.Renderer.Bindings) {
				var expectedSpan = binding.VertexIndex / ringVertices;
				Assert.That(binding.SpanIndex, Is.EqualTo(expectedSpan),
					$"vertex {binding.VertexIndex} belongs to span {expectedSpan}");
			}
			Assert.That(fixture.Renderer.BindingCount, Is.EqualTo(ringVertices * 2));
		}

		[Test]
		public void ShouldRebuildTheBindingsWhenTheDescriptorsChange()
		{
			using var fixture = Fixture.Create();
			Assert.That(fixture.Renderer.BindingCount, Is.GreaterThan(0));

			fixture.ApplyBake(Array.Empty<RubberSpanDescriptor>());
			fixture.Renderer.RenderFrame();
			Assert.That(fixture.Renderer.SpanCount, Is.Zero);
			Assert.That(fixture.Renderer.BindingCount, Is.Zero);
		}

		#endregion

		#region Displacement

		[Test]
		public void ShouldDisplaceBoundVerticesInwardOnAnImpact()
		{
			using var fixture = Fixture.Create();
			fixture.ClockMs = 1000f;
			fixture.Renderer.ApplyImpact(0, 0.5f, 6f, 1000f);
			fixture.Renderer.RenderFrame();

			// inward is -z in mesh space, so an inward-positive profile moves -z
			AssertRingOffset(fixture, ring: 2, inwardVpx: 6f);
			AssertRingOffset(fixture, ring: 1, inwardVpx: 3f);
			AssertRingOffset(fixture, ring: 0, inwardVpx: 0f);
			AssertRingOffset(fixture, ring: 4, inwardVpx: 0f);
		}

		[Test]
		public void ShouldDisplaceBoundVerticesOutwardOnAKick()
		{
			using var fixture = Fixture.Create();
			fixture.ClockMs = 1000f;
			fixture.Renderer.PlayKick(0, 0.5f, 16f);
			// half the duration, where the default curve peaks at one
			fixture.ClockMs = 1000f + fixture.Renderer.AnimationDurationMs * 0.5f;
			fixture.Renderer.RenderFrame();

			AssertRingOffset(fixture, ring: 2, inwardVpx: -16f);
			AssertRingOffset(fixture, ring: 3, inwardVpx: -8f);
		}

		[Test]
		public void ShouldTranslateEveryCrossSectionRigidly()
		{
			using var fixture = Fixture.Create();
			fixture.ClockMs = 1000f;
			fixture.Renderer.ApplyImpact(0, 0.5f, 6f, 1000f);
			fixture.Renderer.RenderFrame();

			var vertices = fixture.Renderer.InstancedMesh.vertices;
			for (var ring = 0; ring < RingFractions.Length; ring++) {
				var reference = vertices[ring * 4] - fixture.RestVertices[ring * 4];
				for (var corner = 1; corner < 4; corner++) {
					var index = ring * 4 + corner;
					Assert.That(vertices[index] - fixture.RestVertices[index],
						Is.EqualTo(reference).Using(Vector3Comparer),
						$"ring {ring} must translate rigidly");
				}
			}
		}

		[Test]
		public void ShouldLeaveUnboundVerticesWhereTheyAre()
		{
			using var fixture = Fixture.Create();
			fixture.ClockMs = 1000f;
			fixture.Renderer.ApplyImpact(0, 0.5f, 6f, 1000f);
			fixture.Renderer.RenderFrame();

			var vertices = fixture.Renderer.InstancedMesh.vertices;
			for (var i = RingFractions.Length * 4; i < fixture.RestVertices.Length; i++) {
				Assert.That(vertices[i], Is.EqualTo(fixture.RestVertices[i]).Using(Vector3Comparer));
			}
		}

		[Test]
		public void ShouldKeepTheSharedMeshUntouchedAndRestoreItOnRelease()
		{
			using var fixture = Fixture.Create();
			var shared = fixture.SharedMesh;
			Assert.That(fixture.Renderer.InstancedMesh, Is.Not.SameAs(shared));
			Assert.That(fixture.MeshFilter.sharedMesh,
				Is.SameAs(fixture.Renderer.InstancedMesh));

			fixture.ClockMs = 1000f;
			fixture.Renderer.ApplyImpact(0, 0.5f, 6f, 1000f);
			fixture.Renderer.RenderFrame();
			Assert.That(shared.vertices, Is.EqualTo(fixture.RestVertices).AsCollection);

			fixture.Renderer.ReleaseMesh();
			Assert.That(fixture.MeshFilter.sharedMesh, Is.SameAs(shared));
		}

		#endregion

		#region Twang and kick

		[Test]
		public void ShouldDecayTheTwangWithItsTimeConstant()
		{
			using var fixture = Fixture.Create();
			fixture.ClockMs = 1000f;
			fixture.Renderer.ApplyImpact(0, 0.4f, 6f, 1000f);
			fixture.Renderer.RenderFrame();
			Assert.That(fixture.Depth(), Is.EqualTo(6f).Within(1e-4f));
			Assert.That(fixture.Apex(), Is.EqualTo(0.4f).Within(1e-5f));

			fixture.ClockMs = 1000f + RubberDeformationRenderer.TwangTimeConstantMs;
			fixture.Renderer.RenderFrame();
			Assert.That(fixture.Depth(), Is.EqualTo(6f / Mathf.Exp(1f)).Within(1e-3f));

			fixture.ClockMs = 1000f + 4f * RubberDeformationRenderer.TwangTimeConstantMs;
			fixture.Renderer.RenderFrame();
			Assert.That(fixture.Depth(), Is.EqualTo(6f * Mathf.Exp(-4f)).Within(1e-3f));

			// far enough out it stops entirely and the mesh returns to rest
			fixture.ClockMs = 1000f + 1000f;
			fixture.Renderer.RenderFrame();
			Assert.That(fixture.Depth(), Is.EqualTo(0f));
			var vertices = fixture.Renderer.InstancedMesh.vertices;
			Assert.That(vertices, Is.EqualTo(fixture.RestVertices).AsCollection);
		}

		[Test]
		public void ShouldRestartTheTwangOnEveryImpact()
		{
			using var fixture = Fixture.Create();
			fixture.ClockMs = 1000f;
			fixture.Renderer.ApplyImpact(0, 0.4f, 6f, 1000f);

			fixture.ClockMs = 1080f;
			fixture.Renderer.RenderFrame();
			Assert.That(fixture.Depth(), Is.LessThan(1f));

			fixture.Renderer.ApplyImpact(0, 0.7f, 4f, 1080f);
			fixture.Renderer.RenderFrame();
			Assert.That(fixture.Depth(), Is.EqualTo(4f).Within(1e-4f));
			Assert.That(fixture.Apex(), Is.EqualTo(0.7f).Within(1e-5f));
		}

		[Test]
		public void ShouldDropImpactsOlderThanTheAgeGate()
		{
			using var fixture = Fixture.Create();
			fixture.ClockMs = 1200f;

			// 150 ms of simulation time late
			fixture.Renderer.ApplyImpact(0, 0.5f, 6f, 1050f);
			fixture.Renderer.RenderFrame();
			Assert.That(fixture.Renderer.DroppedImpactCount, Is.EqualTo(1));
			Assert.That(fixture.Depth(), Is.EqualTo(0f));

			// 90 ms late, still inside the gate, and already partly decayed
			fixture.Renderer.ApplyImpact(0, 0.5f, 6f, 1110f);
			fixture.Renderer.RenderFrame();
			Assert.That(fixture.Renderer.DroppedImpactCount, Is.EqualTo(1));
			Assert.That(fixture.Depth(), Is.EqualTo(6f * Mathf.Exp(-90f / 40f)).Within(1e-3f));
		}

		[Test]
		public void ShouldLetTheKickReplaceTheTwangWhileItPlays()
		{
			using var fixture = Fixture.Create();
			fixture.ClockMs = 1000f;
			fixture.Renderer.ApplyImpact(0, 0.5f, 6f, 1000f);
			fixture.Renderer.PlayKick(0, 0.5f, 16f);

			fixture.ClockMs = 1030f;
			fixture.Renderer.RenderFrame();
			// signed, never summed: the kick alone, outward
			Assert.That(fixture.Depth(), Is.EqualTo(-16f).Within(1e-3f));

			// an impact during the animation does not add to it
			fixture.Renderer.ApplyImpact(0, 0.5f, 5f, 1030f);
			fixture.Renderer.RenderFrame();
			Assert.That(fixture.Depth(), Is.EqualTo(-16f).Within(1e-3f));

			// once the animation is over the twang shows through again
			fixture.ClockMs = 1070f;
			fixture.Renderer.RenderFrame();
			Assert.That(fixture.Depth(), Is.GreaterThan(0f));
			Assert.That(fixture.Depth(), Is.LessThan(5f));
		}

		[Test]
		public void ShouldNotWriteTheMeshBelowTheUpdateThreshold()
		{
			using var fixture = Fixture.Create();
			fixture.Renderer.UpdateThreshold = 2f;
			fixture.ClockMs = 1000f;
			fixture.Renderer.ApplyImpact(0, 0.5f, 6f, 1000f);
			fixture.Renderer.RenderFrame();
			var written = fixture.Renderer.InstancedMesh.vertices[8];

			// one millisecond later the depth moved by far less than the threshold
			fixture.ClockMs = 1001f;
			fixture.Renderer.RenderFrame();
			Assert.That(fixture.Renderer.InstancedMesh.vertices[8],
				Is.EqualTo(written).Using(Vector3Comparer));
		}

		#endregion

		#region Packaging

		[Test]
		public void ShouldRoundTripTheRendererIncludingItsKickCurve()
		{
			var sourceObject = new GameObject("Source Renderer");
			var targetObject = new GameObject("Target Renderer");
			try {
				var source = sourceObject.AddComponent<RubberDeformationRenderer>();
				source.UpdateThreshold = 0.25f;
				source.RecalculateNormals = false;
				source.IncludeSupportedArcs = true;
				source.AnimationDurationMs = 123f;
				source.KickCurve = new AnimationCurve(
					new Keyframe(0f, 0f, 1f, 2f),
					new Keyframe(0.4f, 0.8f, 3f, 4f),
					new Keyframe(1f, 0f, 5f, 6f)
				) {
					preWrapMode = WrapMode.PingPong,
					postWrapMode = WrapMode.Loop,
				};

				var target = targetObject.AddComponent<RubberDeformationRenderer>();
				target.Unpack(source.Pack());

				Assert.That(target.UpdateThreshold, Is.EqualTo(0.25f));
				Assert.That(target.RecalculateNormals, Is.False);
				Assert.That(target.IncludeSupportedArcs, Is.True);
				Assert.That(target.AnimationDurationMs, Is.EqualTo(123f));
				Assert.That(target.KickCurve.keys.Length, Is.EqualTo(3));
				for (var i = 0; i < 3; i++) {
					var expected = source.KickCurve.keys[i];
					var actual = target.KickCurve.keys[i];
					Assert.That(actual.time, Is.EqualTo(expected.time).Within(1e-6f));
					Assert.That(actual.value, Is.EqualTo(expected.value).Within(1e-6f));
					Assert.That(actual.inTangent, Is.EqualTo(expected.inTangent).Within(1e-6f));
					Assert.That(actual.outTangent, Is.EqualTo(expected.outTangent).Within(1e-6f));
				}
				// WrapMode.Clamp and WrapMode.Once share a value, so compare the round trip
				// against the source curve instead of a literal
				Assert.That(target.KickCurve.preWrapMode,
					Is.EqualTo(source.KickCurve.preWrapMode));
				Assert.That(target.KickCurve.postWrapMode,
					Is.EqualTo(source.KickCurve.postWrapMode));
				Assert.That(RubberDeformationRenderer.RuntimeAvailable, Is.True);
			} finally {
				Object.DestroyImmediate(sourceObject);
				Object.DestroyImmediate(targetObject);
			}
		}

		[Test]
		public void ShouldRestoreTheAnimationDefaultsFromAVersionOnePayload()
		{
			var gameObject = new GameObject("Renderer");
			try {
				var renderer = gameObject.AddComponent<RubberDeformationRenderer>();
				renderer.AnimationDurationMs = 500f;
				renderer.KickCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

				var legacy = PackageApi.Packer.Pack(new RubberDeformationRendererPackable {
					Version = 1,
					UpdateThreshold = 0.25f,
					RecalculateNormals = false,
					IncludeSupportedArcs = true,
				});
				renderer.Unpack(legacy);

				Assert.That(renderer.UpdateThreshold, Is.EqualTo(0.25f));
				Assert.That(renderer.RecalculateNormals, Is.False);
				Assert.That(renderer.IncludeSupportedArcs, Is.True);
				Assert.That(renderer.AnimationDurationMs,
					Is.EqualTo(RubberDeformationRenderer.DefaultAnimationDurationMs));
				var defaults = RubberDeformationRenderer.CreateDefaultKickCurve();
				Assert.That(renderer.KickCurve.keys.Length, Is.EqualTo(defaults.keys.Length));
				Assert.That(renderer.KickCurve.Evaluate(0f), Is.EqualTo(0f).Within(1e-6f));
				Assert.That(renderer.KickCurve.Evaluate(0.5f), Is.EqualTo(1f).Within(1e-6f));
				Assert.That(renderer.KickCurve.Evaluate(1f), Is.EqualTo(0f).Within(1e-6f));

				renderer.Unpack(null);
				Assert.That(renderer.AnimationDurationMs,
					Is.EqualTo(RubberDeformationRenderer.DefaultAnimationDurationMs));
			} finally {
				Object.DestroyImmediate(gameObject);
			}
		}

		#endregion

		#region Fixture

		private static readonly Vector3EqualityComparer Vector3Comparer = new(1e-6f);

		private sealed class Vector3EqualityComparer : IEqualityComparer<Vector3>
		{
			private readonly float _tolerance;

			internal Vector3EqualityComparer(float tolerance) => _tolerance = tolerance;

			public bool Equals(Vector3 left, Vector3 right)
				=> Vector3.Distance(left, right) <= _tolerance;

			public int GetHashCode(Vector3 value) => 0;
		}

		/// <summary>
		/// Expected mesh-space offset of a whole ring, given the profile value in VPX
		/// units, inward positive. Inward is the bake plane's +y, which is -z in mesh
		/// space, so an inward deflection moves the ring towards -z.
		/// </summary>
		private static void AssertRingOffset(Fixture fixture, int ring, float inwardVpx)
		{
			var vertices = fixture.Renderer.InstancedMesh.vertices;
			var expected = new Vector3(0f, 0f, -Physics.ScaleToWorld(inwardVpx));
			for (var corner = 0; corner < 4; corner++) {
				var index = ring * 4 + corner;
				Assert.That(vertices[index] - fixture.RestVertices[index],
					Is.EqualTo(expected).Using(Vector3Comparer),
					$"ring {ring}, corner {corner}");
			}
		}

		/// <summary>
		/// A rubber with an identity bake frame, one or two straight free spans along
		/// the bake plane's x axis, and a synthetic tube of rings around them.
		/// </summary>
		private sealed class Fixture : IDisposable
		{
			internal GameObject GameObject;
			internal RubberComponent Rubber;
			internal RubberColliderComponent Collider;
			internal MeshFilter MeshFilter;
			internal Mesh SharedMesh;
			internal RubberDeformationRenderer Renderer;
			internal Vector3[] RestVertices;
			internal float ClockMs;

			internal static Fixture Create(int spanCount = 1)
			{
				var fixture = new Fixture();
				fixture.GameObject = new GameObject("Deforming Rubber");
				// inactive, so the rubber's Awake does not look for a player
				fixture.GameObject.SetActive(false);

				fixture.Rubber = fixture.GameObject.AddComponent<RubberComponent>();
				fixture.Rubber._thickness = Thickness;
				fixture.Rubber.SetGuideBindings(Array.Empty<RubberGuideBinding>());
				fixture.Rubber.ApplyGuidedBake(BuildPath(spanCount), null,
					Matrix4x4.identity, default, 1);

				fixture.Collider = fixture.GameObject.AddComponent<RubberColliderComponent>();
				fixture.Collider.Mode = RubberColliderMode.Physical;
				fixture.Collider.DeformationEnabled = true;

				fixture.MeshFilter = fixture.GameObject.AddComponent<MeshFilter>();
				fixture.SharedMesh = BuildTube(spanCount, out fixture.RestVertices);
				fixture.MeshFilter.sharedMesh = fixture.SharedMesh;

				fixture.Renderer = fixture.GameObject
					.AddComponent<RubberDeformationRenderer>();
				fixture.Renderer.RecalculateNormals = false;
				fixture.Renderer.UpdateThreshold = 0f;
				fixture.Renderer.ClockMsOverride = () => fixture.ClockMs;

				fixture.ApplyBake(BuildDescriptors(spanCount));
				fixture.Renderer.Acquire();
				fixture.Renderer.AcquireMesh();
				fixture.Renderer.RebuildBindings();
				return fixture;
			}

			internal void ApplyBake(RubberSpanDescriptor[] descriptors)
				=> Collider.ApplyDeformationBake(default, descriptors,
					Array.Empty<RubberBackedResponseDescriptor>(),
					RubberDeformationBakeOutcome.Deformable, "renderer fixture",
					RubberDeformationBakeFormat.BakeVersion, new string('a', 64));

			internal float Depth()
			{
				Assert.That(Renderer.TryGetProfile(0, out _, out var depth), Is.True);
				return depth;
			}

			internal float Apex()
			{
				Assert.That(Renderer.TryGetProfile(0, out var apex, out _), Is.True);
				return apex;
			}

			public void Dispose()
			{
				if (Renderer) {
					Renderer.ReleaseMesh();
				}
				if (SharedMesh) {
					Object.DestroyImmediate(SharedMesh);
				}
				if (GameObject) {
					Object.DestroyImmediate(GameObject);
				}
			}

			/// <summary>Straight free spans parallel to the bake plane's x axis.</summary>
			private static RubberPathElement[] BuildPath(int spanCount)
			{
				var path = new RubberPathElement[spanCount];
				for (var i = 0; i < spanCount; i++) {
					var y = SpanSpacingVpx * i;
					var start = new float2(0f, y);
					var end = new float2(SpanLengthVpx, y);
					path[i] = new RubberPathElement {
						Type = RubberPathElementType.FreeSpan,
						Start = start,
						End = end,
						Control1 = start + (end - start) / 3f,
						Control2 = start + (end - start) * (2f / 3f),
						Length = SpanLengthVpx,
					};
				}
				return path;
			}

			private static RubberSpanDescriptor[] BuildDescriptors(int spanCount)
			{
				var descriptors = new RubberSpanDescriptor[spanCount];
				for (var i = 0; i < spanCount; i++) {
					descriptors[i] = new RubberSpanDescriptor {
						PathElementIndex = i,
						InstalledLength = SpanLengthVpx,
						UnloadedLength = SpanLengthVpx / 1.1f,
						InwardSign = 1,
						SegmentCount = 8,
						ArcLengthFractions = new[] { 0f, 1f },
						ReboundEligible = true,
					};
				}
				return descriptors;
			}

			/// <summary>
			/// Four vertices per ring, at the cord radius around the chord, plus two
			/// vertices three radii out that must stay unbound.
			/// </summary>
			private static Mesh BuildTube(int spanCount, out Vector3[] vertices)
			{
				var radius = Physics.ScaleToWorld(Thickness * 0.5f);
				var list = new List<Vector3>();
				for (var span = 0; span < spanCount; span++) {
					foreach (var fraction in RingFractions) {
						var centre = MeshPoint(SpanLengthVpx * fraction, SpanSpacingVpx * span);
						list.Add(centre + new Vector3(0f, radius, 0f));
						list.Add(centre + new Vector3(0f, -radius, 0f));
						list.Add(centre + new Vector3(0f, 0f, radius));
						list.Add(centre + new Vector3(0f, 0f, -radius));
					}
				}
				var far = MeshPoint(SpanLengthVpx * 0.5f, 0f);
				list.Add(far + new Vector3(0f, radius * 3f, 0f));
				list.Add(far + new Vector3(0f, 0f, radius * 3f));

				vertices = list.ToArray();
				var mesh = new Mesh { name = "Synthetic Cord" };
				mesh.SetVertices(vertices);
				return mesh;
			}

			/// <summary>A bake-plane point in mesh space, the rubber's local Unity space.</summary>
			private static Vector3 MeshPoint(float bakeX, float bakeY)
				=> new Vector3(bakeX, bakeY, 0f).TranslateToWorld();
		}

		#endregion
	}
}
