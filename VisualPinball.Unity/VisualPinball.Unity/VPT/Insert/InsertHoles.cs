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
using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>
	/// A round cut-out through the raised floor, down to the channel's floor.
	/// </summary>
	[Serializable]
	public struct InsertHole
	{
		[Tooltip("Where the hole's centre sits relative to the insert's origin, in VPX units.")]
		public Vector2 Center;

		[Min(0f)]
		[Tooltip("Radius of the hole in VPX units.")]
		public float Radius;

		public InsertHole(Vector2 center, float radius)
		{
			Center = center;
			Radius = radius;
		}
	}

	/// <summary>
	/// Where holes go for the usual layouts, as starting points to move them
	/// around from.
	/// </summary>
	/// <remarks>
	/// Holes sit centred on the raised floor's outline, so each bites a half
	/// circle out of its edge, which is how they usually look on real inserts.
	/// </remarks>
	public static class InsertHoles
	{
		/// <summary>One hole at the middle of each corner of the raised floor.</summary>
		public static List<Vector2> AtCorners(InsertOutline outline, float inset)
		{
			var centers = new List<Vector2>(outline.Corners.Count);
			for (var i = 0; i < outline.Corners.Count; i++) {
				centers.Add(CornerMiddle(outline, i, inset));
			}
			return centers;
		}

		/// <summary>
		/// One hole at the middle of each corner and another halfway along each
		/// side between them.
		/// </summary>
		public static List<Vector2> AtCornersAndSides(InsertOutline outline, float inset)
		{
			var ring = Ring(outline, inset, out var distances, out var perimeter);
			var centers = new List<Vector2>(outline.Corners.Count * 2);
			for (var i = 0; i < outline.Corners.Count; i++) {
				var here = CornerDistance(outline, i, distances);
				var there = CornerDistance(outline, (i + 1) % outline.Corners.Count, distances);
				if (there <= here) {
					there += perimeter;
				}
				centers.Add(CornerMiddle(outline, i, inset));
				centers.Add(At(ring, distances, perimeter, (here + there) * 0.5f));
			}
			return centers;
		}

		/// <summary>
		/// <paramref name="count"/> holes spread evenly round the raised floor's
		/// outline, the first at the middle of its topmost corner.
		/// </summary>
		public static List<Vector2> Around(InsertOutline outline, float inset, int count)
		{
			var centers = new List<Vector2>(math.max(0, count));
			if (count <= 0) {
				return centers;
			}

			var ring = Ring(outline, inset, out var distances, out var perimeter);
			var top = 0;
			for (var i = 1; i < outline.Corners.Count; i++) {
				if (CornerMiddle(outline, i, inset).y > CornerMiddle(outline, top, inset).y + InsertOutlineBuilder.Epsilon) {
					top = i;
				}
			}
			var start = CornerDistance(outline, top, distances);
			for (var k = 0; k < count; k++) {
				centers.Add(At(ring, distances, perimeter, start + perimeter * k / count));
			}
			return centers;
		}

		/// <summary>
		/// The holes made symmetric about the insert's middle line, X = 0: any
		/// within <paramref name="tolerance"/> of it move onto it, and any two that
		/// nearly mirror each other across it are set to mirror it exactly, at
		/// their average height and their average distance out.
		/// </summary>
		/// <remarks>
		/// Pairs are matched closest first, so where one hole has two candidates
		/// the better match wins. A hole with no partner stays where it is, and
		/// radii are left alone.
		/// </remarks>
		public static InsertHole[] Symmetrize(IReadOnlyList<InsertHole> holes, float tolerance)
		{
			var result = holes.ToArray();
			var done = new bool[result.Length];
			for (var i = 0; i < result.Length; i++) {
				if (math.abs(result[i].Center.x) <= tolerance) {
					result[i].Center.x = 0f;
					done[i] = true;
				}
			}

			var pairs = new List<(float Error, int Left, int Right)>();
			for (var left = 0; left < result.Length; left++) {
				for (var right = 0; right < result.Length; right++) {
					if (done[left] || done[right] || result[left].Center.x >= 0f || result[right].Center.x <= 0f) {
						continue;
					}
					var mirrored = new Vector2(-result[left].Center.x, result[left].Center.y);
					var error = Vector2.Distance(mirrored, result[right].Center);
					if (error <= tolerance) {
						pairs.Add((error, left, right));
					}
				}
			}
			pairs.Sort((a, b) => a.Error.CompareTo(b.Error));

			foreach (var (_, left, right) in pairs) {
				if (done[left] || done[right]) {
					continue;
				}
				var y = (result[left].Center.y + result[right].Center.y) * 0.5f;
				var x = (result[right].Center.x - result[left].Center.x) * 0.5f;
				result[left].Center = new Vector2(-x, y);
				result[right].Center = new Vector2(x, y);
				done[left] = done[right] = true;
			}
			return result;
		}

		/// <summary>The raised floor's outline point in the middle of a corner.</summary>
		private static Vector2 CornerMiddle(InsertOutline outline, int corner, float inset)
		{
			var span = outline.CornerSpan(corner);
			if (span.Count == 0) {
				// A corner so round it kept no point of its own: the outline point
				// nearest where it was stands in for it.
				var stand = outline.OffsetPoint(Nearest(outline, corner), inset);
				return new Vector2(stand.x, stand.y);
			}
			// An odd span has a point in the middle; an even one straddles it.
			var middle = span.Count / 2;
			var point = span.Count % 2 == 1
				? outline.OffsetPoint(span[middle], inset)
				: (outline.OffsetPoint(span[middle - 1], inset) + outline.OffsetPoint(span[middle], inset)) * 0.5f;
			return new Vector2(point.x, point.y);
		}

		/// <summary>How far round the outline the middle of a corner sits.</summary>
		private static float CornerDistance(InsertOutline outline, int corner, float[] distances)
		{
			var span = outline.CornerSpan(corner);
			if (span.Count == 0) {
				return distances[Nearest(outline, corner)];
			}
			var middle = span.Count / 2;
			return span.Count % 2 == 1
				? distances[span[middle]]
				: (distances[span[middle - 1]] + distances[span[middle]]) * 0.5f;
		}

		/// <summary>The outline point nearest a corner's own position.</summary>
		private static int Nearest(InsertOutline outline, int corner)
		{
			var position = outline.Corners[corner].Position;
			var nearest = 0;
			for (var i = 1; i < outline.Points.Count; i++) {
				if (math.distancesq(outline.Points[i].Position, position)
					< math.distancesq(outline.Points[nearest].Position, position)) {
					nearest = i;
				}
			}
			return nearest;
		}

		private static List<float2> Ring(InsertOutline outline, float inset, out float[] distances, out float perimeter)
		{
			var ring = new List<float2>(outline.Points.Count);
			for (var i = 0; i < outline.Points.Count; i++) {
				ring.Add(outline.OffsetPoint(i, inset));
			}
			distances = new float[ring.Count];
			perimeter = 0f;
			for (var i = 0; i < ring.Count; i++) {
				distances[i] = perimeter;
				perimeter += math.distance(ring[i], ring[(i + 1) % ring.Count]);
			}
			return ring;
		}

		/// <summary>The point <paramref name="distance"/> round a ring, wrapping.</summary>
		private static Vector2 At(List<float2> ring, float[] distances, float perimeter, float distance)
		{
			if (perimeter <= 0f) {
				return new Vector2(ring[0].x, ring[0].y);
			}
			distance = distance % perimeter;
			if (distance < 0f) {
				distance += perimeter;
			}
			for (var i = 0; i < ring.Count; i++) {
				var next = i + 1 < ring.Count ? distances[i + 1] : perimeter;
				if (distance <= next) {
					var length = next - distances[i];
					var t = length > 0f ? (distance - distances[i]) / length : 0f;
					var point = math.lerp(ring[i], ring[(i + 1) % ring.Count], t);
					return new Vector2(point.x, point.y);
				}
			}
			return new Vector2(ring[0].x, ring[0].y);
		}
	}
}
