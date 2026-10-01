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
	public class InsertRibsTests
	{
		private static List<float2> Rectangle(float width, float height) => new() {
			new float2(-width / 2f, -height / 2f), new float2(width / 2f, -height / 2f),
			new float2(width / 2f, height / 2f), new float2(-width / 2f, height / 2f),
		};

		[Test]
		public void ShouldLayACreaseEveryHalfPitchAcrossTheFloor()
		{
			var ribs = InsertRibs.Cover(Rectangle(40f, 30f), 90f, 4f, 1.5f, -10f);
			// Ribs along Y: creases are lines of constant X, two apart, the ones on
			// the edges themselves not counted, since nothing of them lies inside.
			// Ninety degrees is not exact in floating point, so a crease on the
			// edge itself may clip a sliver of it; the arrangement copes with
			// that, and only the ones inside are counted here.
			var parts = ribs.CreasesWithin(Rectangle(40f, 30f), 1e-4f).ToList();
			Assert.That(parts.Count(p => math.abs(p.From.x) < 19.99f), Is.EqualTo(19));
			foreach (var (from, to, fromCenter) in parts) {
				Assert.That(fromCenter, Is.False);
				Assert.That(math.abs(from.x), Is.LessThanOrEqualTo(20f + 1e-3f));
				Assert.That(from.x, Is.EqualTo(to.x).Within(1e-3f), "a crease runs along the ribs");
				Assert.That(math.abs(to.y - from.y), Is.EqualTo(30f).Within(1e-3f), "clear across the floor");
				Assert.That(math.abs(from.x) % 2f, Is.EqualTo(0f).Within(1e-3f).Or.EqualTo(2f).Within(1e-3f));
			}
		}

		[Test]
		public void ShouldRiseAndFallBetweenRidgesAndGrooves()
		{
			var ribs = InsertRibs.Cover(Rectangle(40f, 30f), 90f, 4f, 1.5f, -10f);
			// A ridge through the origin, a groove a half pitch over, and the next ridge a pitch over.
			Assert.That(ribs.DepthAt(new float2(0f, 3f)), Is.EqualTo(-8.5f).Within(1e-4f));
			Assert.That(ribs.DepthAt(new float2(2f, -7f)), Is.EqualTo(-11.5f).Within(1e-4f));
			Assert.That(ribs.DepthAt(new float2(4f, 0f)), Is.EqualTo(-8.5f).Within(1e-4f));
			Assert.That(ribs.DepthAt(new float2(1f, 0f)), Is.EqualTo(-10f).Within(1e-4f), "halfway down a slope");
			Assert.That(ribs.DepthAt(new float2(-3f, 5f)), Is.EqualTo(-10f).Within(1e-4f));
			// Either side of a crease the two strips agree, so the surface is closed.
			Assert.That(ribs.DepthAt(new float2(2f - 1e-4f, 0f)), Is.EqualTo(ribs.DepthAt(new float2(2f + 1e-4f, 0f))).Within(1e-3f));
			Assert.That(ribs.SectorOf(new float2(2.5f, 0f)), Is.EqualTo(ribs.SectorOf(new float2(3.9f, 9f))));
			Assert.That(ribs.SectorOf(new float2(2.5f, 0f)), Is.Not.EqualTo(ribs.SectorOf(new float2(1.5f, 0f))));
		}

		[Test]
		public void ShouldTiltNeighbouringStripsOppositeWays()
		{
			var ribs = InsertRibs.Cover(Rectangle(40f, 30f), 90f, 4f, 1.5f, -10f);
			var here = ribs.SectorOf(new float2(1f, 0f));
			var a = ribs.LocalNormal(here);
			var b = ribs.LocalNormal(here + 1);
			Assert.That(a.y, Is.GreaterThan(0.5f));
			Assert.That(b.y, Is.GreaterThan(0.5f));
			Assert.That(math.length(a.xz), Is.GreaterThan(0.3f), "a strip tilts");
			Assert.That(a.x * b.x + a.z * b.z, Is.LessThan(0f), "its neighbour tilts the other way");
			Assert.That(math.abs(a.y - b.y), Is.LessThan(1e-4f), "and as steeply");
			Assert.That(math.all(ribs.BridgeDirection(new float2(3f, 4f)) == ribs.Along));
		}

		[Test]
		public void ShouldFollowTheAngle()
		{
			var ribs = InsertRibs.Cover(Rectangle(40f, 30f), 30f, 4f, 1.5f, -10f);
			Assert.That(ribs.Along.x, Is.EqualTo(math.cos(math.radians(30f))).Within(1e-5f));
			Assert.That(math.dot(ribs.Along, ribs.Across), Is.EqualTo(0f).Within(1e-6f));
			foreach (var (from, to, _) in ribs.CreasesWithin(Rectangle(40f, 30f), 1e-4f)) {
				var direction = math.normalize(to - from);
				Assert.That(math.abs(math.dot(direction, ribs.Along)), Is.EqualTo(1f).Within(1e-4f));
			}
			// A notch in the floor cuts a crease into two parts.
			var notched = new List<float2> {
				new(-20f, -15f), new(20f, -15f), new(20f, 15f), new(2f, 15f), new(2f, 0f), new(-2f, 0f), new(-2f, 15f), new(-20f, 15f),
			};
			var straight = InsertRibs.Cover(notched, 0f, 4f, 1.5f, -10f);
			var acrossTheNotch = straight.CreasesWithin(notched, 1e-4f).Where(p => math.abs(p.From.y - 2f) < 1e-4f).ToList();
			Assert.That(acrossTheNotch.Count, Is.EqualTo(2), "the crease at y = 2 comes back in two pieces");
			var whole = straight.CreasesWithin(notched, 1e-4f).Where(p => math.abs(p.From.y + 2f) < 1e-4f).ToList();
			Assert.That(whole.Count, Is.EqualTo(1), "the one at y = -2 runs clear across");
		}

		[Test]
		public void ShouldRollTheRibsOverWhenRounded()
		{
			var sharp = InsertRibs.Cover(Rectangle(40f, 30f), 90f, 4f, 1.5f, -10f);
			var round = InsertRibs.Cover(Rectangle(40f, 30f), 90f, 4f, 1.5f, -10f, 1f);
			Assert.That(round.Steps, Is.EqualTo(InsertRelief.RoundedSteps));
			Assert.That(round.LastCrease - round.FirstCrease, Is.GreaterThan((sharp.LastCrease - sharp.FirstCrease) * (InsertRelief.RoundedSteps - 1)));
			// Ridge through the origin, groove a half pitch across, level a
			// quarter pitch across, and still high a step across.
			Assert.That(round.DepthAt(float2.zero), Is.EqualTo(-8.5f).Within(1e-3f));
			Assert.That(round.DepthAt(round.Across * 2f), Is.EqualTo(-11.5f).Within(1e-3f));
			Assert.That(round.DepthAt(round.Across * 1f), Is.EqualTo(-10f).Within(1e-3f));
			Assert.That(round.DepthAt(round.Across * 0.5f), Is.GreaterThan(math.lerp(-8.5f, -11.5f, 0.25f)));
			Assert.That(round.DepthAt(-round.Across * 2f), Is.EqualTo(-11.5f).Within(1e-3f), "and the same the other way");
			// The normal stands up at the ridge and leans most halfway down.
			var ridge = round.Across * 0f + round.Along * 3f;
			Assert.That(round.LocalNormal(round.SectorOf(ridge + round.Across * 1e-3f), ridge).y, Is.EqualTo(1f).Within(1e-3f));
			var halfway = round.Across * 1f + round.Along * 3f;
			var leaning = round.LocalNormal(round.SectorOf(halfway), halfway);
			Assert.That(leaning.y, Is.LessThan(0.8f));
			// Sharp ribs keep their planes.
			Assert.That(math.distance(sharp.LocalNormal(sharp.SectorOf(halfway), halfway), sharp.LocalNormal(sharp.SectorOf(halfway))), Is.LessThan(1e-6f));
		}

		[Test]
		public void ShouldDescribeTheRibsToTheMaterial()
		{
			var ribs = InsertRibs.Cover(Rectangle(40f, 30f), 90f, 4f, 1.5f, -10f);
			var optical = ribs.Optical();
			var halfPitch = (InsertMeshGenerator.ToLocal(new float2(2f, 0f)) - InsertMeshGenerator.ToLocal(float2.zero)).magnitude;
			Assert.That(optical.z, Is.EqualTo(-halfPitch).Within(1e-7f), "the half pitch in metres, negative so the material knows ribs from a wheel");
			Assert.That(new Vector2(optical.x, optical.y).magnitude, Is.EqualTo(1f).Within(1e-5f));
			Assert.That(optical.w, Is.EqualTo(InsertMeshGenerator.ToLocal(float2.zero, -11.5f).y).Within(1e-7f), "the lowest height");
		}

		[Test]
		public void ShouldBuildARibbedFloor()
		{
			var outline = InsertOutlineBuilder.Build(InsertShapes.RectangleCorners(40f, 30f, new InsertCornerSettings(3f, 6)), 1);
			var inset = outline.ClampInset(3f);
			var body = new InsertBody {
				Depth = 20f,
				ChannelWidth = inset,
				ChannelDepth = 4f,
				RibWidth = 4f,
				RibAngle = 90f,
				RibOffset = 1.5f,
				Holes = new[] { new InsertHole(new float2(8f, 5f), 2f) },
				HoleSegments = 12,
			};
			Assert.That(body.HasRelief, Is.True);
			Assert.That(body.ReliefOffset, Is.EqualTo(1.5f));
			var layout = new InsertMeshLayout();
			var mesh = InsertMeshGenerator.Generate(outline, body, new Mesh(), layout);
			try {
				Assert.That(layout.FlatTriangles.Length, Is.GreaterThan(10));
				Assert.That(layout.OpticalStarburst.z, Is.LessThan(0f), "the bake is told these are ribs");
				var normals = mesh.normals;
				var triangles = mesh.triangles;
				var left = 0;
				var right = 0;
				var surface = InsertMeshGenerator.ToLocal(float2.zero, -16f).y;
				var vertices = mesh.vertices;
				foreach (var t in layout.FlatTriangles) {
					var n = normals[triangles[t * 3]];
					if (n.y > 0.999f) {
						continue;
					}
					if (n.x < -0.1f) { left++; } else if (n.x > 0.1f) { right++; }
					// Every point of the floor lies between the grooves and the ridges.
					for (var c = 0; c < 3; c++) {
						var y = vertices[triangles[t * 3 + c]].y - surface;
						Assert.That(math.abs(y), Is.LessThanOrEqualTo(InsertMeshGenerator.ToLocal(float2.zero, 1.5f).y - InsertMeshGenerator.ToLocal(float2.zero, 0f).y + 1e-5f));
					}
				}
				Assert.That(left, Is.GreaterThan(5), "strips tilting one way");
				Assert.That(right, Is.GreaterThan(5), "and the other");
			} finally {
				Object.DestroyImmediate(mesh);
			}
		}

		[Test]
		public void ShouldChooseRibsFromTheComponent()
		{
			var go = new GameObject("Ribbed insert");
			try {
				var insert = go.AddComponent<InsertComponent>();
				var settings = new UnityEditor.SerializedObject(insert);
				settings.FindProperty("_starburst").boolValue = false;
				settings.FindProperty("_ribbed").boolValue = true;
				settings.FindProperty("_ribWidth").floatValue = 3f;
				settings.FindProperty("_ribAngle").floatValue = 45f;
				settings.FindProperty("_ribOffset").floatValue = 100f;
				settings.ApplyModifiedPropertiesWithoutUndo();
				insert.RebuildMesh();
				Assert.That(insert.Error, Is.Null);
				Assert.That(insert.Body.RibWidth, Is.EqualTo(3f));
				Assert.That(insert.Body.StarburstBursts, Is.EqualTo(0));
				Assert.That(insert.Body.RibOffset, Is.EqualTo(insert.Body.StarburstLimit()).Within(1e-5f), "held to the bucket");
				Assert.That(insert.Warnings, Has.Some.Contains("Rib offset reduced"));
				var mesh = go.GetComponent<MeshFilter>().sharedMesh;
				Assert.That(mesh.vertexCount, Is.GreaterThan(0));
				// The starburst wins when both flags are stored.
				settings.FindProperty("_starburst").boolValue = true;
				settings.ApplyModifiedPropertiesWithoutUndo();
				insert.RebuildMesh();
				Assert.That(insert.Body.StarburstBursts, Is.GreaterThan(0));
				Assert.That(insert.Body.RibWidth, Is.EqualTo(0f));
			} finally {
				Object.DestroyImmediate(go.GetComponent<MeshFilter>().sharedMesh);
				Object.DestroyImmediate(go);
			}
		}
	}
}
