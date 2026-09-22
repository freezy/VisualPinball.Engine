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

		private static Mesh BuildChannelMesh(OutlineCase test, out InsertOutline outline)
		{
			outline = Build(test);
			var body = new InsertBody {
				Depth = TestDepth,
				ChannelWidth = outline.ClampInset(TestChannelWidth),
				ChannelDepth = TestChannelDepth,
			};
			return InsertMeshGenerator.Generate(outline, body, new Mesh());
		}

		/// <summary>
		/// A shape, built with the channel both off and on. Cutting the channel must
		/// not cost the body any of the properties it had without one.
		/// </summary>
		public class BodyCase
		{
			public OutlineCase Shape;
			public bool Channel;
			public override string ToString() => Shape.Name + (Channel ? " + channel" : "");
		}

		private static readonly BodyCase[] BodyCases = Cases.SelectMany(shape => new[] {
			new BodyCase { Shape = shape, Channel = false },
			new BodyCase { Shape = shape, Channel = true },
		}).ToArray();

		private static Mesh BuildMesh(BodyCase test, out InsertOutline outline)
		{
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
			// Raising the floor makes the hollow non-convex, so "everything looks at
			// the middle" no longer states it. What holds is that every face turns
			// away from the material behind it: the outer wall looks in across the
			// hollow, the raised middle looks back out at it, and both horizontal
			// surfaces look up. The outer wall is the only wall reaching the rim,
			// which is what tells the two apart.
			var mesh = BuildMesh(test, out var outline);
			try {
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

					if (math.abs(wound.y) > 0.5f) {
						Assert.That(wound.y, Is.GreaterThan(0f), $"triangle {i / 3} is a floor facing down");

					} else {
						var inwards = new float3(axis.x - middle.x, 0f, axis.z - middle.z);
						var atRim = math.abs(a.y) < 1e-7f || math.abs(b.y) < 1e-7f || math.abs(c.y) < 1e-7f;
						var facing = math.dot(wound, inwards);

						if (atRim) {
							Assert.That(facing, Is.GreaterThan(0f), $"triangle {i / 3} is an outer wall facing out");
						} else {
							Assert.That(facing, Is.LessThan(0f), $"triangle {i / 3} is a raised middle facing in");
						}
					}

					// Shipping normals that disagree with the winding gives a mesh
					// that looks right until something culls or shadows it.
					var shaded = math.normalize((float3)normals[triangles[i]]
						+ (float3)normals[triangles[i + 1]] + (float3)normals[triangles[i + 2]]);
					Assert.That(math.dot(wound, shaded), Is.GreaterThan(0.5f),
						$"triangle {i / 3} is wound against its own normals");
				}

			} finally {
				UnityEngine.Object.DestroyImmediate(mesh);
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

				for (var i = 0; i < vertices.Length; i++) {
					if (normals[i].y <= 0.5f) {
						continue;
					}
					var onBottom = math.abs(vertices[i].y - bottom) < 1e-6f;
					var onIsland = test.Channel && math.abs(vertices[i].y - island) < 1e-6f;
					Assert.That(onBottom || onIsland, Is.True, $"vertex {i} faces up off any floor");
				}

			} finally {
				UnityEngine.Object.DestroyImmediate(mesh);
			}
		}

		[TestCaseSource(nameof(BodyCases))]
		public void ShouldNotEmitADegenerateTriangle(BodyCase test)
		{
			// Slivers with no area shade unpredictably and break a bevel later.
			var mesh = BuildMesh(test, out _);
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
	}
}
