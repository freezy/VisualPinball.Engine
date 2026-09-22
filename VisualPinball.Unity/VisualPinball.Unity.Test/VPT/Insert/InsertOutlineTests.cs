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

namespace VisualPinball.Unity.Test
{
	public class InsertOutlineTests
	{
		/// <summary>
		/// A thousandth of a VPX unit, which is well under a micron of real size.
		/// </summary>
		private const float Tolerance = 1e-3f;

		private static readonly float Inch = InsertShapes.VpxUnitsPerInch;

		/// <summary>
		/// One fixture: the size to expect, and a factory for the sharp corners.
		/// Keeping the shape behind a delegate lets every invariant below run
		/// against all shape types instead of just the one it was written for.
		/// </summary>
		public class OutlineCase
		{
			public string Name;
			public float Width;
			public float Height;

			/// <summary>
			/// Builds the corners. A positive segment count overrides whatever the
			/// fixture asked for, which is how the dense sampling checks work.
			/// </summary>
			public Func<int, List<string>, InsertCorner[]> Corners;

			public override string ToString() => Name;
		}

		private static OutlineCase Triangle(float width, float height, float baseRadius, int baseSegments,
			float apexRadius, int apexSegments) => new() {
			Name = $"triangle {width:0.##}x{height:0.##} base={baseRadius}/{baseSegments} apex={apexRadius}/{apexSegments}",
			Width = width,
			Height = height,
			Corners = (segments, warnings) => InsertShapes.TriangleCorners(width, height,
				new InsertCornerSettings(baseRadius, segments > 0 ? segments : baseSegments),
				new InsertCornerSettings(apexRadius, segments > 0 ? segments : apexSegments), warnings),
		};

		private static OutlineCase Rectangle(float width, float height, float radius, int cornerSegments) => new() {
			Name = $"rectangle {width:0.##}x{height:0.##} corners={radius}/{cornerSegments}",
			Width = width,
			Height = height,
			Corners = (segments, _) => InsertShapes.RectangleCorners(width, height,
				new InsertCornerSettings(radius, segments > 0 ? segments : cornerSegments)),
		};

		private static readonly OutlineCase[] TriangleCases = {
			Triangle(Inch, 2f * Inch, 4f, 8, 4f, 8), // the component's own defaults
			Triangle(25.4f, 25.4f, 2f, 8, 2f, 8),
			Triangle(25.4f, 25.4f, 0f, 1, 0f, 1),
			Triangle(25.4f, 25.4f, 4f, 12, 0.5f, 3),
			Triangle(25.4f, 50.8f, 3f, 8, 3f, 8),
			Triangle(50.8f, 25.4f, 3f, 8, 3f, 8),
			Triangle(38.1f, 12.7f, 1.5f, 6, 5f, 10),
			Triangle(12.7f, 38.1f, 1.5f, 6, 1.5f, 16),
			Triangle(100f, 100f, 25f, 24, 25f, 24),
			Triangle(200f, 50f, 5f, 4, 20f, 4),
		};

		private static readonly OutlineCase[] RectangleCases = {
			Rectangle(Inch, 2f * Inch, 4f, 8),
			Rectangle(25.4f, 25.4f, 0f, 1),
			Rectangle(25.4f, 25.4f, 6f, 12),
			Rectangle(50.8f, 12.7f, 6.35f, 8), // radius is exactly half the height: rounded ends
			Rectangle(100f, 100f, 50f, 32),    // radius is half of both: a circle
			Rectangle(200f, 50f, 2f, 3),
		};

		private static readonly OutlineCase[] Cases = TriangleCases.Concat(RectangleCases).ToArray();

		private static InsertOutline Build(OutlineCase test, int segments = 0, int sideSegments = 1)
		{
			var warnings = new List<string>();
			var outline = InsertOutlineBuilder.Build(test.Corners(segments, warnings), sideSegments);

			Assert.That(warnings, Is.Empty, "the fixture should not need clamping");
			Assert.That(outline.Warnings, Is.Empty, "the fixture should not need clamping");
			return outline;
		}

		#region Invariants, for every shape

		[TestCaseSource(nameof(Cases))]
		public void ShouldFillTheRequestedBoundingBox(OutlineCase test)
		{
			var outline = Build(test);
			Assert.That(outline.Width, Is.EqualTo(test.Width).Within(Tolerance));
			Assert.That(outline.Height, Is.EqualTo(test.Height).Within(Tolerance));
		}

		[TestCaseSource(nameof(Cases))]
		public void ShouldFillTheRequestedBoundingBoxWhenDenselySampled(OutlineCase test)
		{
			// Independent of the analytic bounds maths, so this catches a wrong
			// fillet centre or sweep that the closed-form solve would agree with.
			var outline = Build(test, segments: 2000);
			var xs = outline.Points.Select(p => p.Position.x).ToArray();
			var ys = outline.Points.Select(p => p.Position.y).ToArray();

			Assert.That(xs.Max() - xs.Min(), Is.EqualTo(test.Width).Within(Tolerance));
			Assert.That(ys.Max() - ys.Min(), Is.EqualTo(test.Height).Within(Tolerance));
			Assert.That(xs.Min() + xs.Max(), Is.EqualTo(0f).Within(Tolerance), "not centred on X");
			Assert.That(ys.Min() + ys.Max(), Is.EqualTo(0f).Within(Tolerance), "not centred on Y");
		}

		[TestCaseSource(nameof(Cases))]
		public void ShouldKeepCoarseSamplesInsideTheIdealSilhouette(OutlineCase test)
		{
			var outline = Build(test);
			foreach (var point in outline.Points) {
				Assert.That(math.abs(point.Position.x), Is.LessThanOrEqualTo(test.Width * 0.5f + Tolerance));
				Assert.That(math.abs(point.Position.y), Is.LessThanOrEqualTo(test.Height * 0.5f + Tolerance));
			}
		}

		[TestCaseSource(nameof(Cases))]
		public void ShouldWindCounterClockwise(OutlineCase test)
		{
			var outline = Build(test);
			var area = 0f;
			for (var i = 0; i < outline.Points.Count; i++) {
				var current = outline.Points[i].Position;
				var next = outline.Points[(i + 1) % outline.Points.Count].Position;
				area += current.x * next.y - next.x * current.y;
			}
			Assert.That(area, Is.GreaterThan(0f), "a clockwise outline would flip the generated face");
		}

		[TestCaseSource(nameof(Cases))]
		public void ShouldPointEveryNormalOutwards(OutlineCase test)
		{
			// Every shape here is convex, so no point may sit in front of another
			// point's outward normal.
			var outline = Build(test);
			foreach (var point in outline.Points) {
				var worst = outline.Points.Max(other => math.dot(other.Position - point.Position, point.Normal));
				Assert.That(worst, Is.LessThan(Tolerance), $"normal at {point.Kind} points inwards");
			}
		}

		[TestCaseSource(nameof(Cases))]
		public void ShouldTrackPerimeterDistanceMonotonically(OutlineCase test)
		{
			var outline = Build(test);
			Assert.That(outline.Points[0].Distance, Is.EqualTo(0f));
			for (var i = 1; i < outline.Points.Count; i++) {
				Assert.That(outline.Points[i].Distance, Is.GreaterThan(outline.Points[i - 1].Distance));
			}
			Assert.That(outline.Perimeter, Is.GreaterThan(outline.Points[^1].Distance));
		}

		[TestCaseSource(nameof(Cases))]
		public void ShouldNotRepeatAPoint(OutlineCase test)
		{
			var outline = Build(test);
			for (var i = 0; i < outline.Points.Count; i++) {
				var next = outline.Points[(i + 1) % outline.Points.Count];
				Assert.That(math.distance(outline.Points[i].Position, next.Position), Is.GreaterThan(Tolerance),
					$"point {i} is a duplicate, which would degenerate a face");
			}
		}

		[TestCaseSource(nameof(Cases))]
		public void ShouldOffsetAlongTheParallelCurve(OutlineCase test)
		{
			// This is what the border channel in step 2 relies on: arcs keep their
			// centre, sides keep their direction, and a mitred corner lands the
			// requested distance inside both of the edges that meet there.
			var outline = Build(test);
			var distance = math.min(test.Width, test.Height) * 0.05f;

			foreach (var point in outline.Points) {
				var moved = point.Offset(distance);
				switch (point.Kind) {
					case InsertOutlinePointKind.Arc:
						// A convex fillet tighter than the offset turns itself inside
						// out, which OffsetRadius reports as a negative radius.
						Assert.That(math.distance(moved, point.Center),
							Is.EqualTo(math.abs(point.OffsetRadius(distance))).Within(Tolerance));
						break;

					case InsertOutlinePointKind.Side:
						Assert.That(math.dot(moved - point.Position, point.Normal),
							Is.EqualTo(-distance).Within(Tolerance));
						break;

					default:
						var corner = outline.Corners[point.Corner];
						Assert.That(math.dot(moved - point.Position, corner.NormalIn),
							Is.EqualTo(-distance).Within(Tolerance), "mitre missed the incoming edge");
						Assert.That(math.dot(moved - point.Position, corner.NormalOut),
							Is.EqualTo(-distance).Within(Tolerance), "mitre missed the outgoing edge");
						break;
				}
			}
		}

		[TestCaseSource(nameof(Cases))]
		public void ShouldMeetEachSideTangentially(OutlineCase test)
		{
			var outline = Build(test);
			foreach (var corner in outline.Corners.Where(c => c.Radius > InsertOutlineBuilder.Epsilon)) {
				// A fully rounded shape has no straight run to be tangent to: the
				// side is just the seam where two fillets meet.
				if (!outline.HasStraightRun(corner.Index)) {
					continue;
				}

				var span = outline.SideSpan(corner.Index);
				var start = outline.Points[span[0]];
				var direction = outline.Points[span[^1]].Position - start.Position;
				var length = math.length(direction);

				Assert.That(length, Is.GreaterThan(0f), $"side {corner.Index} collapsed");
				Assert.That(math.dot(direction, start.Normal) / length, Is.EqualTo(0f).Within(1e-4f),
					$"fillet {corner.Index} is not tangent to its side");
			}
		}

		#endregion

		#region Triangle

		[TestCaseSource(nameof(TriangleCases))]
		public void ShouldGiveTheTwoBaseCornersTheSameAngleOnAnXAlignedBase(OutlineCase test)
		{
			var outline = Build(test);
			var left = outline.Corners[0];
			var right = outline.Corners[1];

			Assert.That(left.Interior, Is.EqualTo(right.Interior).Within(1e-6f));
			Assert.That(left.Position.y, Is.EqualTo(right.Position.y).Within(Tolerance));
			Assert.That(outline.Corners[2].Position.x, Is.EqualTo(0f).Within(Tolerance), "apex should be centred");
		}

		[Test]
		public void ShouldPointTheApexUpTheTable()
		{
			// The shape itself is built apex-up in its own 2D space; the mesh
			// mapping turns it to face up the table rather than at the flippers.
			var outline = Build(TriangleCases[0]);
			Assert.That(outline.Corners[2].Tag, Is.EqualTo("Apex"));

			float Depth(int index) => InsertMeshGenerator.ToLocal(outline.Points[index].Position).z;
			var apex = outline.CornerSpan(2).Max(Depth);
			var rest = Enumerable.Range(0, outline.Points.Count)
				.Where(i => outline.Points[i].Corner != 2)
				.Max(Depth);

			Assert.That(apex, Is.GreaterThan(rest), "the apex should be the furthest point up the table");
		}

		[Test]
		public void ShouldMatchTheTextbookAngleForASharpTriangle()
		{
			var outline = Build(Triangle(20f, 30f, 0f, 1, 0f, 1));

			Assert.That(outline.Points.Count, Is.EqualTo(3));
			Assert.That(outline.Corners[0].Interior, Is.EqualTo(math.atan2(30f, 10f)).Within(1e-5f));
		}

