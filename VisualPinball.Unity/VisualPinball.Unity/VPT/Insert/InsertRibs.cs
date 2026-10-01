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
	/// Parallel ribs across the raised floor of an insert: a zigzag of flat
	/// strips, ridge and groove by turns, all running the one way.
	/// </summary>
	/// <remarks>
	/// The creases are lines a half pitch apart, across the ribs' direction,
	/// one of them through the insert's origin; those an even number of half
	/// pitches from it are ridges, the others grooves, so a rib is a ridge with
	/// a groove either side, a pitch wide from ridge to ridge. The surface is
	/// flat within each strip, which is what makes it possible to cut the
	/// insert's outline out of it exactly, as with the starburst's triangles.
	/// </remarks>
	public class InsertRibs : InsertRelief
	{
		/// <summary>A rib narrower than this is noise.</summary>
		public const float MinWidth = 0.2f;

		/// <summary>Unit direction the ribs run along.</summary>
		public readonly float2 Along;

		/// <summary>Unit direction across the ribs, a quarter turn on from along.</summary>
		public readonly float2 Across;

		/// <summary>Ridge to ridge, in VPX units.</summary>
		public readonly float Width;

		/// <summary>How far the ribs reach beyond the surface they cover, so every crease runs clear across it.</summary>
		public readonly float Reach;

		private readonly float _centerDepth;
		private readonly float _offset;
		private readonly int _first;
		private readonly int _last;

		public override float CenterDepth => _centerDepth;
		public override float Offset => _offset;
		public override float2 Center => float2.zero;

		/// <summary>The crease index of the first ridge or groove on the surface, and the last.</summary>
		public int FirstCrease => _first;
		public int LastCrease => _last;

		private InsertRibs(float angle, float width, float offset, float centerDepth, float reach, int first, int last)
		{
			var radians = math.radians(angle);
			Along = new float2(math.cos(radians), math.sin(radians));
			Across = new float2(-Along.y, Along.x);
			Width = width;
			_offset = offset;
			_centerDepth = centerDepth;
			Reach = reach;
			_first = first;
			_last = last;
		}

		/// <summary>
		/// Ribs running at <paramref name="angle"/> degrees from the X axis,
		/// <paramref name="width"/> apart from ridge to ridge, laid across
		/// every point of <paramref name="footprint"/>.
		/// </summary>
		public static InsertRibs Cover(IReadOnlyList<float2> footprint, float angle, float width, float offset, float centerDepth)
		{
			if (width < MinWidth) {
				throw new InsertOutlineException($"Ribs need a width of at least {MinWidth}.");
			}
			if (footprint.Count == 0) {
				throw new InsertOutlineException("Ribs need a surface to cover.");
			}
			var radians = math.radians(angle);
			var along = new float2(math.cos(radians), math.sin(radians));
			var across = new float2(-along.y, along.x);
			var low = float.MaxValue;
			var high = float.MinValue;
			var reach = 0f;
			foreach (var point in footprint) {
				var lateral = math.dot(point, across);
				low = math.min(low, lateral);
				high = math.max(high, lateral);
				reach = math.max(reach, math.abs(math.dot(point, along)));
			}
			if (high - low < InsertOutlineBuilder.Epsilon && reach < InsertOutlineBuilder.Epsilon) {
				throw new InsertOutlineException("The surface the ribs should cover has no size.");
			}
			var half = width * 0.5f;
			// The creases either side of the surface as well, so the strips at
			// its edges have both their creases and a point on the edge is never
			// a borderline case for the strip lookup.
			var first = (int)math.floor(low / half) - 1;
			var last = (int)math.ceil(high / half) + 1;
			return new InsertRibs(angle, width, offset, centerDepth, reach * 1.01f + width, first, last);
		}

		/// <summary>The depth of crease <paramref name="index"/>: ridges at even indices, grooves at odd.</summary>
		public float CreaseDepth(int index) => _centerDepth + ((index & 1) == 0 ? _offset : -_offset);

		/// <summary>How far crease <paramref name="index"/> lies from the origin, across the ribs.</summary>
		public float CreaseLateral(int index) => index * Width * 0.5f;

		/// <summary>
		/// Which strip <paramref name="position"/> falls in: the index of the
		/// crease on its lower side.
		/// </summary>
		public override int SectorOf(float2 position)
			=> math.clamp((int)math.floor(math.dot(position, Across) / (Width * 0.5f)), _first, _last - 1);

		public override float DepthAt(float2 position) => DepthInSector(SectorOf(position), position);

		/// <summary>
		/// The depth of strip <paramref name="sector"/>'s plane at
		/// <paramref name="position"/>: linear across the strip between its two
		/// creases, exact anywhere on that plane, so two strips sharing a crease
		/// agree along it.
		/// </summary>
		public float DepthInSector(int sector, float2 position)
		{
			var lateral = math.dot(position, Across);
			var t = (lateral - CreaseLateral(sector)) / (Width * 0.5f);
			return math.lerp(CreaseDepth(sector), CreaseDepth(sector + 1), t);
		}

		public override float3 LocalNormal(int sector)
		{
			float3 a = InsertMeshGenerator.ToLocal(Across * CreaseLateral(sector), CreaseDepth(sector));
			float3 b = InsertMeshGenerator.ToLocal(Across * CreaseLateral(sector) + Along, CreaseDepth(sector));
			float3 c = InsertMeshGenerator.ToLocal(Across * CreaseLateral(sector + 1), CreaseDepth(sector + 1));
			var tilted = math.normalize(math.cross(b - a, c - a));
			return tilted.y < 0f ? -tilted : tilted;
		}

		/// <summary>
		/// Every crease's parts inside the polygon: a crease is a line clear
		/// across the surface, so its crossings pair up from the first to the
		/// last, and a crease through a notch comes back in pieces.
		/// </summary>
		public override IEnumerable<(float2 From, float2 To, bool FromCenter)> CreasesWithin(IReadOnlyList<float2> polygon, float tolerance)
		{
			for (var index = _first; index <= _last; index++) {
				var origin = Across * CreaseLateral(index) - Along * Reach;
				var crossings = Crossings(origin, Along, polygon);
				for (var i = 0; i + 1 < crossings.Count; i += 2) {
					var near = math.max(crossings[i], 0f);
					var far = math.min(crossings[i + 1], Reach * 2f);
					if (far - near > tolerance) {
						yield return (origin + Along * near, origin + Along * far, false);
					}
				}
			}
		}

		/// <summary>Along the ribs: a line that way crosses no crease.</summary>
		public override float2 BridgeDirection(float2 from) => Along;

		public override Vector4 Optical()
		{
			var origin = InsertMeshGenerator.ToLocal(float2.zero, _centerDepth - math.abs(_offset));
			var across = InsertMeshGenerator.ToLocal(Across * Width * 0.5f, _centerDepth - math.abs(_offset)) - origin;
			var halfPitch = new Vector2(across.x, across.z).magnitude;
			var direction = new Vector2(across.x, across.z) / math.max(halfPitch, 1e-9f);
			return new Vector4(direction.x, direction.y, -halfPitch, origin.y);
		}
	}
}
