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
using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// Everything the Blender script bakes an insert from, written out as JSON.
	/// </summary>
	/// <remarks>
	/// The mesh stays in its own local space, in metres, as Unity holds it; the
	/// script converts to Blender's axes and back. Vertices are welded by exact
	/// position, because the generator only splits them to carry different
	/// normals or UVs, and the high-poly bevel needs the surface connected. What
	/// the split carried is kept per corner instead, in triangle order, which is
	/// also the order everything comes back in.
	/// </remarks>
	[Serializable]
	public class InsertBakeRequest
	{
		public const int CurrentVersion = 1;

		/// <summary>
		/// How far apart two corner normals may point, as one minus their dot
		/// product, and still be the same smooth surface.
		/// </summary>
		private const float SmoothTolerance = 1e-5f;

		public int Version = CurrentVersion;

		/// <summary>Welded positions, three floats each.</summary>
		public float[] Positions;

		/// <summary>Three welded position indices per triangle.</summary>
		public int[] Triangles;

		/// <summary>Three floats per corner.</summary>
		public float[] Normals;

		/// <summary>Two floats per corner, the generator's own atlas.</summary>
		public float[] Uvs;

		/// <summary>The UV chart of each triangle. Seams run between charts.</summary>
		public int[] Charts;

		/// <summary>The triangles baked from a flat copy of themselves.</summary>
		public int[] FlatFaces;

		/// <summary>The local height the flat copy is laid at.</summary>
		public float FlatHeight;

		/// <summary>
		/// Welded position pairs, one per edge where the surface creases, so the
		/// high-poly rounds those off and nothing else.
		/// </summary>
		public int[] Creases;

		public int TextureSize;
		public int Padding;

		/// <summary>In metres.</summary>
		public float EdgeBevel;
		public int EdgeBevelSegments;

		/// <summary>
		/// How far above the flat copy its rays start, in metres, which must clear
		/// the starburst's highest point.
		/// </summary>
		public float FlatExtrusion;

		/// <summary>How far outside the mesh its own rays start, in metres.</summary>
		public float RegularExtrusion;

		public bool Unwrap;
		public bool PreferUvPackMaster;
		public bool Gpu;

		public static InsertBakeRequest Build(Mesh mesh, InsertMeshLayout layout, InsertBakeSettings settings, float starburstOffset)
		{
			var vertices = mesh.vertices;
			var normals = mesh.normals;
			var uvs = mesh.uv;
			var triangles = mesh.triangles;

			var weld = new int[vertices.Length];
			var welded = new Dictionary<Vector3, int>(vertices.Length);
			var positions = new List<float>(vertices.Length * 3);
			for (var i = 0; i < vertices.Length; i++) {
				if (!welded.TryGetValue(vertices[i], out var index)) {
					index = welded.Count;
					welded.Add(vertices[i], index);
					positions.Add(vertices[i].x);
					positions.Add(vertices[i].y);
					positions.Add(vertices[i].z);
				}
				weld[i] = index;
			}

			var request = new InsertBakeRequest {
				Positions = positions.ToArray(),
				Triangles = new int[triangles.Length],
				Normals = new float[triangles.Length * 3],
				Uvs = new float[triangles.Length * 2],
				Charts = new int[triangles.Length / 3],
				FlatHeight = layout.FlatHeight,
				TextureSize = settings.TextureSize,
				Padding = settings.Padding,
				EdgeBevel = settings.EdgeBevel * Physics.ScaleInv,
				EdgeBevelSegments = settings.EdgeBevelSegments,
				RegularExtrusion = SurfaceExtrusion,
				FlatExtrusion = 1.2f * math.abs(starburstOffset) * Physics.ScaleInv + SurfaceExtrusion,
				Unwrap = settings.Unwrap,
				PreferUvPackMaster = settings.PreferUvPackMaster,
				Gpu = settings.Gpu,
			};

			var flat = new List<int>();
			for (var corner = 0; corner < triangles.Length; corner++) {
				var vertex = triangles[corner];
				request.Triangles[corner] = weld[vertex];
				request.Normals[corner * 3] = normals[vertex].x;
				request.Normals[corner * 3 + 1] = normals[vertex].y;
				request.Normals[corner * 3 + 2] = normals[vertex].z;
				request.Uvs[corner * 2] = uvs[vertex].x;
				request.Uvs[corner * 2 + 1] = uvs[vertex].y;
			}
			for (var triangle = 0; triangle < request.Charts.Length; triangle++) {
				var chart = layout.VertexCharts[triangles[triangle * 3]];
				request.Charts[triangle] = chart;
				if (chart == layout.FlatChart) {
					flat.Add(triangle);
				}
			}
			request.FlatFaces = flat.ToArray();
			request.Creases = FindCreases(request.Triangles, request.Normals);
			return request;
		}

		/// <summary>
		/// Every edge shared by exactly two triangles whose normals differ at
		/// either end. The rim, which borders one triangle, stays open.
		/// </summary>
		private static int[] FindCreases(int[] triangles, float[] normals)
		{
			var first = new Dictionary<(int, int), (int From, int To)>();
			var shared = new HashSet<(int, int)>();
			var creases = new List<int>();
			for (var corner = 0; corner < triangles.Length; corner++) {
				var next = corner - corner % 3 + (corner + 1) % 3;
				var a = triangles[corner];
				var b = triangles[next];
				var key = a < b ? (a, b) : (b, a);
				var corners = a < b ? (From: corner, To: next) : (From: next, To: corner);
				if (!first.TryGetValue(key, out var other)) {
					first.Add(key, corners);
					continue;
				}
				if (!shared.Add(key)) {
					continue;
				}
				if (!Smooth(normals, other.From, corners.From) || !Smooth(normals, other.To, corners.To)) {
					creases.Add(key.Item1);
					creases.Add(key.Item2);
				}
			}
			return creases.ToArray();
		}

		private static bool Smooth(float[] normals, int a, int b)
		{
			var dot = normals[a * 3] * normals[b * 3] + normals[a * 3 + 1] * normals[b * 3 + 1] + normals[a * 3 + 2] * normals[b * 3 + 2];
			return dot >= 1f - SmoothTolerance;
		}

		/// <summary>
		/// A hundredth of an inch, which clears the float noise of a surface
		/// without reaching across to a neighbouring one.
		/// </summary>
		private const float SurfaceExtrusion = 0.000254f;
	}
}