		[TestCase(25.4f, 25.4f, 20f)]
		[TestCase(25.4f, 12.7f, 50f)]
		public void ShouldClampOversizedTriangleRadiiAndKeepTheBoundingBox(float width, float height, float radius)
		{
			var warnings = new List<string>();
			var corners = InsertShapes.TriangleCorners(width, height,
				new InsertCornerSettings(radius, 8), new InsertCornerSettings(radius, 8), warnings);
			var outline = InsertOutlineBuilder.Build(corners);

			Assert.That(warnings, Is.Not.Empty, "an impossible radius should say so");
			Assert.That(outline.Width, Is.EqualTo(width).Within(Tolerance));
			Assert.That(outline.Height, Is.EqualTo(height).Within(Tolerance));
		}

		// Scaled up far enough, each of these fits again, but only as a vast flat
		// sliver whose fillets round it back down into a lozenge of the right size:
		// there the sharp triangle is tens of times wider than the box. The second
		// asks for radii that fit as they are, straight in that region; the third
		// has a gap in the family narrower than a thousandth of the scale, which
		// a walk in steps went straight over.
		[TestCase(10f, 25f)]
		[TestCase(9.9f, 24.75f)]
		[TestCase(25.4033f, 36.8618f)]
		public void ShouldClampRadiiToTheFamilyOfTheSharpTriangle(float baseRequest, float apexRequest)
		{
			const float width = 25f;
			const float height = 20f;
			var warnings = new List<string>();
			var corners = InsertShapes.TriangleCorners(width, height,
				new InsertCornerSettings(baseRequest, 8), new InsertCornerSettings(apexRequest, 8), warnings);

			Assert.That(warnings, Is.Not.Empty, "these radii do not fit as asked");
			Assert.That(corners[1].Position.x, Is.LessThan(width), "the clamp landed on a lozenge");

			var baseRadius = corners[0].Radius;
			var apexRadius = corners[2].Radius;
			var smaller = new List<string>();
			InsertShapes.TriangleCorners(width, height,
				new InsertCornerSettings(baseRadius * 0.99f, 8), new InsertCornerSettings(apexRadius * 0.99f, 8), smaller);
			Assert.That(smaller, Is.Empty, "the clamp stopped short of the edge");

			var larger = new List<string>();
			InsertShapes.TriangleCorners(width, height,
				new InsertCornerSettings(baseRadius * 1.01f, 8), new InsertCornerSettings(apexRadius * 1.01f, 8), larger);
			Assert.That(larger, Is.Not.Empty, "the clamp stepped past the edge");
		}

		[Test]
		public void ShouldNotEndTheFamilyWhereTheSolveOnlyTouchesItsLimit()
		{
			// Here the solve's own limit is reached at a double root: it touches
			// zero at 5/6 of the requested radii and rises again, so the family
			// carries on, flattening steadily until the base angle reaches its
			// floor at about 0.99568. Treating the touch as an end clamped to 5/6.
			var warnings = new List<string>();
			var corners = InsertShapes.TriangleCorners(3f, 2f,
				new InsertCornerSettings(1f, 8), new InsertCornerSettings(2f, 8), warnings);

			Assert.That(warnings, Is.Not.Empty);
			Assert.That(corners[0].Radius, Is.EqualTo(0.995675f).Within(1e-4f));
		}

		#endregion

		#region Rectangle

		[TestCaseSource(nameof(RectangleCases))]
		public void ShouldGiveARectangleFourRightAngles(OutlineCase test)
		{
			var outline = Build(test);
			Assert.That(outline.Corners.Count, Is.EqualTo(4));
			foreach (var corner in outline.Corners) {
				Assert.That(corner.Interior, Is.EqualTo(math.PI * 0.5f).Within(1e-5f));
			}
		}

		[Test]
		public void ShouldTurnASquareIntoACircleWhenTheRadiusReachesHalfItsWidth()
		{
			// The most common insert of all, and the case where every side is eaten
			// whole by the fillets either end of it.
			const float size = 100f;
			const int segments = 32;
			var outline = Build(Rectangle(size, size, size * 0.5f, segments));

			foreach (var point in outline.Points) {
				Assert.That(math.length(point.Position), Is.EqualTo(size * 0.5f).Within(Tolerance),
					"every point of a circle is the same distance from its centre");
			}

			for (var side = 0; side < outline.Corners.Count; side++) {
				Assert.That(outline.HasStraightRun(side), Is.False, $"side {side} should have been eaten");
			}

			Assert.That(outline.Points.Count, Is.EqualTo(4 * segments),
				"the coincident tangent points where the fillets meet should have merged");
		}

		[Test]
		public void ShouldRoundOnlyTheShortEndsOfAStadium()
		{
			// Radius is exactly half the height, so the short sides vanish while the
			// long ones survive. Both kinds of side in one outline.
			var outline = Build(Rectangle(50.8f, 12.7f, 6.35f, 8));

			Assert.That(outline.HasStraightRun(0), Is.True, "the long bottom side should survive");
			Assert.That(outline.HasStraightRun(1), Is.False, "the short right side should be eaten");
			Assert.That(outline.HasStraightRun(2), Is.True, "the long top side should survive");
			Assert.That(outline.HasStraightRun(3), Is.False, "the short left side should be eaten");
		}

		[Test]
		public void ShouldClampAnOversizedRectangleRadiusAndKeepTheBoundingBox()
		{
			var corners = InsertShapes.RectangleCorners(25.4f, 12.7f, new InsertCornerSettings(40f, 8));
			var outline = InsertOutlineBuilder.Build(corners);

			Assert.That(outline.Warnings, Is.Not.Empty, "an impossible radius should say so");
			Assert.That(outline.Width, Is.EqualTo(25.4f).Within(Tolerance));
			Assert.That(outline.Height, Is.EqualTo(12.7f).Within(Tolerance));
		}

		#endregion

		#region Sampling and mesh

		[TestCase(3, 9)]
		[TestCase(4, 12)]
		public void ShouldSubdivideOnlyStraightRuns(int sideCount, int expected)
		{
			var test = sideCount == 3 ? TriangleCases[0] : RectangleCases[0];
			var outline = Build(test, sideSegments: 4);
			var sides = outline.Points.Where(p => p.Kind == InsertOutlinePointKind.Side).ToArray();

			Assert.That(sides.Length, Is.EqualTo(expected), "each side should gain three subdivisions");
			Assert.That(sides.All(p => p.Corner == -1 && p.Side >= 0), "a side point is tagged to a corner");
		}

		#endregion

		#region Bucket

		private const float TestDepth = 20f;
		private const float TestChannelWidth = 3f;
		private const float TestChannelDepth = 4f;

		private static readonly float Scale = VisualPinball.Unity.Physics.ScaleInv;

		/// <summary>
		/// A bucket with the channel turned off: a plain flat floor.
		/// </summary>
		private static Mesh BuildMesh(OutlineCase test, out InsertOutline outline)
		{
			outline = Build(test);
			return InsertMeshGenerator.Generate(outline, new InsertBody { Depth = TestDepth }, new Mesh());
		}

		private const int TestBursts = 9;
		private const float TestStarburstOffset = 1.5f;

		/// <summary>
		/// Off to one side and outside the shape, which is where an insert's
		/// starburst centre usually sits and the harder case to get right.
		/// </summary>
		private static readonly float2 TestStarburstCenter = new(0f, 60f);

		private static InsertBody TestBody(InsertOutline outline, bool channel, bool starburst) => new() {
			Depth = TestDepth,
			ChannelWidth = channel ? outline.ClampInset(TestChannelWidth) : 0f,
			ChannelDepth = channel ? TestChannelDepth : 0f,
			StarburstBursts = starburst ? TestBursts : 0,
			StarburstCenter = TestStarburstCenter,
			StarburstOffset = TestStarburstOffset,
		};

		private static Mesh BuildChannelMesh(OutlineCase test, out InsertOutline outline)
		{
			outline = Build(test);
			return InsertMeshGenerator.Generate(outline, TestBody(outline, true, false), new Mesh());
		}

		private static Mesh BuildStarburstMesh(OutlineCase test, out InsertOutline outline)
		{
			outline = Build(test);
			return InsertMeshGenerator.Generate(outline, TestBody(outline, true, true), new Mesh());
		}

		/// <summary>
		/// A shape built three ways. Cutting a channel, and then carving a starburst
		/// into what it raised, must not cost the body anything it had before.
		/// </summary>
		public class BodyCase
		{
			public OutlineCase Shape;
			public bool Channel;
			public bool Starburst;

			public override string ToString() =>
				Shape.Name + (Starburst ? " + starburst" : Channel ? " + channel" : "");
		}

		private static readonly BodyCase[] BodyCases = Cases.SelectMany(shape => new[] {
			new BodyCase { Shape = shape, Channel = false },
			new BodyCase { Shape = shape, Channel = true },
			new BodyCase { Shape = shape, Channel = true, Starburst = true },
		}).ToArray();

		private static Mesh BuildMesh(BodyCase test, out InsertOutline outline)
		{
			if (test.Starburst) {
				return BuildStarburstMesh(test.Shape, out outline);
			}
			return test.Channel ? BuildChannelMesh(test.Shape, out outline) : BuildMesh(test.Shape, out outline);
		}

		[Test]
		public void ShouldEmitTheMeshInWorldUnitsOnThePlayfieldPlane()
		{
			// Parameters are VPX units but a Unity mesh is read as metres, so the
			// generator has to convert. It also has to land the profile in the world
			// XZ plane, because VPX Y is the playfield axis that world Y is not.
			// A sharp triangle is used because its bounding box is exact at any
			// sampling density.
			var mesh = BuildMesh(Triangle(Inch, 2f * Inch, 0f, 1, 0f, 1), out _);
			try {
				Assert.That(mesh.bounds.size.x, Is.EqualTo(InsertShapes.MetresPerInch).Within(1e-6f), "width should run along X");
				Assert.That(mesh.bounds.size.z, Is.EqualTo(2f * InsertShapes.MetresPerInch).Within(1e-6f), "height should run along Z");

			} finally {
				UnityEngine.Object.DestroyImmediate(mesh);
			}
		}

		[TestCaseSource(nameof(BodyCases))]
		public void ShouldSinkTheBucketToTheRequestedDepth(BodyCase test)
		{
			// Unlike the width and height, the depth is exact at any sampling
			// density, because nothing is rounded along it.
			var mesh = BuildMesh(test, out _);
			try {
				Assert.That(mesh.bounds.max.y, Is.EqualTo(0f).Within(1e-6f), "the rim should sit at the origin");
				Assert.That(mesh.bounds.min.y, Is.EqualTo(-TestDepth * Scale).Within(1e-6f), "the floor should sit one depth below");

			} finally {
				UnityEngine.Object.DestroyImmediate(mesh);
			}
		}

		[TestCaseSource(nameof(BodyCases))]
		public void ShouldLeaveTheTopOpen(BodyCase test)
		{
			// A wall triangle may touch the rim with two of its corners; only a lid
			// would touch with all three.
			var mesh = BuildMesh(test, out _);
			try {
				var vertices = mesh.vertices;
				var triangles = mesh.triangles;
				for (var i = 0; i < triangles.Length; i += 3) {
					var atRim = 0;
					for (var corner = 0; corner < 3; corner++) {
						if (math.abs(vertices[triangles[i + corner]].y) < 1e-7f) {
							atRim++;
						}
					}
					Assert.That(atRim, Is.LessThan(3), $"triangle {i / 3} caps the bucket");
				}

			} finally {
				UnityEngine.Object.DestroyImmediate(mesh);
			}
		}

		[TestCaseSource(nameof(BodyCases))]
		public void ShouldTurnEveryFaceIntoTheHollow(BodyCase test)
		{
			var mesh = BuildMesh(test, out var outline);
			try {
				AssertFacesIntoHollow(mesh, outline);

			} finally {
				UnityEngine.Object.DestroyImmediate(mesh);
			}
		}

