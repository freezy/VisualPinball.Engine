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
using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Packs UV charts into the unit square without overlap.
	/// </summary>
	/// <remarks>
	/// Every chart comes in laid out in VPX units, so one scale for all of them
	/// keeps the texel density the same everywhere, which is what a baked map
	/// wants. Charts are placed on shelves, tallest first, and the square grows
	/// until they all fit, with a gap round each so baking does not bleed from
	/// one into its neighbour.
	/// </remarks>
	public static class InsertUvAtlas
	{
		/// <summary>The gap round each chart, as a share of the square's side.</summary>
		public const float Padding = 0.004f;

		/// <summary>
		/// Moves every chart into the unit square, in place.
		/// </summary>
		/// <param name="uvs">Each vertex's position within its chart, in VPX units.</param>
		/// <param name="charts">Each vertex's chart.</param>
		/// <returns>How many VPX units the square's side spans.</returns>
		public static float Pack(List<Vector2> uvs, List<int> charts)
		{
			var bounds = new Dictionary<int, (float2 Min, float2 Max)>();
			for (var i = 0; i < uvs.Count; i++) {
				float2 uv = uvs[i];
				bounds[charts[i]] = bounds.TryGetValue(charts[i], out var box)
					? (math.min(box.Min, uv), math.max(box.Max, uv))
					: (uv, uv);
			}
			if (bounds.Count == 0) {
				return 1f;
			}

			var items = new List<(int Chart, float2 Size)>(bounds.Count);
			var area = 0f;
			var widest = 0f;
			foreach (var (chart, box) in bounds) {
				var size = box.Max - box.Min;
				items.Add((chart, size));
				area += (size.x + 1e-3f) * (size.y + 1e-3f);
				widest = math.max(widest, math.cmax(size));
			}
			items.Sort((a, b) => b.Size.y != a.Size.y ? b.Size.y.CompareTo(a.Size.y) : a.Chart.CompareTo(b.Chart));

			var side = math.max(widest * (1f + 4f * Padding), math.sqrt(area));
			var placed = new Dictionary<int, float2>(items.Count);
			for (var attempt = 0; attempt < 200; attempt++) {
				if (TryShelve(items, side, placed)) {
					break;
				}
				side *= 1.03f;
			}

			for (var i = 0; i < uvs.Count; i++) {
				var chart = charts[i];
				var uv = ((float2)uvs[i] - bounds[chart].Min + placed[chart]) / side;
				uvs[i] = new Vector2(uv.x, uv.y);
			}
			return side;
		}

		private static bool TryShelve(List<(int Chart, float2 Size)> items, float side, Dictionary<int, float2> placed)
		{
			placed.Clear();
			var gap = side * Padding;
			var x = gap;
			var y = gap;
			var shelf = 0f;
			foreach (var (chart, size) in items) {
				if (x + size.x + gap > side) {
					x = gap;
					y += shelf + gap;
					shelf = 0f;
				}
				if (x + size.x + gap > side || y + size.y + gap > side) {
					return false;
				}
				placed[chart] = new float2(x, y);
				x += size.x + gap;
				shelf = math.max(shelf, size.y);
			}
			return true;
		}
	}
}
