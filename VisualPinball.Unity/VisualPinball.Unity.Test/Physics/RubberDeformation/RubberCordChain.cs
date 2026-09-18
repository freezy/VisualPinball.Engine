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
using Unity.Mathematics;

namespace VisualPinball.Unity.Test
{
	using Math = System.Math;

	/// <summary>
	/// The test-side twin of <c>CordChainPoint</c> (section 3.2), in double precision.
	/// </summary>
	internal readonly struct RubberCordChainPoint
	{
		internal readonly double3 RestPosition;

		/// <summary>Unit inward deflection direction.</summary>
		internal readonly double3 Direction;

		/// <summary>Arc-length fraction along the span.</summary>
		internal readonly double U;

		/// <summary>sin(n * pi * U) for n = 1..4.</summary>
		internal readonly double4 Shape;

		internal RubberCordChainPoint(double3 restPosition, double3 direction, double u)
		{
			RestPosition = restPosition;
			Direction = direction;
			U = u;
			Shape = new double4(
				math.sin(Math.PI * u),
				math.sin(2.0 * Math.PI * u),
				math.sin(3.0 * Math.PI * u),
				math.sin(4.0 * Math.PI * u));
		}
	}

	/// <summary>
	/// A contact witness on the deformed chain: which segment, where along it, the outward
	/// normal (cord surface towards the ball centre), the signed separation to the cord
	/// surface, and the per-lane basis vector of section 2.4.2.
	/// </summary>
	internal readonly struct RubberCordWitness
	{
		internal readonly int Segment;
		internal readonly double Alpha;
		internal readonly double3 Normal;
		internal readonly double Separation;
		internal readonly double4 Basis0;   // basis vector components along Normal, per lane
		internal readonly double3 BasisLane0;

		internal RubberCordWitness(int segment, double alpha, double3 normal, double separation,
			double4 participation, double3 basisLane0)
		{
			Segment = segment;
			Alpha = alpha;
			Normal = normal;
			Separation = separation;
			Basis0 = participation;
			BasisLane0 = basisLane0;
		}

		/// <summary>Lane participation g_n = dot(b_n, Normal).</summary>
		internal double4 Participation => Basis0;
	}

	/// <summary>
	/// Section 3.2's chain: a polyline whose points move affinely with the modal state,
	/// <c>P_k(t) = Rest_k + Direction_k * dot(Shape_k, Q(t))</c>.
	/// </summary>
	internal sealed class RubberCordChain
	{
		internal readonly RubberCordChainPoint[] Points;
		internal readonly double CordRadius;

		internal int SegmentCount => Points.Length - 1;

		internal RubberCordChain(RubberCordChainPoint[] points, double cordRadius)
		{
			Points = points;
			CordRadius = cordRadius;
		}

		/// <summary>A straight span from a to b with a constant inward direction.</summary>
		internal static RubberCordChain Straight(double3 a, double3 b, double3 inward, int segments, double cordRadius)
		{
			var dir = math.normalize(inward);
			var points = new RubberCordChainPoint[segments + 1];
			for (var k = 0; k <= segments; k++) {
				var u = (double)k / segments;
				points[k] = new RubberCordChainPoint(a + (b - a) * u, dir, u);
			}
			return new RubberCordChain(points, cordRadius);
		}

		internal double3 PointAt(int k, in double4 q)
			=> Points[k].RestPosition + Points[k].Direction * math.dot(Points[k].Shape, q);

		internal double3 VelocityAt(int k, in double4 qDot)
			=> Points[k].Direction * math.dot(Points[k].Shape, qDot);

		/// <summary>
		/// Minimum sphere-to-segment separation against the deformed chain, with the
		/// interpolated basis of section 2.4.2 at the witness.
		/// </summary>
		internal RubberCordWitness Closest(double3 ballCentre, double ballRadius, in double4 q)
		{
			var bestDistSq = double.MaxValue;
			var bestSegment = 0;
			var bestAlpha = 0.0;
			var bestPoint = default(double3);

			for (var k = 0; k < SegmentCount; k++) {
				var p0 = PointAt(k, in q);
				var p1 = PointAt(k + 1, in q);
				var d = p1 - p0;
				var lenSq = math.lengthsq(d);
				var alpha = lenSq > 1e-18 ? math.clamp(math.dot(ballCentre - p0, d) / lenSq, 0.0, 1.0) : 0.0;
				var witness = p0 + d * alpha;
				var distSq = math.lengthsq(ballCentre - witness);
				if (distSq < bestDistSq) {
					bestDistSq = distSq;
					bestSegment = k;
					bestAlpha = alpha;
					bestPoint = witness;
				}
			}

			var dist = math.sqrt(bestDistSq);
			var normal = dist > 1e-12 ? (ballCentre - bestPoint) / dist : new double3(0, 1, 0);
			var separation = dist - (ballRadius + CordRadius);

			var i = Points[bestSegment];
			var j = Points[bestSegment + 1];
			var participation = new double4(
				math.dot((1.0 - bestAlpha) * i.Direction * i.Shape.x + bestAlpha * j.Direction * j.Shape.x, normal),
				math.dot((1.0 - bestAlpha) * i.Direction * i.Shape.y + bestAlpha * j.Direction * j.Shape.y, normal),
				math.dot((1.0 - bestAlpha) * i.Direction * i.Shape.z + bestAlpha * j.Direction * j.Shape.z, normal),
				math.dot((1.0 - bestAlpha) * i.Direction * i.Shape.w + bestAlpha * j.Direction * j.Shape.w, normal));
			var basisLane0 = (1.0 - bestAlpha) * i.Direction * i.Shape.x + bestAlpha * j.Direction * j.Shape.x;

			return new RubberCordWitness(bestSegment, bestAlpha, normal, separation, participation, basisLane0);
		}

		/// <summary>Cord surface velocity at a witness, <c>sum(qdot_n * b_n)</c>.</summary>
		internal double3 SurfaceVelocity(in RubberCordWitness witness, in double4 qDot)
		{
			var i = Points[witness.Segment];
			var j = Points[witness.Segment + 1];
			var a = witness.Alpha;
			var v = default(double3);
			v += qDot.x * ((1.0 - a) * i.Direction * i.Shape.x + a * j.Direction * j.Shape.x);
			v += qDot.y * ((1.0 - a) * i.Direction * i.Shape.y + a * j.Direction * j.Shape.y);
			v += qDot.z * ((1.0 - a) * i.Direction * i.Shape.z + a * j.Direction * j.Shape.z);
			v += qDot.w * ((1.0 - a) * i.Direction * i.Shape.w + a * j.Direction * j.Shape.w);
			return v;
		}

		/// <summary>Arc-length fraction at a witness.</summary>
		internal double FractionAt(in RubberCordWitness witness)
			=> math.lerp(Points[witness.Segment].U, Points[witness.Segment + 1].U, witness.Alpha);
	}
}
