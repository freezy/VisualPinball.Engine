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

		private static InsertGem Brilliant() => InsertGem.Cover(Square(40f), float2.zero, 8, 0.5f, 1.5f, -10f);

		[Test]
		public void ShouldSitTheTableFlatAndSlopeTheFacetsOutward()
		{
			var gem = Brilliant();
			Assert.That(gem.Sides, Is.EqualTo(8));
			Assert.That(gem.Reach, Is.GreaterThan(20f * math.SQRT2), "the girdle lies beyond the square's corners");
			Assert.That(gem.TableRadius, Is.EqualTo(gem.Reach * 0.5f).Within(1e-4f));
			// The table is a plateau the offset above the middle plane.
			Assert.That(gem.DepthAt(float2.zero), Is.EqualTo(-8.5f).Within(1e-4f));
			Assert.That(gem.DepthAt(gem.Direction(3) * (gem.TableRadius - 1e-3f)), Is.EqualTo(-8.5f).Within(1e-3f));
			// Out along a corner's line the kite slopes straight down to the girdle.
			var halfway = gem.Direction(3) * math.lerp(gem.TableRadius, gem.Reach, 0.5f);
			Assert.That(gem.DepthAt(halfway), Is.EqualTo(-10f).Within(1e-3f));
			Assert.That(gem.DepthAt(gem.Direction(3) * (gem.Reach - 1e-3f)), Is.EqualTo(-11.5f).Within(1e-2f));
			// Out along a junction's line the star facet drops to the junction and
			// the girdle facets on from there, the junction between the two.
			Assert.That(gem.JunctionDepth, Is.LessThan(-8.5f));
			Assert.That(gem.JunctionDepth, Is.GreaterThan(-11.5f));
			Assert.That(gem.DepthAt(gem.Junction(2)), Is.EqualTo(gem.JunctionDepth).Within(1e-3f));
			var apothem = gem.TableRadius * math.cos(math.PI / 8f);
			Assert.That(gem.DepthAt(gem.Bisector(2) * math.lerp(apothem, gem.JunctionRadius, 0.5f)), Is.EqualTo(math.lerp(-8.5f, gem.JunctionDepth, 0.5f)).Within(1e-3f));
			Assert.That(gem.DepthAt(gem.Bisector(2) * math.lerp(gem.JunctionRadius, gem.Reach, 0.5f)), Is.EqualTo(math.lerp(gem.JunctionDepth, -11.5f, 0.5f)).Within(1e-3f));
		}

		[Test]
		public void ShouldNameTheFacetUnderEachPoint()
		{
			var gem = Brilliant();
			Assert.That(gem.SectorOf(float2.zero), Is.EqualTo(InsertGem.TableSector));
			Assert.That(gem.SectorOf(gem.Direction(5) * (gem.TableRadius - 1e-2f)), Is.EqualTo(InsertGem.TableSector), "just inside a corner");
			Assert.That(gem.SectorOf(gem.Direction(5) * (gem.TableRadius + 1e-2f)), Is.EqualTo(gem.KiteSector(5)), "just beyond a corner");
			var apothem = gem.TableRadius * math.cos(math.PI / 8f);
			Assert.That(gem.SectorOf(gem.Bisector(5) * (apothem + 1e-2f)), Is.EqualTo(gem.StarSector(5)), "just beyond a side");
			Assert.That(gem.SectorOf(gem.Bisector(5) * (gem.JunctionRadius + 1e-2f)), Is.EqualTo(gem.GirdleSector(5, true)).Or.EqualTo(gem.GirdleSector(5, false)), "just beyond a junction");
			Assert.That(gem.SectorOf(gem.Bisector(5) * (gem.Reach - 1e-2f) + new float2(-gem.Bisector(5).y, gem.Bisector(5).x) * 0.1f), Is.EqualTo(gem.GirdleSector(5, true)), "past the junction's line, anticlockwise");
			Assert.That(gem.SectorOf(gem.Bisector(5) * (gem.Reach - 1e-2f) - new float2(-gem.Bisector(5).y, gem.Bisector(5).x) * 0.1f), Is.EqualTo(gem.GirdleSector(5, false)), "and clockwise");
			// A kite is named the same from both halves of it.
			var side = new float2(-gem.Direction(5).y, gem.Direction(5).x) * 0.5f;
			var onAxis = gem.Direction(5) * math.lerp(gem.TableRadius, gem.Reach, 0.3f);
			Assert.That(gem.SectorOf(onAxis + side), Is.EqualTo(gem.KiteSector(5)));
			Assert.That(gem.SectorOf(onAxis - side), Is.EqualTo(gem.KiteSector(5)));
			Assert.That(gem.SectorCount, Is.EqualTo(33));
		}

		[Test]
		public void ShouldJoinTheFacetsAlongTheirSides()
		{
			var gem = Brilliant();
			void Agree(int a, int b, float2 from, float2 to, string where)
			{
				for (var t = 0.1f; t < 1f; t += 0.2f) {
					var point = math.lerp(from, to, t);
					Assert.That(gem.DepthInSector(a, point), Is.EqualTo(gem.DepthInSector(b, point)).Within(1e-4f), where);
				}
			}
			Agree(InsertGem.TableSector, gem.StarSector(1), gem.Table(1), gem.Table(2), "the table's side");
			Agree(gem.StarSector(1), gem.KiteSector(1), gem.Table(1), gem.Junction(1), "a star facet's side");
			Agree(gem.StarSector(1), gem.KiteSector(2), gem.Table(2), gem.Junction(1), "its other side");
			Agree(gem.KiteSector(1), gem.GirdleSector(1, false), gem.Junction(1), gem.Girdle(1), "a kite's outer side");
			Agree(gem.KiteSector(2), gem.GirdleSector(1, true), gem.Junction(1), gem.Girdle(2), "the next kite's");
			Agree(gem.GirdleSector(1, false), gem.GirdleSector(1, true), gem.Junction(1), gem.GirdleMiddle(1), "between the girdle facets");
			// All the way round, not only on the first corners.
			for (var i = 0; i < gem.Sides; i++) {
				Agree(gem.KiteSector(i), gem.GirdleSector(i, false), gem.Junction(i), gem.Girdle(i), $"kite {i}'s outer side");
				Agree(gem.KiteSector(i + 1), gem.GirdleSector(i, true), gem.Junction(i), gem.Girdle(i + 1), $"kite {i + 1}'s inner side");
				Agree(gem.GirdleSector(i, false), gem.GirdleSector(i, true), gem.Junction(i), gem.GirdleMiddle(i), $"girdle {i}");
				Assert.That(gem.DepthInSector(gem.GirdleSector(i, true), gem.GirdleMiddle(i)), Is.EqualTo(gem.GirdleDepth).Within(1e-4f), $"girdle {i}'s middle");
			}
			// A kite is one plane: the junction on its other side lies on it too.
			Assert.That(gem.DepthInSector(gem.KiteSector(1), gem.Junction(0)), Is.EqualTo(gem.JunctionDepth).Within(1e-4f));
			// Every facet but the table tilts, and leans away from the centre.
			for (var sector = 1; sector < gem.SectorCount; sector++) {
				var normal = gem.LocalNormal(sector);
				Assert.That(normal.y, Is.InRange(0.5f, 0.999f), $"facet {sector}");
			}
			Assert.That(gem.LocalNormal(InsertGem.TableSector).y, Is.EqualTo(1f).Within(1e-6f));
		}

		[Test]
		public void ShouldLayEveryFacetSideIntoTheFloor()
		{
			var gem = Brilliant();
			var parts = gem.CreasesWithin(Square(40f), 1e-4f).ToList();
			Assert.That(parts.Any(p => p.FromCenter), Is.False);
			// The table lies wholly inside: all its sides come back whole.
			var tableSides = parts.Where(p => math.abs(math.length(p.From) - gem.TableRadius) < 1e-3f && math.abs(math.length(p.To) - gem.TableRadius) < 1e-3f).ToList();
			Assert.That(tableSides.Count, Is.EqualTo(8));
			// Six sides a corner, every one at least partly inside.
			Assert.That(parts.Count, Is.EqualTo(48));
			foreach (var (from, to, _) in parts) {
				Assert.That(math.cmax(math.abs(from)), Is.LessThanOrEqualTo(20f + 1e-3f));
				Assert.That(math.cmax(math.abs(to)), Is.LessThanOrEqualTo(20f + 1e-3f));
			}
			Assert.That(math.all(gem.BridgeDirection(new float2(3f, 4f)) == math.normalize(new float2(3f, 4f))));
		}

		[Test]
		public void ShouldHoldTheTableSmallEnoughForTheKitesToSlopeOutward()
		{
			var gem = InsertGem.Cover(Square(40f), float2.zero, 8, 0.9f, 1.5f, -10f);
			Assert.That(gem.TableRadius / gem.Reach, Is.EqualTo(InsertGem.TableLimit(8)).Within(1e-5f));
			Assert.That(gem.JunctionRadius * math.cos(math.PI / 8f), Is.GreaterThan(gem.TableRadius), "the junctions lie beyond the corners along the kites");
			Assert.That(gem.JunctionDepth, Is.LessThan(gem.TableDepth));
			var few = InsertGem.Cover(Square(40f), float2.zero, 2, 0.5f, 1.5f, -10f);
			Assert.That(few.Sides, Is.EqualTo(InsertGem.MinSides));
			Assert.That(few.JunctionRadius * math.cos(math.PI / few.Sides), Is.GreaterThan(few.TableRadius));
		}

		[Test]
		public void ShouldDescribeTheGemToTheMaterial()
		{
			var gem = InsertGem.Cover(Square(40f), new float2(5f, -3f), 8, 0.5f, 1.5f, -10f);
			var optical = gem.Optical();
			Assert.That(optical.z, Is.EqualTo(2008f), "the sides past two thousand, so the material knows a gem from a wheel");
			var centre = InsertMeshGenerator.ToLocal(new float2(5f, -3f), -11.5f);
			Assert.That(optical.x, Is.EqualTo(centre.x).Within(1e-7f));
			Assert.That(optical.y, Is.EqualTo(centre.z).Within(1e-7f));
			Assert.That(optical.w, Is.EqualTo(centre.y).Within(1e-7f), "the lowest height");
		}

		[Test]
		public void ShouldBuildAGemFloor()
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
				GemOffset = 1.5f,
				Holes = new[] { new InsertHole(new float2(9f, 0f), 2f) },
				HoleSegments = 12,
			};
			Assert.That(body.HasRelief, Is.True);
			Assert.That(body.ReliefOffset, Is.EqualTo(1.5f));
			var layout = new InsertMeshLayout();
			var mesh = InsertMeshGenerator.Generate(outline, body, new Mesh(), layout);
			try {
				Assert.That(layout.FlatTriangles.Length, Is.GreaterThan(40));
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
						Assert.That(math.abs(vertices[triangles[t * 3 + c]].y - centre.y), Is.LessThanOrEqualTo(rise + 1e-5f), "every point of the floor lies between the girdle and the table");
					}
				}
				Assert.That(flat, Is.GreaterThan(5), "the table");
				Assert.That(outward, Is.GreaterThan(20), "and the facets leaning away from the centre");
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
				settings.FindProperty("_gemOffset").floatValue = 100f;
				settings.ApplyModifiedPropertiesWithoutUndo();
				insert.RebuildMesh();
				Assert.That(insert.Error, Is.Null);
				Assert.That(insert.Body.GemSides, Is.EqualTo(6));
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
