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
	/// The mesh stays in its own local space, in metres, as Unity holds it,
	/// which Blender reads as it is. Vertices are welded by exact position,
	/// because the generator only splits them to carry different normals or UVs,
	/// and the bake's rounded edges need the surface connected. What the split
	/// carried is kept per corner instead, in triangle order, which is also the
	/// order everything comes back in.
	/// </remarks>
	[Serializable]
	public class InsertBakeRequest
	{
		public const int CurrentVersion = 3;

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

		/// <summary>
		/// The triangles baked from a flat copy of themselves: the raised middle, if
		/// any. Their chart keeps its projection from above, which no unwrap of a
		/// starburst can match.
		/// </summary>
		public int[] FlatFaces;

		/// <summary>The charts laid out at half the density of the floors.</summary>
		public int[] WallCharts;

		/// <summary>The local height the flat copy is laid at.</summary>
		public float FlatHeight;

		public int TextureSize;
		public int Padding;

		/// <summary>How far every sharp edge is rounded off, in metres.</summary>
		public float EdgeBevel;

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
		public bool HybridOptics;

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
				WallCharts = layout.WallCharts,
				TextureSize = settings.TextureSize,
				Padding = settings.Padding,
				EdgeBevel = settings.EdgeBevel * Physics.ScaleInv,
				RegularExtrusion = SurfaceExtrusion,
				FlatExtrusion = 1.2f * math.abs(starburstOffset) * Physics.ScaleInv + SurfaceExtrusion,
				Unwrap = settings.Unwrap,
				PreferUvPackMaster = settings.PreferUvPackMaster,
				Gpu = settings.Gpu,
				HybridOptics = settings.HybridOptics,
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
			return request;
		}

		/// <summary>
		/// A hundredth of an inch, which clears the float noise of a surface
		/// without reaching across to a neighbouring one.
		/// </summary>
		private const float SurfaceExtrusion = 0.000254f;
	}
}
