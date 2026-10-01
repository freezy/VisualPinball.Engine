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
	public class InsertGemTests
	{
		private static List<float2> Square(float size) => new() {
			new float2(-size / 2f, -size / 2f), new float2(size / 2f, -size / 2f),
			new float2(size / 2f, size / 2f), new float2(-size / 2f, size / 2f),
		};

		private static InsertGem OneRow() => InsertGem.Cover(Square(40f), float2.zero, 8, 0.5f, 0f, 0f, 1.5f, -10f);
		private static InsertGem Rowed() => InsertGem.Cover(Square(40f), float2.zero, 8, 0.4f, 5f, 0f, 1.5f, -10f);

		[Test]
		public void ShouldRunOneRowStraightToTheEdge()
		{
			var gem = OneRow();
			Assert.That(gem.Sides, Is.EqualTo(8));
			Assert.That(gem.Rows, Is.EqualTo(1));
			Assert.That(gem.Reach * math.cos(math.PI / 8f), Is.GreaterThan(20f * math.SQRT2), "the outermost ring's sides clear the square's corners");
			Assert.That(gem.Radius(1), Is.EqualTo(gem.Reach).Within(1e-4f));
			Assert.That(gem.TableRadius, Is.EqualTo(gem.Reach * 0.5f).Within(1e-4f));
			// The table is a plateau the offset above the middle plane; the one
			// row drops from it straight to the edge.
			Assert.That(gem.DepthAt(float2.zero), Is.EqualTo(-8.5f).Within(1e-4f));
			Assert.That(gem.DepthAt(gem.Direction(0, 3) * (gem.TableRadius - 1e-3f)), Is.EqualTo(-8.5f).Within(1e-3f));
			Assert.That(gem.RingDepth(1), Is.EqualTo(-11.5f).Within(1e-5f));
			var apothem = gem.TableRadius * math.cos(math.PI / 8f);
			var halfway = gem.Direction(1, 2) * math.lerp(apothem, gem.Reach, 0.5f);
			Assert.That(gem.DepthAt(halfway), Is.EqualTo(-10f).Within(1e-3f), "halfway out along an outward facet's middle");
			Assert.That(gem.SectorCount, Is.EqualTo(17));
		}

		[Test]
		public void ShouldAddARingEveryRowLength()
		{
			var gem = Rowed();
			var apothem = gem.TableRadius * math.cos(math.PI / 8f);
			Assert.That(gem.Rows, Is.EqualTo((int)math.ceil((gem.Reach - apothem) / 5f)));
			Assert.That(gem.Rows, Is.GreaterThanOrEqualTo(3));
			Assert.That(gem.Radius(1), Is.EqualTo(apothem + 5f).Within(1e-4f));
			Assert.That(gem.Radius(2), Is.EqualTo(apothem + 10f).Within(1e-4f));
			Assert.That(gem.Radius(gem.Rows), Is.EqualTo(gem.Reach).Within(1e-4f), "the last ring is the reach, the row kept whole");
			// Each ring is turned half a side on from the one before.
			Assert.That(math.dot(gem.Direction(0, 0), gem.Direction(1, 0)), Is.EqualTo(math.cos(math.PI / 8f)).Within(1e-5f));
			Assert.That(math.distance(gem.Direction(0, 1), gem.Direction(2, 1)), Is.LessThan(1e-6f));
			// A step down a ring.
			for (var ring = 0; ring < gem.Rows; ring++) {
				Assert.That(gem.RingDepth(ring + 1), Is.EqualTo(gem.RingDepth(ring) - 3f / gem.Rows).Within(1e-4f));
			}
			Assert.That(gem.SectorCount, Is.EqualTo(1 + gem.Rows * 16));
		}

		[Test]
		public void ShouldNameTheFacetUnderEachPoint()
		{
			var gem = Rowed();
			Assert.That(gem.SectorOf(float2.zero), Is.EqualTo(InsertGem.TableSector));
			Assert.That(gem.SectorOf(gem.Direction(0, 5) * (gem.TableRadius - 1e-2f)), Is.EqualTo(InsertGem.TableSector), "just inside a corner");
			Assert.That(gem.SectorOf(gem.Direction(0, 5) * (gem.TableRadius + 1e-2f)), Is.EqualTo(gem.InwardSector(0, 5)), "just beyond a corner: the facet off that point");
			var apothem = gem.TableRadius * math.cos(math.PI / 8f);
			Assert.That(gem.SectorOf(gem.SideDirection(0, 5) * (apothem + 1e-2f)), Is.EqualTo(gem.OutwardSector(0, 5)), "just beyond a side: the facet off that side");
			// The first ring's points lie on the sides' middles; just beyond one
			// is the facet off that point, just short of it the outward facet.
			var point = gem.Point(1, 5);
			Assert.That(gem.SectorOf(point * (1f + 1e-3f)), Is.EqualTo(gem.InwardSector(1, 5)));
			Assert.That(gem.SectorOf(point * (1f - 1e-3f)), Is.EqualTo(gem.OutwardSector(0, 5)));
			// Just beyond a second ring point, off that point again, and the
			// outward facet between two of its points.
			Assert.That(gem.SectorOf(gem.Point(2, 3) * (1f + 1e-3f)), Is.EqualTo(gem.InwardSector(2, 3)));
			Assert.That(gem.SectorOf(gem.SideDirection(2, 3) * (gem.Radius(2) * math.cos(math.PI / 8f) + 1e-2f)), Is.EqualTo(gem.OutwardSector(2, 3)));
			// Either side of a ring's side, the facets off it agree on the name
			// from both of the row's halves.
			var side = gem.Direction(1, 2);
			var onLine = side * (gem.Radius(1) + gem.RowLength * 0.3f);
			var across = new float2(-side.y, side.x) * 0.2f;
			Assert.That(gem.SectorOf(onLine + across), Is.EqualTo(gem.InwardSector(1, 2)));
			Assert.That(gem.SectorOf(onLine - across), Is.EqualTo(gem.InwardSector(1, 2)));
		}

		[Test]
		public void ShouldJoinTheFacetsAlongTheirSides()
		{
			var gem = Rowed();
			void Agree(int a, int b, float2 from, float2 to, string where)
			{
				for (var t = 0.1f; t < 1f; t += 0.2f) {
					var point = math.lerp(from, to, t);
					Assert.That(gem.DepthInSector(a, point), Is.EqualTo(gem.DepthInSector(b, point)).Within(1e-4f), where);
				}
			}
			for (var ring = 0; ring < gem.Rows; ring++) {
				for (var i = 0; i < gem.Sides; i++) {
					var shift = ring & 1;
					// A ring's side: the outward facet off it and, inside, the
					// table or the facet off the point of the ring before.
					var inside = ring == 0 ? InsertGem.TableSector : gem.InwardSector(ring - 1, i + 1 - ((ring - 1) & 1));
					Agree(inside, gem.OutwardSector(ring, i), gem.Point(ring, i), gem.Point(ring, i + 1), $"ring {ring} side {i}");
					// The zigzag either side of the outward facet.
					Agree(gem.OutwardSector(ring, i), gem.InwardSector(ring, i), gem.Point(ring, i), gem.Point(ring + 1, i + shift), $"ring {ring} zig {i}");
					Agree(gem.OutwardSector(ring, i), gem.InwardSector(ring, i + 1), gem.Point(ring, i + 1), gem.Point(ring + 1, i + shift), $"ring {ring} zag {i}");
				}
			}
			// Every facet but the table tilts away from the centre.
			for (var sector = 1; sector < gem.SectorCount; sector++) {
				var normal = gem.LocalNormal(sector);
				Assert.That(normal.y, Is.InRange(0.5f, 0.9999f), $"facet {sector}");
				var (a, b, c) = gem.Corners(sector);
				var middle = InsertMeshGenerator.ToLocal((a.xy + b.xy + c.xy) / 3f, 0f) - InsertMeshGenerator.ToLocal(float2.zero, 0f);
				Assert.That(normal.x * middle.x + normal.z * middle.z, Is.GreaterThan(0f), $"facet {sector} leans outward");
			}
			Assert.That(gem.LocalNormal(InsertGem.TableSector).y, Is.EqualTo(1f).Within(1e-6f));
		}

		[Test]
		public void ShouldLayEveryFacetSideIntoTheFloor()
		{
			var gem = OneRow();
			var parts = gem.CreasesWithin(Square(40f), 1e-4f).ToList();
			Assert.That(parts.Any(p => p.FromCenter), Is.False);
			// The table lies wholly inside: all its sides come back whole, and
			// every line out of its corners reaches the square's edge.
			var tableSides = parts.Where(p => math.abs(math.length(p.From) - gem.TableRadius) < 1e-3f && math.abs(math.length(p.To) - gem.TableRadius) < 1e-3f).ToList();
			Assert.That(tableSides.Count, Is.EqualTo(8));
			var outward = parts.Where(p => math.cmax(math.abs(p.To)) > 20f - 1e-3f || math.cmax(math.abs(p.From)) > 20f - 1e-3f).ToList();
			Assert.That(outward.Count, Is.EqualTo(16));
			Assert.That(parts.Count, Is.EqualTo(24), "and nothing of the outermost ring, which lies beyond");
			foreach (var (from, to, _) in parts) {
				Assert.That(math.cmax(math.abs(from)), Is.LessThanOrEqualTo(20f + 1e-3f));
				Assert.That(math.cmax(math.abs(to)), Is.LessThanOrEqualTo(20f + 1e-3f));
			}
			Assert.That(Rowed().CreasesWithin(Square(40f), 1e-4f).Count(), Is.GreaterThan(24 + 16));
			Assert.That(math.all(gem.BridgeDirection(new float2(3f, 4f)) == math.normalize(new float2(3f, 4f))));
		}

		[Test]
		public void ShouldHoldTheTableAndTheSides()
		{
			var gem = InsertGem.Cover(Square(40f), float2.zero, 2, 2f, 0f, 0f, 1.5f, -10f);
			Assert.That(gem.Sides, Is.EqualTo(InsertGem.MinSides));
			Assert.That(gem.TableRadius / gem.Reach, Is.EqualTo(InsertGem.MaxTable).Within(1e-5f));
			// A row length longer than the way out is one row to the edge.
			var one = InsertGem.Cover(Square(40f), float2.zero, 8, 0.5f, 100f, 0f, 1.5f, -10f);
			Assert.That(one.Rows, Is.EqualTo(1));
			Assert.That(one.Radius(1), Is.EqualTo(one.Reach).Within(1e-4f));
			// A row length near zero asks for no more than the most rows, each at
			// least the shortest row long.
			var many = InsertGem.Cover(Square(40f), float2.zero, 8, 0.5f, 0.001f, 0f, 1.5f, -10f);
			Assert.That(many.Rows, Is.EqualTo(InsertGem.MaxRows));
			Assert.That(many.RowLength, Is.GreaterThanOrEqualTo(InsertGem.MinRowLength));
			Assert.That(many.Radius(many.Rows), Is.EqualTo(many.Reach).Within(1e-4f));
		}

		[Test]
		public void ShouldSpreadTheLastRowsLinesEvenlyAlongTheEdge()
		{
			var paired = OneRow();
			var even = InsertGem.Cover(Square(40f), float2.zero, 8, 0.5f, 0f, 1f, 1.5f, -10f);
			// Unspread, the two lines from neighbouring corners meet at one point
			// of the outermost ring; spread, every point is a side's quarter
			// turn from the next, sixteen of them evenly round.
			Assert.That(math.distance(paired.Edge(2, true), paired.Edge(3, false)), Is.LessThan(1e-4f));
			Assert.That(even.SpreadAngle, Is.EqualTo(math.PI / 16f).Within(1e-6f));
			for (var i = 0; i < 8; i++) {
				var a = even.Edge(i, false) - even.Center;
				var b = even.Edge(i, true) - even.Center;
				var c = even.Edge(i + 1, false) - even.Center;
				Assert.That(math.acos(math.dot(math.normalize(a), math.normalize(b))), Is.EqualTo(math.PI / 8f).Within(1e-4f));
				Assert.That(math.acos(math.dot(math.normalize(b), math.normalize(c))), Is.EqualTo(math.PI / 8f).Within(1e-4f));
			}
			// The facet off a side is a trapezoid now, and still one plane: its
			// fourth corner lies on it, at the edge's depth.
			Assert.That(even.DepthInSector(even.OutwardSector(0, 2), even.Edge(3, false)), Is.EqualTo(even.EdgeDepth).Within(1e-4f));
			Assert.That(even.DepthInSector(even.OutwardSector(0, 2), even.Edge(2, true)), Is.EqualTo(even.EdgeDepth).Within(1e-4f));
			// Just inside an edge point, the facet off the side lies towards the
			// side's middle and the facet off the corner the other way.
			var edge = even.Edge(2, true);
			var sideways = new float2(-edge.y, edge.x) / math.length(edge) * 1.5f;
			Assert.That(even.SectorOf(edge * 0.98f + sideways), Is.EqualTo(even.OutwardSector(0, 2)));
			Assert.That(even.SectorOf(edge * 0.98f - sideways), Is.EqualTo(even.InwardSector(0, 2)));
			// The outermost ring's sides still clear the square.
			Assert.That(even.Reach * math.cos(math.PI / 16f), Is.GreaterThan(20f * math.SQRT2));
			Assert.That(even.Reach, Is.LessThan(paired.Reach), "and need not lie as far out as the paired ring's longer sides");
			Assert.That(even.CreasesWithin(Square(40f), 1e-4f).Count(), Is.EqualTo(24));
		}

		[Test]
		public void ShouldDescribeTheGemToTheMaterial()
		{
			var gem = InsertGem.Cover(Square(40f), new float2(5f, -3f), 8, 0.5f, 0f, 0f, 1.5f, -10f);
			var optical = gem.Optical();
			Assert.That(optical.z, Is.EqualTo(2008f), "the sides past two thousand, so the material knows a gem from a wheel");
			var centre = InsertMeshGenerator.ToLocal(new float2(5f, -3f), -11.5f);
			Assert.That(optical.x, Is.EqualTo(centre.x).Within(1e-7f));
			Assert.That(optical.y, Is.EqualTo(centre.z).Within(1e-7f));
			Assert.That(optical.w, Is.EqualTo(centre.y).Within(1e-7f), "the lowest height");
		}

		[TestCase(0f)]
		[TestCase(4f)]
		public void ShouldBuildAGemFloor(float rowLength)
		{
			var outline = InsertOutlineBuilder.Build(InsertShapes.CircleCorners(40f, 48), 1);
			var inset = outline.ClampInset(3f);
			var body = new InsertBody {
				Depth = 20f,
				ChannelWidth = inset,
				ChannelDepth = 4f,
				GemSides = 8,
				GemCenter = float2.zero,
				GemTable = 0.5f,
				GemRowLength = rowLength,
				GemOffset = 1.5f,
				Holes = new[] { new InsertHole(new float2(9f, 0f), 2f) },
				HoleSegments = 12,
			};
			Assert.That(body.HasRelief, Is.True);
			Assert.That(body.ReliefOffset, Is.EqualTo(1.5f));
			var layout = new InsertMeshLayout();
			var mesh = InsertMeshGenerator.Generate(outline, body, new Mesh(), layout);
			try {
				Assert.That(layout.FlatTriangles.Length, Is.GreaterThan(30));
				Assert.That(layout.OpticalStarburst.z, Is.EqualTo(2008f), "the bake is told this is a gem");
				var normals = mesh.normals;
				var triangles = mesh.triangles;
				var vertices = mesh.vertices;
				var flat = 0;
				var outward = 0;
				var centre = InsertMeshGenerator.ToLocal(float2.zero, -16f);
				var rise = InsertMeshGenerator.ToLocal(float2.zero, 1.5f).y - InsertMeshGenerator.ToLocal(float2.zero, 0f).y;
				foreach (var t in layout.FlatTriangles) {
					var n = normals[triangles[t * 3]];
					var a = vertices[triangles[t * 3]];
					if (n.y > 0.999f) {
						flat++;
					} else {
						var radial = new Vector2(a.x - centre.x, a.z - centre.z).normalized;
						if (n.x * radial.x + n.z * radial.y > 0.1f) { outward++; }
					}
					for (var c = 0; c < 3; c++) {
						Assert.That(math.abs(vertices[triangles[t * 3 + c]].y - centre.y), Is.LessThanOrEqualTo(rise + 1e-5f), "every point of the floor lies between the edge and the table");
					}
				}
				Assert.That(flat, Is.GreaterThan(5), "the table");
				Assert.That(outward, Is.GreaterThan(15), "and the facets leaning away from the centre");
			} finally {
				Object.DestroyImmediate(mesh);
			}
		}

		[Test]
		public void ShouldChooseTheGemFromTheComponent()
		{
			var go = new GameObject("Gem insert");
			try {
				var insert = go.AddComponent<InsertComponent>();
				var settings = new UnityEditor.SerializedObject(insert);
				settings.FindProperty("_starburst").boolValue = false;
				settings.FindProperty("_gem").boolValue = true;
				settings.FindProperty("_gemSides").intValue = 6;
				settings.FindProperty("_gemCenter").vector2Value = new Vector2(2f, 1f);
				settings.FindProperty("_gemRowLength").floatValue = 3f;
				settings.FindProperty("_gemOffset").floatValue = 100f;
				settings.ApplyModifiedPropertiesWithoutUndo();
				insert.RebuildMesh();
				Assert.That(insert.Error, Is.Null);
				Assert.That(insert.Body.GemSides, Is.EqualTo(6));
				Assert.That(insert.Body.GemRowLength, Is.EqualTo(3f));
				Assert.That(insert.Body.RingWidth, Is.EqualTo(0f));
				Assert.That(insert.Body.RibWidth, Is.EqualTo(0f));
				Assert.That(insert.Body.StarburstBursts, Is.EqualTo(0));
				Assert.That(insert.Body.GemOffset, Is.EqualTo(insert.Body.StarburstLimit()).Within(1e-5f), "held to the bucket");
				Assert.That(insert.Warnings, Has.Some.Contains("Gem offset reduced"));
				// Rings come before the gem when both flags are stored.
				settings.FindProperty("_ringed").boolValue = true;
				settings.ApplyModifiedPropertiesWithoutUndo();
				insert.RebuildMesh();
				Assert.That(insert.Body.RingWidth, Is.GreaterThan(0f));
				Assert.That(insert.Body.GemSides, Is.EqualTo(0));
			} finally {
				Object.DestroyImmediate(go.GetComponent<MeshFilter>().sharedMesh);
				Object.DestroyImmediate(go);
			}
		}
	}
}