		/// <summary>
		/// Raising the floor makes the hollow non-convex, so "everything looks at
		/// the middle" no longer states it. What holds is that every face turns
		/// away from the material behind it: the outer wall looks in across the
		/// hollow, the raised middle looks back out at it, and every surface looks
		/// up. The outer wall is the only wall reaching the rim, which is what tells
		/// the two apart.
		/// </summary>
		private static void AssertFacesIntoHollow(Mesh mesh, InsertOutline outline)
		{
			var axis = (float3)InsertMeshGenerator.ToLocal(outline.Centroid);
			var vertices = mesh.vertices;
			var normals = mesh.normals;
			var triangles = mesh.triangles;

			for (var i = 0; i < triangles.Length; i += 3) {
				float3 a = vertices[triangles[i]];
				float3 b = vertices[triangles[i + 1]];
				float3 c = vertices[triangles[i + 2]];

				// math.normalize, not Vector3.normalized: the latter returns zero
				// below a magnitude of 1e-5, and a wall triangle on a millimetre
				// scale part measured in metres is well under that. Every face
				// normal would come back zero and every dot product would pass or
				// fail on noise.
				var wound = math.normalize(math.cross(b - a, c - a));
				var middle = (a + b + c) / 3f;

				// An arrangement moves points by up to half a weld's diagonal onto a
				// lattice, so a wall standing on a step between two neighbouring
				// lattice points can face any way at all. From two welds across, a
				// step turns at most 35 degrees off the line it follows, which every
				// check below allows for. A flat triangle is held to its height
				// instead: one no taller than a snap, lifted onto a steep starburst,
				// is tipped by whichever side of a ridge its points were snapped to.
				var wall = math.abs(wound.y) <= 1e-3f;
				var pixels = (wall ? PlanExtent(a, b, c) : PlanAltitude(a, b, c)) / outline.WeldTolerance;
				if (pixels < 2f) {
					continue;
				}

				// Walls are extruded straight down, so they are exactly vertical.
				// Anything with any lean at all is a surface, which lets a
				// starburst facet tilt as far as it likes without being mistaken
				// for a wall.
				if (math.abs(wound.y) > 1e-3f) {
					Assert.That(wound.y, Is.GreaterThan(0f), $"triangle {i / 3} is a floor facing down ({pixels} px)");

				} else {
					var inwards = new float3(axis.x - middle.x, 0f, axis.z - middle.z);
					var atRim = math.abs(a.y) < 1e-7f || math.abs(b.y) < 1e-7f || math.abs(c.y) < 1e-7f;
					var facing = math.dot(wound, inwards);

					if (atRim) {
						Assert.That(facing, Is.GreaterThan(0f), $"triangle {i / 3} is an outer wall facing out ({pixels} px)");
					} else {
						Assert.That(facing, Is.LessThan(0f), $"triangle {i / 3} is a raised middle facing in ({pixels} px)");
					}
				}

				// Shipping normals that disagree with the winding gives a mesh
				// that looks right until something culls or shadows it.
				var shaded = math.normalize((float3)normals[triangles[i]]
					+ (float3)normals[triangles[i + 1]] + (float3)normals[triangles[i + 2]]);
				Assert.That(math.dot(wound, shaded), Is.GreaterThan(0.5f),
					$"triangle {i / 3} is wound against its own normals ({pixels} px)");
			}
		}

		[TestCaseSource(nameof(BodyCases))]
		public void ShouldPutEveryUpwardFacingVertexOnAFloor(BodyCase test)
		{
			// A lidless bucket may only face up where it has a floor to do it on:
			// the bottom, and the raised middle when a channel has been cut.
			var mesh = BuildMesh(test, out _);
			try {
				var bottom = -TestDepth * Scale;
				var island = (-TestDepth + TestChannelDepth) * Scale;
				var vertices = mesh.vertices;
				var normals = mesh.normals;

				// A starburst leaves the raised middle anything but level, so there
				// its floor is a band the rim can reach either side of.
				var lean = test.Starburst ? TestStarburstOffset * Scale + 1e-6f : 1e-6f;
				for (var i = 0; i < vertices.Length; i++) {
					if (normals[i].y <= 0.5f) {
						continue;
					}
					var onBottom = math.abs(vertices[i].y - bottom) < 1e-6f;
					var onIsland = test.Channel && math.abs(vertices[i].y - island) < lean;
					Assert.That(onBottom || onIsland, Is.True, $"vertex {i} faces up off any floor");
				}

			} finally {
				UnityEngine.Object.DestroyImmediate(mesh);
			}
		}

		[TestCaseSource(nameof(BodyCases))]
		public void ShouldNotEmitADegenerateTriangle(BodyCase test)
		{
			var mesh = BuildMesh(test, out var outline);
			try {
				AssertNoDegenerateTriangles(mesh, outline);

			} finally {
				UnityEngine.Object.DestroyImmediate(mesh);
			}
		}

		/// <summary>
		/// Slivers with no area shade unpredictably and break a bevel later.
		/// </summary>
		/// <remarks>
		/// Held to the lattice, not to an absolute area. An arrangement puts every
		/// point it makes on a lattice a weld apart, and three lattice points not in
		/// line enclose at least half a weld squared, however small or thin that
		/// makes the triangle; where a fixture sets features at that scale, the
		/// triangles there are that small. So a flat triangle must keep a quarter
		/// of a weld squared in plan, which leaves room for the mesh's rounding,
		/// and a wall, which has no area in plan, must stand a tenth of a weld clear
		/// of its points lying in line.
		/// </remarks>
		private static void AssertNoDegenerateTriangles(Mesh mesh, InsertOutline outline)
		{
			var weld = outline.WeldTolerance;
			var vertices = mesh.vertices;
			var triangles = mesh.triangles;
			for (var i = 0; i < triangles.Length; i += 3) {
				float3 a = vertices[triangles[i]];
				float3 b = vertices[triangles[i + 1]];
				float3 c = vertices[triangles[i + 2]];
				var plan = math.abs(Cross(Plan(b) - Plan(a), Plan(c) - Plan(a))) * 0.5f;
				if (plan > 0.25f * weld * weld) {
					continue;
				}
				var longest = math.max(math.distance(a, b), math.max(math.distance(b, c), math.distance(c, a)));
				var height = math.length(math.cross(b - a, c - a)) / longest;
				Assert.That(height, Is.GreaterThan(0.1f * weld * Scale), $"triangle {i / 3} has no area");
			}
		}

		/// <summary>The longest side of a triangle in plan, in VPX units.</summary>
		private static float PlanExtent(float3 a, float3 b, float3 c)
			=> math.max(math.distance(Plan(a), Plan(b)), math.max(math.distance(Plan(b), Plan(c)), math.distance(Plan(c), Plan(a))));

		/// <summary>The smallest height of a triangle in plan, in VPX units.</summary>
		private static float PlanAltitude(float3 a, float3 b, float3 c)
		{
			var extent = PlanExtent(a, b, c);
			return extent > 0f ? math.abs(Cross(Plan(b) - Plan(a), Plan(c) - Plan(a))) / extent : 0f;
		}

		[Test]
		public void ShouldRaiseTheChannelIslandWithoutLiftingTheChannelFloor()
		{
			var mesh = BuildChannelMesh(RectangleCases[0], out _);
			try {
				Assert.That(mesh.bounds.min.y, Is.EqualTo(-TestDepth * Scale).Within(1e-6f),
					"the channel floor should stay at the bottom of the bucket");
				Assert.That(mesh.vertices.Any(v => math.abs(v.y - (-TestDepth + TestChannelDepth) * Scale) < 1e-6f),
					"nothing was raised to the island height");

			} finally {
				UnityEngine.Object.DestroyImmediate(mesh);
			}
		}

		[Test]
		public void ShouldNarrowTheIslandByTwiceTheChannelWidth()
		{
			// Eroding a convex shape pulls every supporting line in by the inset, so
			// the bounding box loses exactly twice that on each axis. A sharp
			// rectangle makes it exact at any sampling density.
			const float width = 25.4f;
			const float height = 12.7f;
			const float inset = 3f;
			var mesh = BuildChannelMesh(Rectangle(width, height, 0f, 1), out _);
			try {
				var island = (-TestDepth + TestChannelDepth) * Scale;
				var top = mesh.vertices.Where(v => math.abs(v.y - island) < 1e-6f).ToArray();

				Assert.That(top.Max(v => v.x) - top.Min(v => v.x),
					Is.EqualTo((width - 2f * inset) * Scale).Within(1e-6f));
				Assert.That(top.Max(v => v.z) - top.Min(v => v.z),
					Is.EqualTo((height - 2f * inset) * Scale).Within(1e-6f));

			} finally {
				UnityEngine.Object.DestroyImmediate(mesh);
			}
		}

		[Test]
		public void ShouldSquareOffAFilletTheChannelErodesAway()
		{
			// The inset is wider than the fillet, so the fillet cannot shrink. It
			// has to collapse onto the mitre where its two sides now meet.
			var outline = Build(Rectangle(50.8f, 50.8f, 2f, 8));
			const float inset = 5f;

			for (var corner = 0; corner < outline.Corners.Count; corner++) {
				var span = outline.CornerSpan(corner);
				var mitre = outline.OffsetPoint(span[0], inset);
				foreach (var index in span) {
					Assert.That(math.distance(outline.OffsetPoint(index, inset), mitre), Is.LessThan(Tolerance),
						$"corner {corner} did not collapse to a single point");
				}
			}
		}

		[Test]
		public void ShouldNotJumpWhereErosionTakesOverFromShrinking()
		{
			// Either side of the changeover the offset comes from a different
			// formula, so this is where a sign error would show up as a jump.
			const float radius = 4f;
			var outline = Build(Rectangle(50.8f, 50.8f, radius, 8));
			var index = outline.CornerSpan(0)[4];

			var shrunk = outline.OffsetPoint(index, radius - 1e-3f);
			var eroded = outline.OffsetPoint(index, radius + 1e-3f);
			Assert.That(math.distance(shrunk, eroded), Is.LessThan(0.01f));
		}

		[Test]
		public void ShouldClampAChannelWiderThanTheShapeCanTake()
		{
			// The widest inset a 12.7 tall shape can take is half its height.
			var outline = Build(Rectangle(25.4f, 12.7f, 2f, 8));
			var clamped = outline.ClampInset(20f);

			Assert.That(clamped, Is.GreaterThan(0f));
			Assert.That(clamped, Is.LessThanOrEqualTo(12.7f * 0.5f + Tolerance));
			Assert.That(outline.ClampInset(1f), Is.EqualTo(1f), "a channel that fits should pass through untouched");
		}

		[Test]
		public void ShouldStopInsettingWhileThereIsStillSomethingToWeld()
		{
			// Right at the theoretical limit the ring survives the fold test as a
			// sliver thinner than the weld tolerance, which then collapses it into a
			// lopsided scrap. The clamp has to stop short of that.
			var outline = Build(Rectangle(25.4f, 12.7f, 2f, 8));
			var inset = outline.ClampInset(20f);
			var body = new InsertBody { Depth = TestDepth, ChannelWidth = inset, ChannelDepth = TestChannelDepth };

			var mesh = InsertMeshGenerator.Generate(outline, body, new Mesh());
			try {
				var island = (-TestDepth + TestChannelDepth) * Scale;
				var top = mesh.vertices.Where(v => math.abs(v.y - island) < 1e-6f).ToArray();
				Assert.That(top.Length, Is.GreaterThanOrEqualTo(3), "the island collapsed away entirely");

				var triangles = mesh.triangles;
				var vertices = mesh.vertices;
				for (var i = 0; i < triangles.Length; i += 3) {
					float3 a = vertices[triangles[i]];
					float3 b = vertices[triangles[i + 1]];
					float3 c = vertices[triangles[i + 2]];
					Assert.That(math.length(math.cross(b - a, c - a)), Is.GreaterThan(1e-12f),
						$"triangle {i / 3} has no area");
				}

			} finally {
				UnityEngine.Object.DestroyImmediate(mesh);
			}
		}

		[Test]
		public void ShouldStillFindAnInsetForAnAbsurdlyLargeRequest()
		{
			// Bracketing against the request rather than the shape means a big
			// enough number never halves its way down into the feasible range, and
			// the search reports that nothing fits at all.
			var outline = Build(Rectangle(25.4f, 12.7f, 2f, 8));
			Assert.That(outline.ClampInset(1e9f), Is.GreaterThan(1f));
			Assert.That(outline.ClampInset(1e9f), Is.LessThanOrEqualTo(12.7f * 0.5f + Tolerance));
		}

