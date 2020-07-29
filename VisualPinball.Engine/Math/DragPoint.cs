using System;
using System.Collections.Generic;

namespace VisualPinball.Engine.Math
{
	static public class DragPoint
	{
		public static TVertex[] GetRgVertex<TVertex, TCatmullCurveFactory>(DragPointData[] dragPoints, bool loop = true, float accuracy = 4.0f) where TVertex : IRenderVertex, new() where TCatmullCurveFactory : ICatmullCurveFactory<TVertex>, new()
		// 4 = maximum precision that we allow for
		{
			var vertices = new List<TVertex>();
			var numPoints = dragPoints.Length;
			var lastPoint = loop ? numPoints : numPoints - 1;

			var vertex2 = new TVertex();

			for (var i = 0; i < lastPoint; i++) {
				var pdp1 = dragPoints[i];
				var pdp2 = dragPoints[i < numPoints - 1 ? i + 1 : 0];

				if (pdp1.Center.X == pdp2.Center.X && pdp1.Center.Y == pdp2.Center.Y && pdp1.Center.Z == pdp2.Center.Z) {
					continue; // Special case - two points coincide
				}

				var prev = pdp1.IsSmooth ? i - 1 : i;
				if (prev < 0) {
					prev = loop ? numPoints - 1 : 0;
				}

				var next = pdp2.IsSmooth ? i + 2 : i + 1;
				if (next >= numPoints) {
					next = loop ? next - numPoints : numPoints - 1;
				}

				var pdp0 = dragPoints[prev];
				var pdp3 = dragPoints[next];

				var cc = CatmullCurve<TVertex>.GetInstance<TCatmullCurveFactory>(pdp0.Center, pdp1.Center, pdp2.Center, pdp3.Center);

				var vertex1 = new TVertex();

				vertex1.Set(pdp1.Center);
				vertex1.Smooth = pdp1.IsSmooth;
				vertex1.IsSlingshot = pdp1.IsSlingshot;
				vertex1.IsControlPoint = true;

				// Properties of last point don't matter, because it won't be added to the list on this pass (it'll get added as the first point of the next curve)
				vertex2.Set(pdp2.Center);

				vertices = RecurseSmoothLine(vertices, cc, 0.0f, 1.0f, vertex1, vertex2, accuracy);
			}

			if (!loop) {
				// Add the very last point to the list because nobody else added it
				vertex2.Smooth = true;
				vertex2.IsSlingshot = false;
				vertex2.IsControlPoint = false;
				vertices.Add(vertex2);
			}

			return vertices.ToArray();
		}

