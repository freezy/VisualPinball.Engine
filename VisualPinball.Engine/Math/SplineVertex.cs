﻿// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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

// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable CommentTypo
// ReSharper disable ConvertIfStatementToSwitchStatement

namespace VisualPinball.Engine.Math
{
	public class SplineVertex
	{
		/// <summary>
		/// number of vertices for the central curve
		/// </summary>
		public int VertexCount; // originally "pcvertex"

		/// <summary>
		/// true if i-th vertex corresponds to a control point
		/// </summary>
		public bool[] Cross; // originally "ppfCross"

		/// <summary>
		/// vertices forming the 2D outline of the ramp
		/// </summary>
		public Vertex2D[] MiddlePoints;

		public Vertex2D[] RgvLocal;

		public SplineVertex(DragPointData[] dragPoints, int thickness, int tableDetailLevel, float accuracy, bool staticRendering = true, float margin = 0f, bool loop = true)
		{
			var vertices = GetCentralCurve(dragPoints, tableDetailLevel, accuracy, staticRendering, loop: loop);
			var numVertices = vertices.Length;

			Cross = new bool[numVertices + 1];
			MiddlePoints = new Vertex2D[numVertices + 1];
			RgvLocal = new Vertex2D[(numVertices + 1) * 2];

			for (var i = 0; i < numVertices; i++)
			{
				// prev and next wrap around in loops
				var prev = vertices[i > 0 ? i - 1 : numVertices - 1];
				var next = vertices[i < numVertices - 1 ? i + 1 : 0];

				// .. but have to be corrected at start and end with "virtual vertices" continuing the spline when not looping, so cuts perpendicular to the tangents
				// maybe fix ramps after that that also hat the same problem.
				if (!loop && i == 0) {
					prev = new RenderVertex2D(vertices[0].X*2-vertices[1].X, vertices[0].Y * 2 - vertices[1].Y);
				}
				if (!loop && i == (numVertices-1))
				{
					next = new RenderVertex2D(vertices[numVertices-1].X * 2 - vertices[numVertices - 2].X, vertices[numVertices - 1].Y * 2 - vertices[numVertices - 2].Y);
				}

				var middle = vertices[i];

				Cross[i] = middle.IsControlPoint;
				Vertex2D normal;

				// Get normal at this point
				// Notice that these values equal the ones in the line
				// equation and could probably be substituted by them.
				var normal1 = new Vertex2D(prev.Y - middle.Y, middle.X - prev.X); // vector vmiddle-vprev rotated RIGHT
				var normal2 = new Vertex2D(middle.Y - next.Y, next.X - middle.X); // vector vnext-vmiddle rotated RIGHT

				// not needed special start/end handling as rubbers always loop, except for the case where there are only 2 control points 
				// I guess this does not work as intended, but could not figure out what was wrong. i think that somehow the normal of Node 1 is wrong. /cupiii
				if (numVertices == 2 && i == numVertices - 1) { 
					normal1.Normalize();
					normal = normal1;

				} else if (numVertices == 2 && i == 0) { 
					normal2.Normalize();
					normal = normal2;

				} else {
					normal1.Normalize();
					normal2.Normalize();

					if (MathF.Abs(normal1.X - normal2.X) < 0.0001 && MathF.Abs(normal1.Y - normal2.Y) < 0.0001) {
						// Two parallel segments
						normal = normal1;
					}
					else {
						// Find intersection of the two edges meeting this points, but
						// shift those lines outwards along their normals

						// First line
						var a = prev.Y - middle.Y;
						var b = middle.X - prev.X;

						// Shift line along the normal
						var c = a * (normal1.X - prev.X) + b * (normal1.Y - prev.Y);

						// Second line
						var d = next.Y - middle.Y;
						var e = middle.X - next.X;

						// Shift line along the normal
						var f = d * (normal2.X - next.X) + e * (normal2.Y - next.Y);

						var det = a * e - b * d;
						var invDet = det != 0.0f ? 1.0f / det : 0.0f;

						var intersectX = (b * f - e * c) * invDet;
						var intersectY = (c * d - a * f) * invDet;

						normal = new Vertex2D(middle.X - intersectX, middle.Y - intersectY);
					}
				}

				var widthCur = thickness + margin;

				MiddlePoints[i] = new Vertex2D(middle.X, middle.Y);

				RgvLocal[i] = new Vertex2D(middle.X, middle.Y) + widthCur * 0.5f * normal;
				RgvLocal[(numVertices + 1) * 2 - i - 1] = new Vertex2D(middle.X, middle.Y) - widthCur * 0.5f * normal;

				if (i == 0) {
					RgvLocal[numVertices] = RgvLocal[0];
					RgvLocal[(numVertices + 1) * 2 - numVertices - 1] = RgvLocal[(numVertices + 1) * 2 - 1];
				}
			}

			if (loop)
				VertexCount = numVertices;
			else
			{
				MiddlePoints[numVertices] = MiddlePoints[0];
				Cross[numVertices] = vertices[0].IsControlPoint;
				VertexCount = numVertices + 1;
			}
		}

		private static RenderVertex2D[] GetCentralCurve(DragPointData[] dragPoints, int tableDetailLevel, float acc, bool staticRendering = true, bool loop = true)
		{
			float accuracy;

			// as solid rubbers are rendered into the static buffer, always use maximum precision
			if (acc != -1.0f) {
				accuracy = acc; // used for hit shape calculation, always!

			} else {
				accuracy = staticRendering ? 10.0f : tableDetailLevel;

				// min = 4 (highest accuracy/detail level), max = 4 * 10^(10/1.5) = ~18.000.000 (lowest accuracy/detail level)
				accuracy = 4.0f * MathF.Pow(10.0f, (10.0f - accuracy) * (float) (1.0 / 1.5));
			}

			return DragPoint.GetRgVertex<RenderVertex2D, CatmullCurve2DCatmullCurveFactory>(dragPoints, loop, accuracy);
		}
	}
}