		[Test]
		public void ShouldNotBuildADegenerateWallForAChannelWithNoDepth()
		{
			// Zero depth is a legal setting and means an inset flat floor, not a
			// ring of zero area triangles between two coincident rings.
			var outline = Build(RectangleCases[0]);
			var body = new InsertBody { Depth = TestDepth, ChannelWidth = 3f, ChannelDepth = 0f };

			var mesh = InsertMeshGenerator.Generate(outline, body, new Mesh());
			try {
				var vertices = mesh.vertices;
				var triangles = mesh.triangles;
				for (var i = 0; i < triangles.Length; i += 3) {
					float3 a = vertices[triangles[i]];
					float3 b = vertices[triangles[i + 1]];
					float3 c = vertices[triangles[i + 2]];
					Assert.That(math.length(math.cross(b - a, c - a)), Is.GreaterThan(1e-12f),
						$"triangle {i / 3} has no area");
				}

			} finally {
				UnityEngine.Object.DestroyImmediate(mesh);
			}
		}

		[Test]
		public void ShouldApplyASmallRadiusCorrectionRatherThanRoundItAway()
		{
			// A correction of well under a percent still answers a real overlap.
			// Discarding it leaves the two tangent points crossed over by far more
			// than the weld can absorb, and the side runs backwards.
			var corners = InsertShapes.RectangleCorners(100f, 100f, new InsertCornerSettings(50.002f, 8));
			var outline = InsertOutlineBuilder.Build(corners);

			Assert.That(outline.Corners.Max(c => c.Radius), Is.LessThanOrEqualTo(50f + Tolerance),
				"the correction was rounded away");

			// With the radius honoured, no side may run against its own direction.
			foreach (var corner in outline.Corners) {
				var span = outline.SideSpan(corner.Index);
				if (span.Count < 2) {
					continue;
				}
				var step = outline.Points[span[^1]].Position - outline.Points[span[0]].Position;
				Assert.That(math.dot(step, corner.Outgoing), Is.GreaterThanOrEqualTo(-Tolerance),
					$"side {corner.Index} runs backwards");
			}
		}

		[Test]
		public void ShouldNotCompoundTheRadiusClampAcrossSharedCorners()
		{
			// Every side of a square overruns by the same ratio. Taking both in turn
			// would reduce each corner twice and land far tighter than either side
			// needed: 60 * (5/6)^2 = 41.7 where 50 fits.
			var corners = InsertShapes.RectangleCorners(100f, 100f, new InsertCornerSettings(60f, 8));
			var outline = InsertOutlineBuilder.Build(corners);

			Assert.That(outline.Corners[0].Radius, Is.EqualTo(50f).Within(0.01f));
		}

		[Test]
		public void ShouldNotOrphanACornerWhenFilletsWeldTogether()
		{
			// A square whose radius reaches half its width, cut at one segment a
			// corner, is a diamond of four points shared between four corners.
			// Asking about the sides must report that, not throw.
			var outline = Build(Rectangle(100f, 100f, 50f, 1));

			Assert.That(outline.Points.Count, Is.EqualTo(4));
			for (var side = 0; side < outline.Corners.Count; side++) {
				Assert.That(() => outline.SideSpan(side), Throws.Nothing);
				Assert.That(() => outline.HasStraightRun(side), Throws.Nothing);
			}

			// The last corner's only surviving sample is welded onto the first
			// corner's, which leaves it nothing of its own, and both of its sides
			// with nothing to run between.
			Assert.That(outline.CornerSpan(3), Is.Empty);
			Assert.That(outline.SideSpan(2), Is.Empty);
			Assert.That(outline.SideSpan(3), Is.Empty);
		}

		[Test]
		public void ShouldMitreAReflexCornerOutOfTheNotch()
		{
			// Both edge lines move in by the offset and meet behind the corner along
			// its bisector. At a reflex corner the bisector points out of the
			// material, into the notch, so the mitre steps back along it just as a
			// convex one does. Getting that sign wrong lands inside the notch, on
			// the wrong side of both shifted edges.
			var corners = new[] {
				new InsertCorner(new float2(0f, 0f), 0f, 1), new InsertCorner(new float2(3f, 0f), 0f, 1),
				new InsertCorner(new float2(3f, 3f), 0f, 1), new InsertCorner(new float2(2f, 3f), 0f, 1),
				new InsertCorner(new float2(2f, 1f), 0f, 1), new InsertCorner(new float2(1f, 1f), 0f, 1),
				new InsertCorner(new float2(1f, 3f), 0f, 1), new InsertCorner(new float2(0f, 3f), 0f, 1),
			};
			var outline = InsertOutlineBuilder.Build(corners, center: false);
			var notch = outline.Points[4];
			const float inset = 0.25f;

			Assert.That(outline.Corners[4].Concave, Is.True, "(2,1) should be the reflex corner");
			Assert.That(notch.Offset(inset).x, Is.EqualTo(2f + inset).Within(1e-4f));
			Assert.That(notch.Offset(inset).y, Is.EqualTo(1f - inset).Within(1e-4f));
		}

		#endregion

		#region Starburst

		private static InsertStarburst TestStarburst(InsertOutline outline, out List<float2> footprint,
			float2? center = null, int bursts = TestBursts, float offset = TestStarburstOffset,
			float channelWidth = TestChannelWidth)
		{
			var inset = outline.ClampInset(channelWidth);
			footprint = new List<float2>();
			for (var i = 0; i < outline.Points.Count; i++) {
				footprint.Add(outline.OffsetPoint(i, inset));
			}

			// Held to the bucket the same way the generator holds it, or the two
			// would be describing different surfaces.
			var limit = InsertStarburst.OffsetLimit(TestDepth, TestChannelDepth);
			offset = math.clamp(offset, -limit, limit);
			return InsertStarburst.Cover(footprint, center ?? TestStarburstCenter, bursts, offset, 0f);
		}

		[TestCaseSource(nameof(Cases))]
		public void ShouldGrowTheWheelUntilItsTrianglesCoverTheSurface(OutlineCase test)
		{
			// It is the inscribed polygon that has to reach, not the circle: a point
			// beyond a triangle's closing chord but inside the circle would fall in
			// no triangle at all, and its height would be extrapolated.
			var starburst = TestStarburst(Build(test), out var footprint);

			foreach (var point in footprint) {
				var sector = starburst.SectorOf(point);
				var a = starburst.Center;
				var b = starburst.Rim(sector);
				var c = starburst.Rim(sector + 1);

				var area = Cross(b - a, c - a);
				var along = Cross(point - a, c - a) / area;
				var across = Cross(b - a, point - a) / area;
				Assert.That(along, Is.GreaterThanOrEqualTo(-1e-4f));
				Assert.That(across, Is.GreaterThanOrEqualTo(-1e-4f));
				Assert.That(along + across, Is.LessThanOrEqualTo(1f + 1e-4f),
					"the point sits beyond the chord closing its triangle");
			}
		}

		private static float Cross(float2 a, float2 b) => a.x * b.y - b.x * a.y;

		[Test]
		public void ShouldZigzagTheRimAboutItsCentre()
		{
			var starburst = TestStarburst(Build(TriangleCases[0]), out _);

			Assert.That(starburst.Count, Is.EqualTo(TestBursts * 2), "two rim vertices a burst");
			for (var i = 0; i < starburst.Count; i++) {
				var expected = i % 2 == 0 ? TestStarburstOffset : -TestStarburstOffset;
				Assert.That(starburst.RimDepth(i), Is.EqualTo(expected).Within(Tolerance));

				// The count is even, so going round the loop the sign keeps changing
				// rather than repeating once at the seam.
				Assert.That(starburst.RimDepth(i) * starburst.RimDepth(i + 1), Is.LessThan(0f));
			}
		}

		[Test]
		public void ShouldAgreeOnTheHeightOfASharedSpoke()
		{
			// Every crease runs along a spoke. If the two triangles meeting there
			// disagreed by even a little, the surface would split open along it.
			var starburst = TestStarburst(Build(TriangleCases[0]), out _);

			for (var spoke = 0; spoke < starburst.Count; spoke++) {
				var direction = starburst.Direction(spoke);
				for (var step = 1; step <= 8; step++) {
					var point = starburst.Center + direction * (starburst.Radius * step / 8f);
					Assert.That(starburst.DepthInSector(spoke, point),
						Is.EqualTo(starburst.DepthInSector(spoke - 1, point)).Within(Tolerance),
						$"the triangles either side of spoke {spoke} disagree");
				}
			}
		}

		[Test]
		public void ShouldKeepTheSlopesOfATinyStarburst()
		{
			// Twice a triangle's area was once tested against a length tolerance,
			// which called every triangle of a small enough wheel degenerate and
			// flattened it while its facet normals still described the slopes.
			var footprint = new List<float2> {
				new(-0.004f, -0.004f), new(0.004f, -0.004f), new(0.004f, 0.004f), new(-0.004f, 0.004f),
			};
			var starburst = InsertStarburst.Cover(footprint, float2.zero, 12, 1.5f, 0f);
			var halfway = starburst.Direction(0) * (starburst.Radius * 0.5f);

			Assert.That(starburst.DepthAt(halfway), Is.EqualTo(0.75f).Within(1e-3f));
		}

		[TestCaseSource(nameof(StarburstCases))]
		public void ShouldTileTheIslandWithTheStarburstWhereverTheCentreSits(StarburstCase test)
		{
			var mesh = BuildStarburstMesh(test, out var outline, out _);
			try {
				AssertTilesTheIsland(mesh, outline, test.SideSegments, test.ChannelWidth);

			} finally {
				UnityEngine.Object.DestroyImmediate(mesh);
			}
		}

		[TestCaseSource(nameof(Cases))]
		public void ShouldTileTheIslandWithTheStarburst(OutlineCase test)
		{
			var mesh = BuildStarburstMesh(test, out var outline);
			try {
				AssertTilesTheIsland(mesh, outline, 1);

			} finally {
				UnityEngine.Object.DestroyImmediate(mesh);
			}
		}

		/// <summary>
		/// Checks the surface the mesh actually carries over the raised floor, not
		/// an intermediate step on the way to it: seen from above, it has to cover
		/// the raised floor's whole area once, with no hole and no overlap.
		/// </summary>
		/// <remarks>
		/// The area alone would let a hole be paid for by an overlap elsewhere, so a
		/// grid of sample points is checked too, each of which has to lie in exactly
		/// one triangle. A sample within a hair of any triangle's edge is on a seam,
		/// where one or two would both be right, and is skipped.
		/// </remarks>
		private static void AssertTilesTheIsland(Mesh mesh, InsertOutline outline, int sideSegments,
			float channelWidth = TestChannelWidth)
		{
			var inset = outline.ClampInset(channelWidth);
			var island = new List<float2>();
			for (var i = 0; i < outline.Points.Count; i++) {
				island.Add(outline.OffsetPoint(i, inset));
			}

			// The surface is everything above the channel floor that faces up: the
			// walls are vertical, and the channel floor sits at the bottom.
			var floor = -TestDepth * Scale;
			var vertices = mesh.vertices;
			var triangles = mesh.triangles;
			var surface = new List<(float2, float2, float2)>();
			for (var i = 0; i < triangles.Length; i += 3) {
				float3 a = vertices[triangles[i]];
				float3 b = vertices[triangles[i + 1]];
				float3 c = vertices[triangles[i + 2]];
				if (math.abs(a.y - floor) < 1e-7f && math.abs(b.y - floor) < 1e-7f && math.abs(c.y - floor) < 1e-7f) {
					continue;
				}
				if (math.normalize(math.cross(b - a, c - a)).y <= 1e-3f) {
					continue;
				}
				surface.Add((Plan(a), Plan(b), Plan(c)));
			}

			var whole = math.abs(Area(island)) * 0.5f;
			var covered = surface.Sum(t => math.abs(Cross(t.Item2 - t.Item1, t.Item3 - t.Item1)) * 0.5f);
			Assert.That(covered, Is.EqualTo(whole).Within(whole * 1e-4f), "the surface does not cover the raised floor once");

			var min = new float2(float.MaxValue);
			var max = new float2(float.MinValue);
			foreach (var point in island) {
				min = math.min(min, point);
				max = math.max(max, point);
			}

			// Off the grid lines, so no sample is placed on a spoke by construction.
			const int samples = 40;
			var margin = math.max(outline.Width, outline.Height) * 1e-5f;
			for (var i = 0; i < samples; i++) {
				for (var j = 0; j < samples; j++) {
					var point = min + (max - min) * new float2((i + 0.37f) / samples, (j + 0.61f) / samples);
					if (!Inside(island, point, margin)) {
						continue;
					}

					var holders = 0;
					var onSeam = false;
					foreach (var (a, b, c) in surface) {
						var clearance = Clearance(a, b, c, point);
						if (math.abs(clearance) <= margin) {
							onSeam = true;
							break;
						}
						if (clearance > 0f) {
							holders++;
						}
					}
					if (!onSeam) {
						Assert.That(holders, Is.EqualTo(1), $"{point} lies in {holders} pieces of the surface");
					}
				}
			}
		}

