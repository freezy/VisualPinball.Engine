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
using FluentAssertions;
using NUnit.Framework;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Plunger;

namespace VisualPinball.Engine.Test.VPT.Plunger
{
	public class PlungerMeshTests
	{
		[Test]
		public void ShouldCloseModernRodTip()
		{
			var mesh = new PlungerMeshGenerator(new PlungerData()).GetMesh(0.0f, PlungerMeshGenerator.Rod);
			var maxZ = mesh.Vertices.Max(v => v.Z);

			FindBoundaryEdges(mesh)
				.Where(e => System.Math.Abs(mesh.Vertices[e.a].Z - maxZ) < 0.0001f && System.Math.Abs(mesh.Vertices[e.b].Z - maxZ) < 0.0001f)
				.Should().BeEmpty();
		}

		[Test]
		public void ShouldKeepCustomRodTipCapCoplanarWithOffsetFirstTipPoint()
		{
			var data = new PlungerData {
				Type = PlungerType.PlungerTypeCustom,
				TipShape = "5 .34; 10 .5"
			};

			var mesh = new PlungerMeshGenerator(data).GetMesh(0.0f, PlungerMeshGenerator.Rod);
			var maxZ = mesh.Vertices.Max(v => v.Z);

			mesh.Vertices.Count(v => System.Math.Abs(v.Z - maxZ) < 0.0001f).Should().BeGreaterThan(1);
		}

		[Test]
		public void ShouldBuildEmptySpringForZeroLoops()
		{
			var data = new PlungerData {
				Type = PlungerType.PlungerTypeCustom,
				SpringLoops = 0.0f,
				SpringEndLoops = 0.0f
			};

			var mesh = new PlungerMeshGenerator(data).GetMesh(0.0f, PlungerMeshGenerator.Spring);

			mesh.Vertices.Should().BeEmpty();
			mesh.Indices.Should().BeEmpty();
		}

		[Test]
		public void ShouldBuildEmptySpringForNegativeLoops()
		{
			var data = new PlungerData {
				Type = PlungerType.PlungerTypeCustom,
				SpringLoops = -1.0f,
				SpringEndLoops = 0.0f
			};

			var mesh = new PlungerMeshGenerator(data).GetMesh(0.0f, PlungerMeshGenerator.Spring);

			mesh.Vertices.Should().BeEmpty();
			mesh.Indices.Should().BeEmpty();
		}

		[TestCase(PlungerMeshGenerator.Rod)]
		[TestCase(PlungerMeshGenerator.Spring)]
		[TestCase(PlungerMeshGenerator.Flat)]
		public void ShouldBuildLocalMeshIndependentOfZAdjust(string meshId)
		{
			var baseMesh = new PlungerMeshGenerator(CreateData(meshId, 0.0f)).GetLocalMesh(meshId);
			var elevatedMesh = new PlungerMeshGenerator(CreateData(meshId, 17.0f)).GetLocalMesh(meshId);

			elevatedMesh.Vertices.Should().HaveCount(baseMesh.Vertices.Length);
			for (var i = 0; i < baseMesh.Vertices.Length; i++) {
				elevatedMesh.Vertices[i].X.Should().BeApproximately(baseMesh.Vertices[i].X, 0.0001f);
				elevatedMesh.Vertices[i].Y.Should().BeApproximately(baseMesh.Vertices[i].Y, 0.0001f);
				elevatedMesh.Vertices[i].Z.Should().BeApproximately(baseMesh.Vertices[i].Z, 0.0001f);
			}
		}

		private static IEnumerable<(int a, int b)> FindBoundaryEdges(Mesh mesh)
		{
			var edgeCounts = new Dictionary<(int a, int b), int>();
			for (var i = 0; i < mesh.Indices.Length; i += 3) {
				AddEdge(mesh.Indices[i], mesh.Indices[i + 1], edgeCounts);
				AddEdge(mesh.Indices[i + 1], mesh.Indices[i + 2], edgeCounts);
				AddEdge(mesh.Indices[i + 2], mesh.Indices[i], edgeCounts);
			}
			return edgeCounts.Where(e => e.Value == 1).Select(e => e.Key);
		}

		private static void AddEdge(int a, int b, IDictionary<(int a, int b), int> edgeCounts)
		{
			var edge = a < b ? (a, b) : (b, a);
			edgeCounts.TryGetValue(edge, out var count);
			edgeCounts[edge] = count + 1;
		}

		private static PlungerData CreateData(string meshId, float zAdjust)
		{
			return new PlungerData {
				Type = meshId switch {
					PlungerMeshGenerator.Flat => PlungerType.PlungerTypeFlat,
					PlungerMeshGenerator.Spring => PlungerType.PlungerTypeCustom,
					_ => PlungerType.PlungerTypeModern
				},
				ZAdjust = zAdjust
			};
		}
	}
}