		public static float[] GetTextureCoords(DragPointData[] dragPoints, IRenderVertex[] vv)
		{
			var texPoints = new List<int>();
			var renderPoints = new List<int>();
			var noCoords = false;

			var numPoints = vv.Length;
			var controlPoint = 0;

			var coords = new float[numPoints];

			for (var i = 0; i < numPoints; ++i) {
				var prv = vv[i];
				if (prv.IsControlPoint) {
					if (!dragPoints[controlPoint].HasAutoTexture) {
						texPoints.Add(controlPoint);
						renderPoints.Add(i);
					}

					++controlPoint;
				}
			}

			if (texPoints.Count == 0) {
				// Special case - no texture coordinates were specified
				// Make them up starting at point 0
				texPoints.Add(0);
				renderPoints.Add(0);

				noCoords = true;
			}

			// Wrap the array around so we cover the last section
			texPoints.Add(texPoints[0] + dragPoints.Length);
			renderPoints.Add(renderPoints[0] + numPoints);

			for (var i = 0; i < texPoints.Count - 1; ++i) {
				var startRenderPoint = renderPoints[i] % numPoints;
				var endRenderPoint = renderPoints[i < numPoints - 1 ? i + 1 : 0] % numPoints;

				float startTexCoord;
				float endTexCoord;
				if (noCoords) {
					startTexCoord = 0.0f;
					endTexCoord = 1.0f;
				}
				else {
					startTexCoord = dragPoints[texPoints[i] % dragPoints.Length].TextureCoord;
					endTexCoord = dragPoints[texPoints[i + 1] % dragPoints.Length].TextureCoord;
				}

				var deltacoord = endTexCoord - startTexCoord;

				if (endRenderPoint <= startRenderPoint) {
					endRenderPoint += numPoints;
				}

				var totalLength = 0.0f;
				for (var l = startRenderPoint; l < endRenderPoint; ++l) {
					var pv1 = vv[l % numPoints];
					var pv2 = vv[(l + 1) % numPoints];

					var dx = pv1.GetX() - pv2.GetX() ;
					var dy = pv1.GetY() - pv2.GetY();
					var length = MathF.Sqrt(dx * dx + dy * dy);

					totalLength += length;
				}

				var partialLength = 0.0f;
				for (var l = startRenderPoint; l < endRenderPoint; ++l) {
					var pv1 = vv[l % numPoints];
					var pv2 = vv[(l + 1) % numPoints];

					var dx = pv1.GetX()  - pv2.GetX() ;
					var dy = pv1.GetY() - pv2.GetY();
					var length = MathF.Sqrt(dx * dx + dy * dy);
					if (totalLength == 0.0) {
						totalLength = 1.0f;
					}

					var texCoord = partialLength / totalLength;

					coords[l % numPoints] = texCoord * deltacoord + startTexCoord;
					partialLength += length;
				}
			}

			return coords;
		}

		private static List<TVertex> RecurseSmoothLine<TVertex>(List<TVertex> vv, CatmullCurve<TVertex> cc, float t1, float t2, TVertex vt1, TVertex vt2, float accuracy) where TVertex : IRenderVertex {

			var tMid = (t1 + t2) * 0.5f;

			var vMid = cc.GetPointAt(tMid);

			vMid.Smooth = true; // Generated points must always be smooth, because they are part of the curve
			vMid.IsSlingshot = false; // Slingshots can"t be along curves
			vMid.IsControlPoint = false; // We created this point, so it can"t be a control point

			if (FlatWithAccuracy(vt1, vt2, vMid, accuracy)) {
				// Add first segment point to array.
				// Last point never gets added by this recursive loop,
				// but that"s where it wraps around to the next curve.
				vv.Add(vt1);

			} else {
				vv = RecurseSmoothLine(vv, cc, t1, tMid, vt1, vMid, accuracy);
				vv = RecurseSmoothLine(vv, cc, tMid, t2, vMid, vt2, accuracy);
			}
			return vv;
		}

		private static bool FlatWithAccuracy(IRenderVertex v1, IRenderVertex v2, IRenderVertex vMid, float accuracy) {

			switch (v1) {
				case Vertex3D v31 when v2 is Vertex3D v32 && vMid is Vertex3D vMid3:
					return FlatWithAccuracy3(v31, v32, vMid3, accuracy);

				case Vertex2D v21 when v2 is Vertex2D v22 && vMid is Vertex2D vMid2:
					return FlatWithAccuracy2(v21, v22, vMid2, accuracy);

				default:
					throw new InvalidOperationException("Vertices must be either 2- or 3-dimensional.");
			}
		}

		private static bool FlatWithAccuracy2(Vertex2D v1, Vertex2D v2, Vertex2D vMid, float accuracy) {
			// compute double the signed area of the triangle (v1, vMid, v2)
			var dblArea = (vMid.X - v1.X) * (v2.Y - v1.Y) - (v2.X - v1.X) * (vMid.Y - v1.Y);
			return dblArea * dblArea < accuracy;
		}

		private static bool FlatWithAccuracy3(Vertex3D v1, Vertex3D v2, Vertex3D vMid, float accuracy) {
			// compute the square of double the signed area of the triangle (v1, vMid, v2)
			var cross = vMid.Clone().Sub(v1).Cross(v2.Clone().Sub(v1));
			var areaSq = cross.LengthSq();
			return areaSq < accuracy;
		}
	}
}
