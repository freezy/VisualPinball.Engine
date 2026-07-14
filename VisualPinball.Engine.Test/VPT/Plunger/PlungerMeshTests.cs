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
		[Explicit("Known failing characterization for Phase 5: the rod tip is currently open.")]
		public void ShouldCloseModernRodTip()
		{
			var mesh = new PlungerMeshGenerator(new PlungerData()).GetMesh(0.0f, PlungerMeshGenerator.Rod);
			var maxZ = mesh.Vertices.Max(v => v.Z);

			FindBoundaryEdges(mesh)
				.Where(e => System.Math.Abs(mesh.Vertices[e.a].Z - maxZ) < 0.0001f && System.Math.Abs(mesh.Vertices[e.b].Z - maxZ) < 0.0001f)
				.Should().BeEmpty();
		}

		[Test]
		[Explicit("Known failing characterization for Phase 5: zero-loop springs currently overflow while building indices.")]
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
	}
}