		/// <summary>A local space point seen from above, back in VPX units.</summary>
		private static float2 Plan(float3 local) => new float2(-local.x, local.z) / Scale;

		/// <summary>
		/// How far inside a triangle a point is, by its nearest edge: negative
		/// outside, whichever way the triangle winds.
		/// </summary>
		private static float Clearance(float2 a, float2 b, float2 c, float2 point)
		{
			var sign = Cross(b - a, c - a) >= 0f ? 1f : -1f;
			var nearest = float.MaxValue;
			foreach (var (from, to) in new[] { (a, b), (b, c), (c, a) }) {
				var edge = to - from;
				var length = math.length(edge);
				if (length > 0f) {
					nearest = math.min(nearest, sign * Cross(edge, point - from) / length);
				}
			}
			return nearest;
		}

		private static bool Inside(IReadOnlyList<float2> polygon, float2 point, float margin)
		{
			for (var i = 0; i < polygon.Count; i++) {
				var from = polygon[i];
				var edge = polygon[(i + 1) % polygon.Count] - from;
				var length = math.length(edge);
				if (length > 0f && Cross(edge, point - from) / length <= margin) {
					return false;
				}
			}
			return true;
		}

		private static float Area(IReadOnlyList<float2> polygon)
		{
			var area = 0f;
			for (var i = 0; i < polygon.Count; i++) {
				var current = polygon[i];
				var next = polygon[(i + 1) % polygon.Count];
				area += current.x * next.y - next.x * current.y;
			}
			return area;
		}

		[TestCaseSource(nameof(BodyCases))]
		public void ShouldCloseEverySeamBelowTheRim(BodyCase test)
		{
			var mesh = BuildMesh(test, out _);
			try {
				AssertSeamsClosed(mesh);

			} finally {
				UnityEngine.Object.DestroyImmediate(mesh);
			}
		}

		/// <summary>
		/// The surest sign that the separately built parts of the body meet. Edges
		/// are matched by position, since walls and surfaces carry their own
		/// vertices for shading, and every edge below the rim has to be shared by
		/// exactly two triangles. Once means the surface is torn there, which is
		/// how a T-junction shows up; more than twice means it folds back on
		/// itself. Only the rim is left open.
		/// </summary>
		/// <remarks>
		/// Positions are matched exactly. Every point two parts share is taken from
		/// the same place by the same arithmetic, so they agree to the bit, and a
		/// tolerance would only let two distinct points a hair apart pass for one.
		/// </remarks>
		private static void AssertSeamsClosed(Mesh mesh)
		{
			var vertices = mesh.vertices;
			var triangles = mesh.triangles;
			var edges = new Dictionary<((float, float, float), (float, float, float)), int>();

			// Negative zero equals zero but need not hash like it.
			float Clean(float value) => value == 0f ? 0f : value;
			(float, float, float) Key(Vector3 v) => (Clean(v.x), Clean(v.y), Clean(v.z));

			for (var i = 0; i < triangles.Length; i += 3) {
				for (var edge = 0; edge < 3; edge++) {
					var from = Key(vertices[triangles[i + edge]]);
					var to = Key(vertices[triangles[i + (edge + 1) % 3]]);
					var key = from.CompareTo(to) <= 0 ? (from, to) : (to, from);
					edges.TryGetValue(key, out var seen);
					edges[key] = seen + 1;
				}
			}

			var wrong = new List<string>();
			foreach (var ((from, to), seen) in edges) {
				if (seen != 2 && (from.Item2 != 0f || to.Item2 != 0f)) {
					wrong.Add($"({from.Item1:G9}, {from.Item2:G9}, {from.Item3:G9})"
						+ $" -> ({to.Item1:G9}, {to.Item2:G9}, {to.Item3:G9}) x{seen}");
				}
			}
			Assert.That(wrong, Is.Empty,
				$"{wrong.Count} edges below the rim are not shared by exactly two triangles");
		}

		[TestCaseSource(nameof(Cases))]
		public void ShouldStandTheWallUpToMeetTheStarburst(OutlineCase test)
		{
			var mesh = BuildStarburstMesh(test, out var outline);
			try {
				AssertWallMeetsSurface(mesh, TestStarburst(outline, out _));

			} finally {
				UnityEngine.Object.DestroyImmediate(mesh);
			}
		}

		/// <summary>
		/// Every vertex where the raised middle's wall stops has to be on the
		/// surface above it, not at the level the floor would have had without one.
		/// </summary>
		private static void AssertWallMeetsSurface(Mesh mesh, InsertStarburst starburst)
		{
			var island = -TestDepth + TestChannelDepth;
			var vertices = mesh.vertices;
			var normals = mesh.normals;
			var raised = 0;

			for (var i = 0; i < vertices.Length; i++) {
				// Wall vertices are the vertical ones. Of those, the rim belongs to
				// the outer wall and the channel floor is where both walls stand;
				// whatever is left is the top of the raised middle's wall.
				if (math.abs(normals[i].y) > 1e-3f
					|| math.abs(vertices[i].y) < 1e-6f
					|| math.abs(vertices[i].y + TestDepth * Scale) < 1e-6f) {
					continue;
				}

				var plan = new float2(-vertices[i].x, vertices[i].z) / Scale;
				var expected = (island + starburst.DepthAt(plan)) * Scale;
				Assert.That(vertices[i].y, Is.EqualTo(expected).Within(1e-6f),
					$"wall vertex {i} does not reach the surface above it");
				raised++;
			}

			Assert.That(raised, Is.GreaterThan(0), "no raised wall was found to check");
		}

		private static void AssertWithinBucket(Mesh mesh)
		{
			Assert.That(mesh.bounds.max.y, Is.LessThanOrEqualTo(1e-7f), "something stands proud of the rim");
			Assert.That(mesh.bounds.min.y, Is.GreaterThanOrEqualTo(-TestDepth * Scale - 1e-7f),
				"something digs below the floor");
		}

		/// <summary>
		/// A starburst placed wherever it is hardest to get right.
		/// </summary>
		public class StarburstCase
		{
			public string Name;
			public OutlineCase Shape;
			public int SideSegments = 1;
			public float2 Center;
			public int Bursts;
			public float Offset = TestStarburstOffset;
			public float ChannelWidth = TestChannelWidth;

			public override string ToString() => Name;
		}

		/// <summary>
		/// A 20 unit sharp square, whose raised middle after a 3 unit channel is
		/// exactly the square from -7 to 7, so a centre can be put precisely on a
		/// corner or a side of it.
		/// </summary>
		private static readonly OutlineCase SharpSquare = Rectangle(20f, 20f, 0f, 1);

		/// <summary>
		/// A sharp triangle with a thirty degree apex.
		/// </summary>
		private static readonly OutlineCase AcuteTriangle = Triangle(10f, 18.660254f, 0f, 1, 0f, 1);

		/// <summary>
		/// A centre whose ninth-burst spoke at 80 degrees ends on the raised floor's
		/// left side a weld and a half below its apex, where the right side is only
		/// three quarters of a weld away.
		/// </summary>
		private static float2 AcuteTipCenter()
		{
			var tip = AcuteTip(TestChannelWidth, out var left, out var weld);
			var ending = tip + 1.5f * weld * math.normalize(left - tip);
			var angle = math.radians(80f);
			return ending - new float2(math.cos(angle), math.sin(angle));
		}

		/// <summary>
		/// The raised floor's apex of <see cref="AcuteTriangle"/> after an inset,
		/// with its left base corner and the weld.
		/// </summary>
		private static float2 AcuteTip(float channelWidth, out float2 left, out float weld)
		{
			var outline = Build(AcuteTriangle);
			var inset = outline.ClampInset(channelWidth);
			int apex = 0, leftmost = 0;
			for (var i = 1; i < outline.Points.Count; i++) {
				if (outline.Points[i].Position.y > outline.Points[apex].Position.y) {
					apex = i;
				}
				if (outline.Points[i].Position.x < outline.Points[leftmost].Position.x) {
					leftmost = i;
				}
			}
			left = outline.OffsetPoint(leftmost, inset);
			weld = outline.WeldTolerance;
			return outline.OffsetPoint(apex, inset);
		}

		/// <summary>
		/// A centre two dozen welds below the tip, with 128 bursts: one spoke
		/// ends at the apex and the next on the left side within a weld of the
		/// right, and routing the right side through that end carried it across
		/// the first spoke.
		/// </summary>
		private static float2 AcuteTipSpokeCenter()
		{
			var tip = AcuteTip(1f, out _, out var weld);
			return tip + new float2(1.18468f, -24.11462f) * weld;
		}

		private static readonly StarburstCase[] StarburstCases = {
			new() { Name = "centre inside", Shape = SharpSquare, Center = new float2(0f, 0f), Bursts = 9 },
			new() { Name = "centre on a corner", Shape = SharpSquare, Center = new float2(-7f, -7f), Bursts = 9 },
			new() { Name = "centre on a side", Shape = SharpSquare, Center = new float2(0f, -7f), Bursts = 9 },
			new() { Name = "spoke along a side", Shape = SharpSquare, Center = new float2(-20f, -7f), Bursts = 9 },
			new() { Name = "two bursts", Shape = SharpSquare, Center = new float2(0f, 0f), Bursts = 2 },
			new() {
				Name = "offset past the rim and the floor", Shape = SharpSquare,
				Center = new float2(0f, 0f), Bursts = 2, Offset = 100f,
			},
			new() {
				Name = "subdivided sides, flat", Shape = Rectangle(25.4f, 25.4f, 0f, 1), SideSegments = 2,
				Center = TestStarburstCenter, Bursts = 9, Offset = 0f,
			},
			new() { Name = "128 bursts", Shape = TriangleCases[0], Center = TestStarburstCenter, Bursts = 128 },
			new() { Name = "128 bursts, centre inside", Shape = RectangleCases[0], Center = float2.zero, Bursts = 128 },
			new() { Name = "rounded, centre inside", Shape = RectangleCases[0], Center = new float2(5f, -10f), Bursts = 12 },
			new() { Name = "centre a hair outside a side", Shape = SharpSquare, Center = new float2(0f, -7.00002f), Bursts = 9 },
			new() { Name = "centre a hair inside a side", Shape = SharpSquare, Center = new float2(0f, -6.99998f), Bursts = 9 },
			new() {
				// A spoke that only just clips a corner, 5e-5 beside it, leaving a
				// sliver of the island in the sector before it.
				Name = "spoke grazing a corner", Shape = Rectangle(106f, 106f, 0f, 1),
				Center = new float2(-50f, -52.45481221f), Bursts = 128,
			},
			new() { Name = "negative offset", Shape = SharpSquare, Center = new float2(0f, 0f), Bursts = 2, Offset = -100f },
			new() {
				// A real corner face a tenth of a unit wide, fifty units from the
				// origin, which a float area about the origin reports as nothing.
				Name = "small face far out", Shape = Rectangle(106f, 106f, 0f, 1),
				Center = new float2(-50f, -52.45240784f), Bursts = 128,
			},
			new() {
				// The same, where the face survives but its centroid lands far
				// outside it and picks the wrong sector.
				Name = "small face far out, surviving", Shape = Rectangle(106f, 106f, 0f, 1),
				Center = new float2(-50f, -52.45363617f), Bursts = 128,
			},
			new() {
				// Close enough to both sides of the tip to be taken onto both,
				// which folded the ring back on itself there.
				Name = "spoke ending in an acute tip", Shape = AcuteTriangle, Center = AcuteTipCenter(), Bursts = 9,
			},
			new() {
				Name = "spokes fanning into an acute tip", Shape = AcuteTriangle, Center = AcuteTipSpokeCenter(),
				Bursts = 128, ChannelWidth = 1f, Offset = 1f,
			},
		};

