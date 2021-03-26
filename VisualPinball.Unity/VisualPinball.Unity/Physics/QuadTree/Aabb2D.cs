using System;
using Unity.Mathematics;

namespace VisualPinball.Unity
{
	[Serializable]
	public struct Aabb2D {
		public float2 Center;
		public float2 Extents;

		public float2 Size => Extents * 2;
		public float2 Min => Center - Extents;
		public float2 Max => Center + Extents;

		public Aabb2D(float2 center, float2 extents)
		{
			Center = center;
			Extents = extents;
		}

		public bool Contains(float2 point) {
			if (point[0] < Center[0] - Extents[0]) {
				return false;
			}
			if (point[0] > Center[0] + Extents[0]) {
				return false;
			}

			if (point[1] < Center[1] - Extents[1]) {
				return false;
			}
			if (point[1] > Center[1] + Extents[1]) {
				return false;
			}

			return true;
		}

		public bool Contains(Aabb2D b) {
			return Contains(b.Center + new float2(-b.Extents.x, -b.Extents.y)) &&
			       Contains(b.Center + new float2(-b.Extents.x, b.Extents.y)) &&
			       Contains(b.Center + new float2(b.Extents.x, -b.Extents.y)) &&
			       Contains(b.Center + new float2(b.Extents.x, b.Extents.y));
		}

		public bool Intersects(Aabb2D b)
		{
			//bool noOverlap = Min[0] > b.Max[0] ||
			//                 b.Min[0] > Max[0]||
			//                 Min[1] > b.Max[1] ||
			//                 b.Min[1] > Max[1];
//
			//return !noOverlap;

			return (math.abs(Center[0] - b.Center[0]) < (Extents[0] + b.Extents[0])) &&
			       (math.abs(Center[1] - b.Center[1]) < (Extents[1] + b.Extents[1]));
		}
	}
}
