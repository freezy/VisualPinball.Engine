// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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

		public SplineVertex(DragPointData[] dragPoints, int thickness, int tableDetailLevel,
			int accuracy, bool staticRendering = true)
		{
			var vertices = GetCentralCurve(dragPoints, tableDetailLevel, accuracy, staticRendering);
			var numVertices = vertices.Length;

			Cross = new bool[numVertices + 1];
			MiddlePoints = new Vertex2D[numVertices + 1];
			RgvLocal = new Vertex2D[(numVertices + 1) * 2];

			for (var i = 0; i < numVertices; i++) {
				// prev and next wrap around as rubbers always loop
				var prev = vertices[i > 0 ? i - 1 : numVertices - 1];
				var next = vertices[i < numVertices - 1 ? i + 1 : 0];
				var middle = vertices[i];

				Cross[i] = middle.IsControlPoint;
				Vertex2D normal;

				// Get normal at this point
				// Notice that these values equal the ones in the line
				// equation and could probably be substituted by them.
				var normal1 = new Vertex2D(prev.Y - middle.Y, middle.X - prev.X); // vector vmiddle-vprev rotated RIGHT
				var normal2 = new Vertex2D(middle.Y - next.Y, next.X - middle.X); // vector vnext-vmiddle rotated RIGHT

				// not needed special start/end handling as rubbers always loop, except for the case where there are only 2 control points
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

				var widthCur = thickness;

				MiddlePoints[i] = middle;

				// vmiddle + (widthcur * 0.5) * vnormal;
				RgvLocal[i] = middle.Clone().Add(normal.Clone().MultiplyScalar(widthCur * 0.5f));

				// vmiddle - (widthcur*0.5f) * vnormal;
				RgvLocal[(numVertices + 1) * 2 - i - 1] =
					middle.Clone().Sub(normal.Clone().MultiplyScalar(widthCur * 0.5f));

				if (i == 0) {
					RgvLocal[numVertices] = RgvLocal[0];
					RgvLocal[(numVertices + 1) * 2 - numVertices - 1] = RgvLocal[(numVertices + 1) * 2 - 1];
				}
			}

			Cross[numVertices] = vertices[0].IsControlPoint;
			MiddlePoints[numVertices] = MiddlePoints[0];
			VertexCount = numVertices + 1;
		}

		private static RenderVertex2D[] GetCentralCurve(DragPointData[] dragPoints, int tableDetailLevel, int acc,
			bool staticRendering = true)
		{
			float accuracy;

			// as solid rubbers are rendered into the static buffer, always use maximum precision
			if (acc != -1.0) {
				accuracy = acc; // used for hit shape calculation, always!

			} else {
				accuracy = staticRendering ? 10 : tableDetailLevel;

				// min = 4 (highest accuracy/detail level), max = 4 * 10^(10/1.5) = ~18.000.000 (lowest accuracy/detail level)
				accuracy = 4.0f * MathF.Pow(10.0f, (10.0f - accuracy) * (float) (1.0 / 1.5));
			}

			return DragPoint.GetRgVertex<RenderVertex2D, CatmullCurve2DCatmullCurveFactory>(dragPoints, true, accuracy);
		}
	}
}