		private static Mesh BuildStarburstMesh(StarburstCase test, out InsertOutline outline, out InsertStarburst starburst)
		{
			outline = Build(test.Shape, sideSegments: test.SideSegments);
			var body = new InsertBody {
				Depth = TestDepth,
				ChannelWidth = outline.ClampInset(test.ChannelWidth),
				ChannelDepth = TestChannelDepth,
				StarburstBursts = test.Bursts,
				StarburstCenter = test.Center,
				StarburstOffset = test.Offset,
			};
			starburst = TestStarburst(outline, out _, test.Center, test.Bursts, test.Offset, test.ChannelWidth);
			return InsertMeshGenerator.Generate(outline, body, new Mesh());
		}

		[Test]
		public void ShouldKeepTwoRingPointsApartWhenACentreFallsBetweenThem()
		{
			// Two samples of the raised floor's outline just over the weld apart,
			// and a starburst centre within the weld of both. Welding in a chain
			// merged all three into one point, while the wall and the channel
			// floor went on using both samples, and the surface came adrift of
			// them there.
			var outline = Build(Rectangle(20f, 20f, 0.001f, 64));
			const float inset = 0.0001f;
			var center = (outline.OffsetPoint(0, inset) + outline.OffsetPoint(1, inset)) * 0.5f;
			var body = new InsertBody {
				Depth = TestDepth, ChannelWidth = inset, ChannelDepth = TestChannelDepth,
				StarburstBursts = TestBursts, StarburstCenter = center, StarburstOffset = TestStarburstOffset,
			};

			var mesh = InsertMeshGenerator.Generate(outline, body, new Mesh());
			try {
				AssertSeamsClosed(mesh);

			} finally {
				UnityEngine.Object.DestroyImmediate(mesh);
			}
		}

		[TestCaseSource(nameof(StarburstCases))]
		public void ShouldCloseEverySeamWhereverTheCentreSits(StarburstCase test)
		{
			var mesh = BuildStarburstMesh(test, out _, out _);
			try {
				AssertSeamsClosed(mesh);

			} finally {
				UnityEngine.Object.DestroyImmediate(mesh);
			}
		}

		[TestCaseSource(nameof(StarburstCases))]
		public void ShouldNotEmitADegenerateTriangleWhereverTheCentreSits(StarburstCase test)
		{
			var mesh = BuildStarburstMesh(test, out var outline, out _);
			try {
				AssertNoDegenerateTriangles(mesh, outline);

			} finally {
				UnityEngine.Object.DestroyImmediate(mesh);
			}
		}

		[TestCaseSource(nameof(StarburstCases))]
		public void ShouldTurnEveryFaceIntoTheHollowWhereverTheCentreSits(StarburstCase test)
		{
			var mesh = BuildStarburstMesh(test, out var outline, out _);
			try {
				AssertFacesIntoHollow(mesh, outline);

			} finally {
				UnityEngine.Object.DestroyImmediate(mesh);
			}
		}

		[TestCaseSource(nameof(StarburstCases))]
		public void ShouldKeepTheStarburstInsideTheBucket(StarburstCase test)
		{
			var mesh = BuildStarburstMesh(test, out _, out _);
			try {
				AssertWithinBucket(mesh);

			} finally {
				UnityEngine.Object.DestroyImmediate(mesh);
			}
		}

		[TestCaseSource(nameof(StarburstCases))]
		public void ShouldStandTheWallUpToMeetTheStarburstWhereverTheCentreSits(StarburstCase test)
		{
			var mesh = BuildStarburstMesh(test, out _, out var starburst);
			try {
				AssertWallMeetsSurface(mesh, starburst);

			} finally {
				UnityEngine.Object.DestroyImmediate(mesh);
			}
		}

		[Test]
		public void ShouldLeaveTheFloorFlatWithoutEnoughBursts()
		{
			var outline = Build(TriangleCases[0]);
			var body = TestBody(outline, true, true);
			body.StarburstBursts = InsertStarburst.MinBursts - 1;

			var mesh = InsertMeshGenerator.Generate(outline, body, new Mesh());
			try {
				var island = (-TestDepth + TestChannelDepth) * Scale;
				var vertices = mesh.vertices;
				var normals = mesh.normals;
				for (var i = 0; i < vertices.Length; i++) {
					if (normals[i].y > 0.5f && vertices[i].y > (-TestDepth + 1e-6f) * Scale) {
						Assert.That(vertices[i].y, Is.EqualTo(island).Within(1e-6f));
					}
				}

			} finally {
				UnityEngine.Object.DestroyImmediate(mesh);
			}
		}

		#endregion

		#region Wall topology

		[Test]
		public void ShouldSplitWallVerticesAtASharpCorner()
		{
			// A sharp corner's two sides carry genuinely different normals, so the
			// wall must not share a vertex across it or the crease shades round.
			// Four corners, each needing a vertex per side at the rim and at the
			// floor, plus the floor's own ring and its fan centre.
			var mesh = BuildMesh(Rectangle(25.4f, 25.4f, 0f, 1), out _);
			try {
				Assert.That(mesh.vertexCount, Is.EqualTo(4 * 2 * 2 + 4 + 1));

			} finally {
				UnityEngine.Object.DestroyImmediate(mesh);
			}
		}

		[Test]
		public void ShouldShareWallVerticesWhereTheOutlineIsSmooth()
		{
			// A circle has no corner to split, so the ring is shared all the way
			// round save for the seam, where U has to restart rather than wrap.
			var mesh = BuildMesh(Rectangle(100f, 100f, 50f, 32), out var outline);
			try {
				var ring = outline.Points.Count + 1;
				Assert.That(mesh.vertexCount, Is.EqualTo(ring * 2 + outline.Points.Count + 1));

			} finally {
				UnityEngine.Object.DestroyImmediate(mesh);
			}
		}

		#endregion

		#region Holes

		public class HoleCase
		{
			public string Name;
			public OutlineCase Shape;
			public float2 Center = TestStarburstCenter;
			public int Bursts = TestBursts;
			public int Segments = 16;

			/// <summary>The holes, given the outline and the channel's inset.</summary>
			public Func<InsertOutline, float, IEnumerable<InsertHole>> Holes;

			public override string ToString() => Name;
		}

		private static IEnumerable<InsertHole> Radius(IEnumerable<Vector2> centers, float radius)
			=> centers.Select(center => new InsertHole(center, radius));

		private static readonly HoleCase[] HoleCases = {
			new() {
				Name = "corners, starburst", Shape = TriangleCases[0],
				Holes = (outline, inset) => Radius(InsertHoles.AtCorners(outline, inset), 3f),
			},
			new() {
				Name = "corners and sides, 128 bursts", Shape = TriangleCases[0], Bursts = 128,
				Holes = (outline, inset) => Radius(InsertHoles.AtCornersAndSides(outline, inset), 2f),
			},
			new() {
				Name = "seven around, flat", Shape = RectangleCases[0], Bursts = 0,
				Holes = (outline, inset) => Radius(InsertHoles.Around(outline, inset, 7), 2.5f),
			},
			new() {
				// Crosses nothing, so only its bridge ties it to the rest.
				Name = "floating in a face", Shape = SharpSquare, Center = new float2(0f, 20f), Bursts = 4,
				Holes = (_, _) => new[] { new InsertHole(new Vector2(2f, 1f), 1f) },
			},
			new() {
				Name = "floating, flat", Shape = SharpSquare, Bursts = 0,
				Holes = (_, _) => new[] { new InsertHole(new Vector2(1f, 1f), 2f) },
			},
			new() {
				Name = "across a spoke", Shape = SharpSquare, Center = new float2(0f, 20f),
				Holes = (_, _) => new[] { new InsertHole(Vector2.zero, 2f) },
			},
			new() {
				Name = "around the starburst's centre", Shape = SharpSquare, Center = float2.zero,
				Holes = (_, _) => new[] { new InsertHole(Vector2.zero, 2f) },
			},
			new() {
				Name = "overlapping", Shape = SharpSquare,
				Holes = (_, _) => new[] { new InsertHole(new Vector2(2f, 2f), 2f), new InsertHole(new Vector2(3.5f, 2.5f), 2f) },
			},
			new() {
				Name = "over the whole middle", Shape = SharpSquare,
				Holes = (_, _) => new[] { new InsertHole(Vector2.zero, 20f) },
			},
			new() {
				Name = "off the middle, in the channel", Shape = SharpSquare,
				Holes = (_, _) => new[] { new InsertHole(new Vector2(8.5f, 8.5f), 0.5f) },
			},
			new() {
				Name = "coarse circles", Shape = RectangleCases[0], Segments = 3,
				Holes = (outline, inset) => Radius(InsertHoles.AtCorners(outline, inset), 3f),
			},
		};

		private static Mesh BuildHoleMesh(HoleCase test, out InsertOutline outline, out InsertHole[] holes)
		{
			outline = Build(test.Shape);
			var inset = outline.ClampInset(TestChannelWidth);
			holes = test.Holes(outline, inset).ToArray();
			var body = new InsertBody {
				Depth = TestDepth,
				ChannelWidth = inset,
				ChannelDepth = TestChannelDepth,
				StarburstBursts = test.Bursts,
				StarburstCenter = test.Center,
				StarburstOffset = TestStarburstOffset,
				Holes = holes,
				HoleSegments = test.Segments,
			};
			return InsertMeshGenerator.Generate(outline, body, new Mesh());
		}

		[TestCaseSource(nameof(HoleCases))]
		public void ShouldCloseEverySeamRoundTheHoles(HoleCase test)
		{
			var mesh = BuildHoleMesh(test, out _, out _);
			try {
				AssertSeamsClosed(mesh);

			} finally {
				UnityEngine.Object.DestroyImmediate(mesh);
			}
		}

		[TestCaseSource(nameof(HoleCases))]
		public void ShouldNotEmitADegenerateTriangleRoundTheHoles(HoleCase test)
		{
			var mesh = BuildHoleMesh(test, out var outline, out _);
			try {
				AssertNoDegenerateTriangles(mesh, outline);

			} finally {
				UnityEngine.Object.DestroyImmediate(mesh);
			}
		}

		[TestCaseSource(nameof(HoleCases))]
		public void ShouldKeepTheHolesInsideTheBucket(HoleCase test)
		{
			var mesh = BuildHoleMesh(test, out _, out _);
			try {
				AssertWithinBucket(mesh);

			} finally {
				UnityEngine.Object.DestroyImmediate(mesh);
			}
		}

		[TestCaseSource(nameof(HoleCases))]
		public void ShouldFaceEveryHoleWallIntoTheHole(HoleCase test)
		{
			// A hole's wall faces into the hole, away from the raised floor it is
			// cut from: from each wall triangle, a step along its normal lands in a
			// hole or off the raised floor, and a step against it lands on it.
			var mesh = BuildHoleMesh(test, out var outline, out var holes);
			try {
				var inset = outline.ClampInset(TestChannelWidth);
				var island = Enumerable.Range(0, outline.Points.Count).Select(i => outline.OffsetPoint(i, inset)).ToList();
				var polygons = HolePolygons(holes, test.Segments);
				var step = 0.05f;
				var vertices = mesh.vertices;
				var triangles = mesh.triangles;

				for (var i = 0; i < triangles.Length; i += 3) {
					float3 a = vertices[triangles[i]];
					float3 b = vertices[triangles[i + 1]];
					float3 c = vertices[triangles[i + 2]];
					var wound = math.normalize(math.cross(b - a, c - a));
					var atRim = math.abs(a.y) < 1e-7f || math.abs(b.y) < 1e-7f || math.abs(c.y) < 1e-7f;
					if (math.abs(wound.y) > 1e-3f || atRim || PlanExtent(a, b, c) < 20f * outline.WeldTolerance) {
						continue;
					}

					var middle = Plan((a + b + c) / 3f);
					var direction = math.normalize(new float2(-wound.x, wound.z));
					var ahead = middle + direction * step;
					var behind = middle - direction * step;
					if (IsNear(island, polygons, ahead, step * 0.5f) || IsNear(island, polygons, behind, step * 0.5f)) {
						continue;
					}
					Assert.That(OnRaised(island, polygons, behind) && !OnRaised(island, polygons, ahead), Is.True,
						$"wall triangle {i / 3} at {middle} does not face off the raised floor");
				}

			} finally {
				UnityEngine.Object.DestroyImmediate(mesh);
			}
		}

