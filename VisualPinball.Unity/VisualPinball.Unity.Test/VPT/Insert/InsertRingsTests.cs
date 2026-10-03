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

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity.Test
{
	public class InsertRingsTests
	{
		private static List<float2> Square(float size) => new() {
			new float2(-size / 2f, -size / 2f), new float2(size / 2f, -size / 2f),
			new float2(size / 2f, size / 2f), new float2(-size / 2f, size / 2f),
		};

		[Test]
		public void ShouldRiseAndFallFromTheCentreOut()
		{
			var rings = InsertRings.Cover(Square(40f), float2.zero, 4f, 1.5f, -10f);
			Assert.That(rings.Count, Is.GreaterThanOrEqualTo(15));
			Assert.That(rings.Segments, Is.InRange(InsertRings.MinSegments, InsertRings.MaxSegments));
			// The centre is a ridge; a half pitch out a groove, a pitch out a ridge
			// again, measured along a sector's middle where the chord is nearest.
			var bisector = rings.Bisector(0);
			Assert.That(rings.DepthAt(float2.zero), Is.EqualTo(-8.5f).Within(1e-4f));
			Assert.That(rings.DepthAt(bisector * rings.Apothem(1)), Is.EqualTo(-11.5f).Within(1e-3f));
			Assert.That(rings.DepthAt(bisector * rings.Apothem(2)), Is.EqualTo(-8.5f).Within(1e-3f));
			Assert.That(rings.DepthAt(bisector * (rings.Apothem(1) + rings.Apothem(2)) * 0.5f), Is.EqualTo(-10f).Within(1e-3f), "halfway down a slope");
			// A corner of a ring sits at the ring's depth, whichever sector looks at it.
			var corner = rings.Corner(3, 5);
			Assert.That(rings.DepthInSector(3 * rings.Segments + 5, corner), Is.EqualTo(rings.RingDepth(3)).Within(1e-3f));
			Assert.That(rings.DepthInSector(3 * rings.Segments + 4, corner), Is.EqualTo(rings.RingDepth(3)).Within(1e-3f));
			Assert.That(rings.DepthInSector(2 * rings.Segments + 5, corner), Is.EqualTo(rings.RingDepth(3)).Within(1e-3f), "and from the strip inside it");
		}

		[Test]
		public void ShouldJudgeTheStripByTheChordNotTheCircle()
		{
			var rings = InsertRings.Cover(Square(40f), float2.zero, 4f, 1.5f, -10f);
			// Between a ring's side and the circle through its corners, a point is
			// already in the strip outside the side.
			var direction = rings.Bisector(2);
			var between = direction * (rings.Apothem(2) + rings.Radius(2)) * 0.5f;
			Assert.That(rings.Ring(between, 2), Is.EqualTo(2));
			var within = direction * (rings.Apothem(2) - 1e-3f);
			Assert.That(rings.Ring(within, 2), Is.EqualTo(1));
			// Either side of a line out of the centre, the two facets agree.
			var line = rings.Direction(7) * 9f;
			var left = rings.Direction(7 + 0) * 9f + rings.Direction(7) * 0f;
			Assert.That(rings.DepthAt(left + new float2(-line.y, line.x) * 1e-4f), Is.EqualTo(rings.DepthAt(left - new float2(-line.y, line.x) * 1e-4f)).Within(1e-3f));
		}

		[Test]
		public void ShouldTiltNeighbouringStripsOppositeWaysAndTurnRoundTheCentre()
		{
			var rings = InsertRings.Cover(Square(40f), float2.zero, 4f, 1.5f, -10f);
			var inner = rings.LocalNormal(2 * rings.Segments + 3);
			var outer = rings.LocalNormal(3 * rings.Segments + 3);
			Assert.That(inner.y, Is.GreaterThan(0.5f));
			Assert.That(math.length(inner.xz), Is.GreaterThan(0.3f), "a strip tilts");
			Assert.That(inner.x * outer.x + inner.z * outer.z, Is.LessThan(0f), "the next strip out tilts the other way");
			var opposite = rings.LocalNormal(2 * rings.Segments + 3 + rings.Segments / 2);
			Assert.That(inner.x * opposite.x + inner.z * opposite.z, Is.LessThan(0f), "and across the centre the same strip tilts back");
			Assert.That(math.abs(inner.y - opposite.y), Is.LessThan(1e-3f));
		}

		[Test]
		public void ShouldShadeAsAConeAcrossTheLinesOutOfTheCentre()
		{
			var rings = InsertRings.Cover(Square(40f), float2.zero, 4f, 1.5f, -10f);
			// On a line out of the centre, the facets either side agree on the
			// normal, which leans straight along the line.
			var corner = rings.Corner(2, 5) * 1.2f;
			var left = rings.LocalNormal(2 * rings.Segments + 4, corner);
			var right = rings.LocalNormal(2 * rings.Segments + 5, corner);
			Assert.That(math.distance(left, right), Is.LessThan(1e-4f));
			var radial = ((float3)InsertMeshGenerator.ToLocal(corner, 0f) - (float3)InsertMeshGenerator.ToLocal(float2.zero, 0f)).xz;
			var lean = math.normalize(left.xz);
			Assert.That(math.abs(math.dot(lean, math.normalize(radial))), Is.EqualTo(1f).Within(1e-4f));
			// The lean is the cone's: as steep as the chord plane, near enough.
			Assert.That(left.y, Is.EqualTo(rings.LocalNormal(2 * rings.Segments + 5).y).Within(5e-3f));
			// Halfway between two lines, the plane's normal and the cone's are one.
			var middle = rings.Bisector(5) * (rings.Apothem(2) + rings.Apothem(3)) * 0.5f;
			Assert.That(math.distance(rings.LocalNormal(2 * rings.Segments + 5, middle), rings.LocalNormal(2 * rings.Segments + 5)), Is.LessThan(5e-3f));
			// The centre has no direction out of it: each facet keeps its own there.
			Assert.That(math.distance(rings.LocalNormal(3, float2.zero), rings.LocalNormal(3)), Is.LessThan(1e-6f));
		}

		[Test]
		public void ShouldRollTheRingsOverWhenRounded()
		{
			var sharp = InsertRings.Cover(Square(40f), float2.zero, 4f, 1.5f, -10f);
			var round = InsertRings.Cover(Square(40f), float2.zero, 4f, 1.5f, -10f, 1f);
			Assert.That(sharp.Steps, Is.EqualTo(1));
			Assert.That(round.Steps, Is.EqualTo(InsertRelief.RoundedSteps));
			Assert.That(round.Count, Is.GreaterThan(sharp.Count * (InsertRelief.RoundedSteps - 1)));
			// The ridges and grooves keep their heights; between them the slope
			// bows out into a cosine, level with the middle plane a quarter pitch out.
			var bisector = round.Bisector(0);
			Assert.That(round.DepthAt(float2.zero), Is.EqualTo(-8.5f).Within(1e-3f));
			Assert.That(round.DepthAt(bisector * round.Apothem(round.Steps)), Is.EqualTo(-11.5f).Within(1e-3f), "the first groove");
			Assert.That(round.DepthAt(bisector * round.Apothem(2 * round.Steps)), Is.EqualTo(-8.5f).Within(1e-3f), "the next ridge");
			Assert.That(round.DepthAt(bisector * 1f), Is.EqualTo(-10f).Within(2e-2f), "level halfway down");
			Assert.That(round.DepthAt(bisector * round.Apothem(1)), Is.GreaterThan(math.lerp(-8.5f, -11.5f, 1f / round.Steps) + 0.1f), "and still high a step out, where a straight slope would already have dropped");
			// Rounded, the normal stands up at the ridge and leans most halfway
			// down; sharp, it leans the same on the whole slope.
			var ridge = round.Direction(3) * round.Radius(2 * round.Steps);
			var ridgeFacet = round.SectorOf(ridge + round.Direction(3) * 1e-3f);
			Assert.That(round.LocalNormal(ridgeFacet, ridge).y, Is.EqualTo(1f).Within(1e-3f));
			var halfway = round.Direction(3) * 1f;
			Assert.That(round.LocalNormal(round.SectorOf(halfway), halfway).y, Is.LessThan(0.8f));
			Assert.That(sharp.LocalNormal(sharp.SectorOf(halfway), halfway).y, Is.EqualTo(sharp.LocalNormal(sharp.SectorOf(ridge + sharp.Direction(3) * 1e-3f), ridge + sharp.Direction(3) * 1e-3f).y).Within(1e-4f));
			// Half rounded, a ridge still has a crease: the facets either side lean apart.
			var half = InsertRings.Cover(Square(40f), float2.zero, 4f, 1.5f, -10f, 0.5f);
			var ridgeHalf = half.Direction(3) * half.Radius(2 * half.Steps);
			var outer = half.LocalNormal(half.SectorOf(ridgeHalf + half.Direction(3) * 1e-3f), ridgeHalf);
			var inner = half.LocalNormal(half.SectorOf(ridgeHalf - half.Direction(3) * 1e-3f), ridgeHalf);
			Assert.That(outer.y, Is.LessThan(0.99f));
			Assert.That(outer.x * inner.x + outer.z * inner.z, Is.LessThan(0f));
		}

		[Test]
		public void ShouldLeaveAFlatDiscWhereTheRingsStart()
		{
			var rings = InsertRings.Cover(Square(40f), float2.zero, 4f, 1.5f, -10f, 0f, 5f);
			Assert.That(rings.Start, Is.EqualTo(4f), "rounded to whole pitches");
			Assert.That(InsertRings.SnapStart(6.5f, 4f), Is.EqualTo(8f));
			Assert.That(InsertRings.SnapStart(1f, 4f), Is.EqualTo(0f));
			Assert.That(rings.Radius(0), Is.EqualTo(4f));
			Assert.That(rings.Radius(2), Is.EqualTo(8f));
			// The disc is flat at the ridges' height; the first groove lies a half
			// pitch beyond its edge, the next ridge a pitch beyond.
			var bisector = rings.Bisector(0);
			Assert.That(rings.DepthAt(float2.zero), Is.EqualTo(-8.5f).Within(1e-4f));
			Assert.That(rings.DepthAt(bisector * 3f), Is.EqualTo(-8.5f).Within(1e-4f));
			Assert.That(rings.DepthAt(bisector * rings.Apothem(1)), Is.EqualTo(-11.5f).Within(1e-3f));
			Assert.That(rings.DepthAt(bisector * rings.Apothem(2)), Is.EqualTo(-8.5f).Within(1e-3f));
			var disc = rings.SectorOf(bisector * 3f);
			Assert.That(disc / rings.Segments, Is.EqualTo(rings.DiscRing));
			Assert.That(rings.LocalNormal(disc).y, Is.EqualTo(1f).Within(1e-6f));
			Assert.That(rings.LocalNormal(disc, bisector * 3f).y, Is.EqualTo(1f).Within(1e-6f));
			// Just outside the disc's edge the first slope drops away from the centre.
			var outside = bisector * (rings.Apothem(0) + 0.1f);
			var normal = rings.LocalNormal(rings.SectorOf(outside), outside);
			Assert.That(normal.y, Is.LessThan(0.99f));
			// The disc's edge is laid as a crease and the lines out of the centre
			// start there, sharing nothing.
			var parts = rings.CreasesWithin(Square(40f), 1e-4f).ToList();
			Assert.That(parts.Any(p => p.FromCenter), Is.False);
			var edge = parts.Where(p => math.abs(math.length(p.From) - 4f) < 1e-3f && math.abs(math.length(p.To) - 4f) < 1e-3f).ToList();
			Assert.That(edge.Count, Is.EqualTo(rings.Segments));
			Assert.That(parts.Count(p => math.abs(math.length(p.From) - 4f) < 1e-3f && math.length(p.To) > 4.5f), Is.EqualTo(rings.Segments), "a line out from every corner of the disc");
			Assert.That(parts.All(p => math.length(p.From) > 4f - 1e-3f && math.length(p.To) > 4f - 1e-3f), "and nothing inside it");
		}

		[Test]
		public void ShouldLayRingsAndLinesIntoTheFloor()
		{
			var rings = InsertRings.Cover(Square(40f), float2.zero, 4f, 1.5f, -10f);
			var parts = rings.CreasesWithin(Square(40f), 1e-4f).ToList();
			var fromCentre = parts.Where(p => p.FromCenter).ToList();
			Assert.That(fromCentre.Count, Is.EqualTo(rings.Segments), "every line out of the centre starts there, the centre being inside");
			var sides = parts.Where(p => !p.FromCenter).ToList();
			// The innermost rings lie wholly inside: all their sides come back whole.
			var ring1 = sides.Where(p => math.abs(math.distance(p.From, float2.zero) - rings.Radius(1)) < 1e-3f && math.abs(math.distance(p.To, float2.zero) - rings.Radius(1)) < 1e-3f).ToList();
			Assert.That(ring1.Count, Is.EqualTo(rings.Segments));
			// Every part lies inside the square.
			foreach (var (from, to, _) in parts) {
				Assert.That(math.cmax(math.abs(from)), Is.LessThanOrEqualTo(20f + 1e-3f));
				Assert.That(math.cmax(math.abs(to)), Is.LessThanOrEqualTo(20f + 1e-3f));
			}
			// A ring that crosses the edge comes back only where it is inside.
			var outerSides = sides.Where(p => math.distance(p.From, float2.zero) > 20f - 1e-3f || math.distance(p.To, float2.zero) > 20f - 1e-3f).ToList();
			Assert.That(outerSides.Count, Is.GreaterThan(0));
			Assert.That(math.all(rings.BridgeDirection(new float2(3f, 4f)) == math.normalize(new float2(3f, 4f))));
		}

		[Test]
		public void ShouldDescribeTheRingsToTheMaterial()
		{
			var rings = InsertRings.Cover(Square(40f), new float2(5f, -3f), 4f, 1.5f, -10f);
			var optical = rings.Optical();
			var halfPitch = (InsertMeshGenerator.ToLocal(new float2(2f, 0f)) - InsertMeshGenerator.ToLocal(float2.zero)).magnitude;
			Assert.That(optical.z, Is.EqualTo(-1000f - halfPitch).Within(1e-6f), "the half pitch past minus a thousand, so the material knows rings from ribs and a wheel");
			var centre = InsertMeshGenerator.ToLocal(new float2(5f, -3f), -11.5f);
			Assert.That(optical.x, Is.EqualTo(centre.x).Within(1e-7f));
			Assert.That(optical.y, Is.EqualTo(centre.z).Within(1e-7f));
			Assert.That(optical.w, Is.EqualTo(centre.y).Within(1e-7f), "the lowest height");
		}

		[Test]
		public void ShouldBuildARingedFloor()
		{
			var outline = InsertOutlineBuilder.Build(InsertShapes.CircleCorners(40f, 48));
			var inset = outline.ClampInset(3f);
			var body = new InsertBody {
				Depth = 20f,
				ChannelWidth = inset,
				ChannelDepth = 4f,
				RingWidth = 4f,
				RingCenter = float2.zero,
				RingOffset = 1.5f,
				Holes = new[] { new InsertHole(new float2(9f, 0f), 2f) },
				HoleSegments = 12,
			};
			Assert.That(body.HasRelief, Is.True);
			Assert.That(body.ReliefOffset, Is.EqualTo(1.5f));
			var layout = new InsertMeshLayout();
			var mesh = InsertMeshGenerator.Generate(outline, body, new Mesh(), layout);
			try {
				Assert.That(layout.FlatTriangles.Length, Is.GreaterThan(100));
				Assert.That(layout.OpticalStarburst.z, Is.LessThan(-1000f), "the bake is told these are rings");
				var normals = mesh.normals;
				var triangles = mesh.triangles;
				var vertices = mesh.vertices;
				var inward = 0;
				var outward = 0;
				var centre = InsertMeshGenerator.ToLocal(float2.zero, -16f);
				var rise = InsertMeshGenerator.ToLocal(float2.zero, 1.5f).y - InsertMeshGenerator.ToLocal(float2.zero, 0f).y;
				foreach (var t in layout.FlatTriangles) {
					var n = normals[triangles[t * 3]];
					if (n.y > 0.999f) {
						continue;
					}
					var a = vertices[triangles[t * 3]];
					var radial = new Vector2(a.x - centre.x, a.z - centre.z).normalized;
					var lean = n.x * radial.x + n.z * radial.y;
					if (lean < -0.1f) { inward++; } else if (lean > 0.1f) { outward++; }
					for (var c = 0; c < 3; c++) {
						Assert.That(math.abs(vertices[triangles[t * 3 + c]].y - centre.y), Is.LessThanOrEqualTo(rise + 1e-5f), "every point of the floor lies between the grooves and the ridges");
					}
				}
				Assert.That(inward, Is.GreaterThan(20), "strips leaning in towards the centre");
				Assert.That(outward, Is.GreaterThan(20), "and out away from it");
			} finally {
				Object.DestroyImmediate(mesh);
			}
		}

		[Test]
		public void ShouldChooseRingsFromTheComponent()
		{
			var go = new GameObject("Ringed insert");
			try {
				var insert = go.AddComponent<InsertComponent>();
				var settings = new UnityEditor.SerializedObject(insert);
				settings.FindProperty("_starburst").boolValue = false;
				settings.FindProperty("_ringed").boolValue = true;
				settings.FindProperty("_ringWidth").floatValue = 3f;
				settings.FindProperty("_ringCenter").vector2Value = new Vector2(2f, 1f);
				settings.FindProperty("_ringOffset").floatValue = 100f;
				settings.ApplyModifiedPropertiesWithoutUndo();
				insert.RebuildMesh();
				Assert.That(insert.Error, Is.Null);
				Assert.That(insert.Body.RingWidth, Is.EqualTo(3f));
				Assert.That(insert.Body.RibWidth, Is.EqualTo(0f));
				Assert.That(insert.Body.StarburstBursts, Is.EqualTo(0));
				Assert.That(insert.Body.RingOffset, Is.EqualTo(insert.Body.StarburstLimit()).Within(1e-5f), "held to the bucket");
				Assert.That(insert.Warnings, Has.Some.Contains("Ring offset reduced"));
				// Ribs come before rings when both flags are stored.
				settings.FindProperty("_ribbed").boolValue = true;
				settings.ApplyModifiedPropertiesWithoutUndo();
				insert.RebuildMesh();
				Assert.That(insert.Body.RibWidth, Is.GreaterThan(0f));
				Assert.That(insert.Body.RingWidth, Is.EqualTo(0f));
			} finally {
				Object.DestroyImmediate(go.GetComponent<MeshFilter>().sharedMesh);
				Object.DestroyImmediate(go);
			}
		}
	}
}