		[TestCaseSource(nameof(HoleCases))]
		public void ShouldCoverTheRaisedFloorAroundTheHolesOnce(HoleCase test)
		{
			// Sampled: every point of the raised floor outside the holes is under
			// exactly one piece of its surface, and every point inside a hole is
			// under none, with the channel's floor below it instead.
			var mesh = BuildHoleMesh(test, out var outline, out var holes);
			try {
				var inset = outline.ClampInset(TestChannelWidth);
				var island = Enumerable.Range(0, outline.Points.Count).Select(i => outline.OffsetPoint(i, inset)).ToList();
				var polygons = HolePolygons(holes, test.Segments);

				var floor = -TestDepth * Scale;
				var vertices = mesh.vertices;
				var triangles = mesh.triangles;
				var surface = new List<(float2, float2, float2)>();
				var bottom = new List<(float2, float2, float2)>();
				for (var i = 0; i < triangles.Length; i += 3) {
					float3 a = vertices[triangles[i]];
					float3 b = vertices[triangles[i + 1]];
					float3 c = vertices[triangles[i + 2]];
					if (math.normalize(math.cross(b - a, c - a)).y <= 1e-3f) {
						continue;
					}
					var low = math.abs(a.y - floor) < 1e-7f && math.abs(b.y - floor) < 1e-7f && math.abs(c.y - floor) < 1e-7f;
					(low ? bottom : surface).Add((Plan(a), Plan(b), Plan(c)));
				}

				var min = new float2(float.MaxValue);
				var max = new float2(float.MinValue);
				foreach (var point in island) {
					min = math.min(min, point);
					max = math.max(max, point);
				}

				const int samples = 40;
				var margin = math.max(outline.Width, outline.Height) * 1e-4f;
				for (var i = 0; i < samples; i++) {
					for (var j = 0; j < samples; j++) {
						var point = min + (max - min) * new float2((i + 0.37f) / samples, (j + 0.61f) / samples);
						if (!Inside(island, point, margin) || IsNear(island, polygons, point, margin)) {
							continue;
						}
						var inHole = polygons.Any(polygon => Inside(polygon, point, 0f));
						var over = surface.Count(t => Clearance(t.Item1, t.Item2, t.Item3, point) > 0f);
						var under = bottom.Count(t => Clearance(t.Item1, t.Item2, t.Item3, point) > 0f);
						if (inHole) {
							Assert.That(over, Is.EqualTo(0), $"{point} is in a hole but under the raised floor");
							Assert.That(under, Is.EqualTo(1), $"{point} is in a hole with no floor below it");
						} else {
							Assert.That(over, Is.EqualTo(1), $"{point} lies in {over} pieces of the raised floor");
						}
					}
				}

			} finally {
				UnityEngine.Object.DestroyImmediate(mesh);
			}
		}

		[Test]
		public void ShouldPutAHoleAtEveryCorner()
		{
			var outline = Build(TriangleCases[0]);
			var inset = outline.ClampInset(TestChannelWidth);
			var centers = InsertHoles.AtCorners(outline, inset);

			Assert.That(centers, Has.Count.EqualTo(outline.Corners.Count));
			var island = Enumerable.Range(0, outline.Points.Count).Select(i => outline.OffsetPoint(i, inset)).ToList();
			foreach (var center in centers) {
				Assert.That(DistanceToOutline(island, new float2(center.x, center.y)), Is.LessThan(1e-3f),
					$"{center} is not on the raised floor's edge");
			}
		}

		[Test]
		public void ShouldPutACornerHoleOnTheEdgeEvenWhereTheCornerKeptNoPoint()
		{
			// Rounded all the way with a single segment, a corner can leave no
			// point of its own, and its hole went to where the sharp corner was,
			// well outside the shape.
			var warnings = new List<string>();
			var outline = InsertOutlineBuilder.Build(InsertShapes.RectangleCorners(100f, 100f,
				new InsertCornerSettings(50f, 1)), 1);
			var inset = outline.ClampInset(TestChannelWidth);
			var island = Enumerable.Range(0, outline.Points.Count).Select(i => outline.OffsetPoint(i, inset)).ToList();

			foreach (var center in InsertHoles.AtCorners(outline, inset)) {
				Assert.That(DistanceToOutline(island, new float2(center.x, center.y)), Is.LessThan(1e-3f),
					$"{center} is not on the raised floor's edge");
			}
		}

		[Test]
		public void ShouldSymmetrizeHoles()
		{
			var holes = new[] {
				new InsertHole(new Vector2(0.3f, 5f), 1f),      // near the middle
				new InsertHole(new Vector2(10f, -3f), 1f),      // pairs with the next
				new InsertHole(new Vector2(-9.6f, -3.4f), 2f),
				new InsertHole(new Vector2(4f, 20f), 1f),       // no partner
				new InsertHole(new Vector2(-6f, 8f), 1f),       // two candidates, the closer wins
				new InsertHole(new Vector2(6.2f, 8.1f), 1f),
				new InsertHole(new Vector2(6.8f, 8.5f), 1f),
			};

			var symmetric = InsertHoles.Symmetrize(holes, 1f);

			AssertAt(symmetric[0], new Vector2(0f, 5f));
			AssertAt(symmetric[1], new Vector2(9.8f, -3.2f));
			AssertAt(symmetric[2], new Vector2(-9.8f, -3.2f));
			Assert.That(symmetric[2].Radius, Is.EqualTo(2f), "radii stay as they were");
			AssertAt(symmetric[3], new Vector2(4f, 20f));
			AssertAt(symmetric[4], new Vector2(-6.1f, 8.05f));
			AssertAt(symmetric[5], new Vector2(6.1f, 8.05f));
			AssertAt(symmetric[6], new Vector2(6.8f, 8.5f));

			static void AssertAt(InsertHole hole, Vector2 expected)
				=> Assert.That(Vector2.Distance(hole.Center, expected), Is.LessThan(1e-5f), $"{hole.Center} should be {expected}");
		}

		[Test]
		public void ShouldSpreadHolesEvenlyAroundTheEdge()
		{
			var outline = Build(RectangleCases[0]);
			var inset = outline.ClampInset(TestChannelWidth);
			var centers = InsertHoles.Around(outline, inset, 6);
			var island = Enumerable.Range(0, outline.Points.Count).Select(i => outline.OffsetPoint(i, inset)).ToList();

			Assert.That(centers, Has.Count.EqualTo(6));
			foreach (var center in centers) {
				Assert.That(DistanceToOutline(island, new float2(center.x, center.y)), Is.LessThan(1e-3f));
			}
			Assert.That(InsertHoles.AtCornersAndSides(outline, inset), Has.Count.EqualTo(2 * outline.Corners.Count));
		}

		private static List<List<float2>> HolePolygons(IEnumerable<InsertHole> holes, int segments)
		{
			var count = math.max(3, segments);
			return holes.Select(hole => Enumerable.Range(0, count).Select(k => {
				var angle = 2f * math.PI * k / count;
				return new float2(hole.Center.x, hole.Center.y) + hole.Radius * new float2(math.cos(angle), math.sin(angle));
			}).ToList()).ToList();
		}

		private static bool OnRaised(List<float2> island, List<List<float2>> holes, float2 point)
			=> Inside(island, point, 0f) && !holes.Any(hole => Inside(hole, point, 0f));

		/// <summary>Whether a point is within <paramref name="distance"/> of the middle's or a hole's outline.</summary>
		private static bool IsNear(List<float2> island, List<List<float2>> holes, float2 point, float distance)
			=> DistanceToOutline(island, point) < distance || holes.Any(hole => DistanceToOutline(hole, point) < distance);

		private static float DistanceToOutline(List<float2> polygon, float2 point)
		{
			var best = float.MaxValue;
			for (var i = 0; i < polygon.Count; i++) {
				var a = polygon[i];
				var edge = polygon[(i + 1) % polygon.Count] - a;
				var t = math.saturate(math.dot(point - a, edge) / math.max(math.lengthsq(edge), 1e-12f));
				best = math.min(best, math.distance(point, a + edge * t));
			}
			return best;
		}

		#endregion

		#region Bevel

		public class BevelCase
		{
			public string Name;
			public OutlineCase Shape;
			public bool Channel = true;
			public int Bursts;
			public float2 Center = TestStarburstCenter;
			public float Width = 1f;
			public int Segments = 4;
			public bool Holes;

			public override string ToString() => Name;
		}

		private static readonly BevelCase[] BevelCases = {
			new() { Name = "rounded, flat middle", Shape = TriangleCases[0] },
			new() { Name = "chamfer", Shape = TriangleCases[0], Segments = 1 },
			new() { Name = "starburst", Shape = TriangleCases[0], Bursts = 12 },
			new() { Name = "sharp corners mitred", Shape = SharpSquare, Bursts = 9, Center = new float2(0f, 20f), Width = 2f },
			new() { Name = "no channel", Shape = RectangleCases[0], Channel = false, Width = 1.5f, Segments = 3 },
			new() { Name = "sharp, no channel", Shape = SharpSquare, Channel = false, Width = 2f, Segments = 5 },
			new() { Name = "circle, 128 bursts", Shape = Rectangle(100f, 100f, 50f, 32), Bursts = 128, Width = 2f, Segments = 6 },
			new() { Name = "with holes", Shape = TriangleCases[0], Bursts = 12, Holes = true },
			new() { Name = "wider than a corner's fillet", Shape = RectangleCases[0], Width = 2.8f, Segments = 8 },
		};

		private static Mesh BuildBevelMesh(BevelCase test, out InsertOutline outline, out float width)
		{
			outline = Build(test.Shape);
			var inset = test.Channel ? outline.ClampInset(TestChannelWidth) : 0f;
			width = outline.ClampInset(test.Width);
			var body = new InsertBody {
				Depth = TestDepth,
				ChannelWidth = inset,
				ChannelDepth = TestChannelDepth,
				StarburstBursts = test.Bursts,
				StarburstCenter = test.Center,
				StarburstOffset = TestStarburstOffset,
				BottomBevel = width,
				BottomBevelSegments = test.Segments,
				HoleSegments = 16,
				Holes = test.Holes ? InsertHoles.AtCorners(outline, inset).Select(c => new InsertHole(c, 3f)).ToArray() : null,
			};
			return InsertMeshGenerator.Generate(outline, body, new Mesh());
		}

		[TestCaseSource(nameof(BevelCases))]
		public void ShouldCloseEverySeamWithABevel(BevelCase test)
		{
			var mesh = BuildBevelMesh(test, out _, out _);
			try {
				AssertSeamsClosed(mesh);

			} finally {
				UnityEngine.Object.DestroyImmediate(mesh);
			}
		}

		[TestCaseSource(nameof(BevelCases))]
		public void ShouldNotEmitADegenerateTriangleWithABevel(BevelCase test)
		{
			var mesh = BuildBevelMesh(test, out var outline, out _);
			try {
				AssertNoDegenerateTriangles(mesh, outline);

			} finally {
				UnityEngine.Object.DestroyImmediate(mesh);
			}
		}

		[TestCaseSource(nameof(BevelCases))]
		public void ShouldTurnEveryFaceIntoTheHollowWithABevel(BevelCase test)
		{
			if (test.Holes) {
				// Hole walls face into their holes, which the axis rule below does not know.
				Assert.Pass("hole walls are checked by the hole tests");
			}
			var mesh = BuildBevelMesh(test, out var outline, out _);
			try {
				AssertFacesIntoHollow(mesh, outline);

			} finally {
				UnityEngine.Object.DestroyImmediate(mesh);
			}
		}

		[TestCaseSource(nameof(BevelCases))]
		public void ShouldKeepTheBevelInsideTheBucket(BevelCase test)
		{
			var mesh = BuildBevelMesh(test, out _, out _);
			try {
				AssertWithinBucket(mesh);

			} finally {
				UnityEngine.Object.DestroyImmediate(mesh);
			}
		}

		[TestCase(1)]
		[TestCase(4)]
		[TestCase(9)]
		public void ShouldRoundTheBottomEdgeOnAQuarterCircle(int segments)
		{
			// A sharp 20 unit square has its rim at 10 either way, so how far in a
			// point sits is ten less the larger of its coordinates, whichever side
			// or mitre it is on. Every bevel vertex lies on the arc from the wall,
			// the width up, to the floor, the width in.
			const float width = 2f;
			var outline = Build(SharpSquare);
			var body = new InsertBody {
				Depth = TestDepth, BottomBevel = width, BottomBevelSegments = segments,
			};
			var mesh = InsertMeshGenerator.Generate(outline, body, new Mesh());
			try {
				var floor = -TestDepth;
				var found = 0;
				var vertices = mesh.vertices;
				var normals = mesh.normals;
				for (var i = 0; i < vertices.Length; i++) {
					var height = vertices[i].y / Scale;
					var plan = Plan(vertices[i]);
					var inset = 10f - math.cmax(math.abs(plan));
					var tilted = math.abs(normals[i].y) > 1e-3f && math.abs(normals[i].y) < 1f - 1e-3f;
					if (!tilted) {
						continue;
					}
					var distance = math.length(new float2(inset - width, height - (floor + width)));
					Assert.That(distance, Is.EqualTo(width).Within(1e-3f), $"bevel vertex {i} at inset {inset}, height {height} is off the arc");
					found++;
				}
				Assert.That(found, Is.GreaterThan(0), "no bevel vertex found");

				// The wall stops where the bevel starts.
				foreach (var vertex in vertices) {
					if (10f - math.cmax(math.abs(Plan(vertex))) < 1e-4f) {
						Assert.That(vertex.y / Scale, Is.GreaterThanOrEqualTo(floor + width - 1e-3f), "the wall reaches below the bevel");
					}
				}

			} finally {
				UnityEngine.Object.DestroyImmediate(mesh);
			}
		}

		#endregion

		#region Arrangement

		private static readonly TestCaseData[] UnspikeCases = {
			new TestCaseData(new[] { 0, 1, 2, 3 }, new[] { 0, 1, 2, 3 }, new[] { 0, 1, 2, 3 })
				.SetName("{m}(nothing to take out)"),
			new TestCaseData(new[] { 0, 1, 1, 2 }, new[] { 0, 1, 2 }, new[] { 0, 1, 1, 2 })
				.SetName("{m}(a repeat)"),
			new TestCaseData(new[] { 0, 1, 2, 1, 3 }, new[] { 0, 1, 3 }, new[] { 0, 1, 1, 1, 2 })
				.SetName("{m}(a spike)"),
			new TestCaseData(new[] { 0, 1, 2, 3, 2, 1, 4 }, new[] { 0, 1, 4 }, new[] { 0, 1, 1, 1, 1, 1, 2 })
				.SetName("{m}(a spike on a spike)"),
			new TestCaseData(new[] { 0, 1, 2, 0 }, new[] { 0, 1, 2 }, new[] { 0, 1, 2, 0 })
				.SetName("{m}(a repeat across the end)"),
			new TestCaseData(new[] { 0, 1, 2, 3, 0, 9 }, new[] { 0, 1, 2, 3 }, new[] { 0, 1, 2, 3, 0, 0 })
				.SetName("{m}(a spike at the end)"),
			new TestCaseData(new[] { 9, 0, 1, 2, 3, 0 }, new[] { 1, 2, 3, 0 }, new[] { 3, 3, 0, 1, 2, 3 })
				.SetName("{m}(a spike at the start)"),
			new TestCaseData(new[] { 0, 1, 2, 1 }, new[] { 0, 1 }, new[] { 0, 1, 1, 1 })
				.SetName("{m}(nothing but spikes)"),
		};

		[TestCaseSource(nameof(UnspikeCases))]
		public void ShouldTakeEverySpikeOutOfAWalk(int[] walk, int[] expected, int[] survivors)
		{
			var result = InsertArrangement.Unspike(walk, out var survivor);

			Assert.That(result, Is.EqualTo(expected));
			Assert.That(survivor, Is.EqualTo(survivors));
		}

		[Test]
		public void ShouldCollapseASliverNarrowerThanTheWeldOutOfEveryFace()
		{
			// A thirty degree tip, and a segment ending on its left side a weld
			// and a half below it, three quarters of a weld from the right side.
			// Whether the sliver of tip above that end survives depends on how the
			// pixels fall, but either way no face may walk out and back along what
			// is left of it, and no edge may cross another. Edges move by up to a
			// pixel, so the area is only held to a pixel times the perimeter.
			const float weld = 0.01f;
			var height = 5f / math.tan(math.radians(15f));
			var arrangement = new InsertArrangement(weld);
			var left = arrangement.AddPoint(new float2(-5f, 0f));
			var right = arrangement.AddPoint(new float2(5f, 0f));
			var apex = arrangement.AddPoint(new float2(0f, height));
			var segments = new List<int> {
				arrangement.AddSegment(left, right),
				arrangement.AddSegment(right, apex),
				arrangement.AddSegment(apex, left),
			};

			var tip = new float2(0f, height);
			var ending = tip + 1.5f * weld * math.normalize(new float2(-5f, 0f) - tip);
			var direction = new float2(math.cos(math.radians(80f)), math.sin(math.radians(80f)));
			segments.Add(arrangement.AddSegment(arrangement.AddPoint(ending - direction), arrangement.AddPoint(ending)));
			arrangement.Build();

			var faces = arrangement.Faces();
			Assert.That(faces, Has.Count.EqualTo(1));
			Assert.That(faces[0].Distinct().Count(), Is.EqualTo(faces[0].Count), "the face repeats a point");

			var perimeter = 10.0 + 2.0 * math.length(new float2(5f, height));
			Assert.That(arrangement.SignedArea(faces[0]), Is.EqualTo(5.0 * height).Within(perimeter * weld * 1.5));
			AssertNoEdgesCross(arrangement, segments);
		}

		[Test]
		public void ShouldNeverLetTwoEdgesCross()
		{
			// Rings with tips down to a few degrees, and fans of spokes from
			// centres placed a few pixels from one of their corners, inside and
			// out, which is where rerouting one segment through a point could
			// carry it across another. Snap rounding promises that it never does,
			// and that the faces still cover the ring to within a pixel along its
			// length.
			var random = new System.Random(20260922);
			for (var trial = 0; trial < 300; trial++) {
				var weld = (float)(0.001 * (1.0 + random.NextDouble()));
				var arrangement = new InsertArrangement(weld);
				var segments = new List<int>();

				var ring = RandomRing(random);
				var corners = ring.Select(point => arrangement.AddPoint(point)).ToArray();
				for (var i = 0; i < ring.Count; i++) {
					segments.Add(arrangement.AddSegment(corners[i], corners[(i + 1) % ring.Count]));
				}

				var corner = ring[random.Next(ring.Count)];
				var center = corner + new float2((float)(random.NextDouble() - 0.5), (float)(random.NextDouble() - 0.5))
					* weld * random.Next(1, 40);
				var count = 2 * random.Next(2, 129);
				var hub = arrangement.AddPoint(center);
				var phase = random.NextDouble();
				for (var k = 0; k < count; k++) {
					var angle = 2.0 * math.PI_DBL * (k + phase) / count;
					var reach = new float2((float)math.cos(angle), (float)math.sin(angle)) * 400f;
					if (TryClipToConvex(ring, center, center + reach, out var from, out var to)) {
						var start = math.distance(from, center) < 1e-6f ? hub : arrangement.AddPoint(from);
						segments.Add(arrangement.AddSegment(start, arrangement.AddPoint(to)));
					}
				}
				arrangement.Build();

				AssertNoEdgesCross(arrangement, segments, $"trial {trial}");

				var covered = arrangement.Faces().Sum(face => arrangement.SignedArea(face));
				var area = 0.0;
				var perimeter = 0.0;
				for (var i = 0; i < ring.Count; i++) {
					var a = (double2)ring[i];
					var b = (double2)ring[(i + 1) % ring.Count];
					area += (a.x * b.y - b.x * a.y) * 0.5;
					perimeter += math.distance(a, b);
				}
				Assert.That(covered, Is.EqualTo(area).Within(perimeter * weld * 1.5), $"trial {trial}");
			}
		}

		/// <summary>
		/// A convex counter-clockwise ring of three to eight corners, half the time
		/// with one corner drawn far out into an acute tip.
		/// </summary>
		private static List<float2> RandomRing(System.Random random)
		{
			var count = random.Next(3, 9);
			var angles = Enumerable.Range(0, count).Select(_ => random.NextDouble() * 2.0 * math.PI_DBL).OrderBy(a => a).ToList();
			var radius = new double2(5.0 + random.NextDouble() * 20.0, 5.0 + random.NextDouble() * 20.0);
			var ring = angles.Select(a => (float2)(radius * new double2(math.cos(a), math.sin(a)))).ToList();

			if (random.NextDouble() < 0.5) {
				var index = random.Next(count);
				ring[index] *= 2f + (float)random.NextDouble() * 6f;
			}

			// Whatever came out non-convex is replaced by its hull.
			return ConvexHull(ring);
		}

		/// <summary>Andrew's monotone chain, counter-clockwise, without collinear points.</summary>
		private static List<float2> ConvexHull(List<float2> points)
		{
			var sorted = points.Distinct().OrderBy(p => p.x).ThenBy(p => p.y).ToList();
			var hull = new List<float2>();
			foreach (var pass in new[] { sorted, Enumerable.Reverse(sorted).ToList() }) {
				var start = hull.Count;
				foreach (var point in pass) {
					while (hull.Count - start >= 2 && Cross(hull[^2], hull[^1], point) <= 0.0) {
						hull.RemoveAt(hull.Count - 1);
					}
					hull.Add(point);
				}
				hull.RemoveAt(hull.Count - 1);
			}
			return hull;
		}

		/// <summary>
		/// The part of a segment inside a convex counter-clockwise ring.
		/// </summary>
		private static bool TryClipToConvex(List<float2> ring, float2 a, float2 b, out float2 from, out float2 to)
		{
			var enter = 0.0;
			var leave = 1.0;
			for (var i = 0; i < ring.Count; i++) {
				var p = (double2)ring[i];
				var edge = (double2)ring[(i + 1) % ring.Count] - p;
				var inward = new double2(-edge.y, edge.x);
				var start = math.dot((double2)a - p, inward);
				var change = math.dot((double2)b - (double2)a, inward);
				if (change == 0.0) {
					if (start < 0.0) {
						from = to = default;
						return false;
					}
					continue;
				}
				var t = -start / change;
				if (change > 0.0) {
					enter = math.max(enter, t);
				} else {
					leave = math.min(leave, t);
				}
			}
			from = (float2)((double2)a + ((double2)b - (double2)a) * enter);
			to = (float2)((double2)a + ((double2)b - (double2)a) * leave);
			return leave - enter > 1e-6;
		}

		/// <summary>
		/// That no two edges of the result cross, other than at a vertex they
		/// share.
		/// </summary>
		private static void AssertNoEdgesCross(InsertArrangement arrangement, List<int> segments, string context = "")
		{
			var edges = new HashSet<(int, int)>();
			foreach (var segment in segments) {
				var cuts = arrangement.Cuts(segment);
				for (var i = 1; i < cuts.Count; i++) {
					edges.Add((math.min(cuts[i - 1].Point, cuts[i].Point), math.max(cuts[i - 1].Point, cuts[i].Point)));
				}
			}

			var list = edges.ToList();
			for (var i = 0; i < list.Count; i++) {
				for (var j = i + 1; j < list.Count; j++) {
					var (a, b) = list[i];
					var (c, d) = list[j];
					if (a == c || a == d || b == c || b == d) {
						continue;
					}
					var pa = (double2)arrangement.Position(a);
					var pb = (double2)arrangement.Position(b);
					var pc = (double2)arrangement.Position(c);
					var pd = (double2)arrangement.Position(d);
					var crosses = Cross(pa, pb, pc) * Cross(pa, pb, pd) < 0.0 && Cross(pc, pd, pa) * Cross(pc, pd, pb) < 0.0;
					Assert.That(crosses, Is.False, $"{context}: edges {a}-{b} and {c}-{d} cross");
				}
			}
		}

		private static double Cross(double2 o, double2 a, double2 b)
			=> (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

		#endregion
	}
}
