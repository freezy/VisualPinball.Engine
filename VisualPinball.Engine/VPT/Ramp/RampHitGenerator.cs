using System;
using System.Collections.Generic;
using System.Linq;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.VPT.Ramp
{
	public class RampHitGenerator
	{
		private readonly RampData _data;
		private readonly RampMeshGenerator _meshGenerator;

		public RampHitGenerator(RampData data, RampMeshGenerator meshGenerator)
		{
			_data = data;
			_meshGenerator = meshGenerator;
		}

		public HitObject[] GenerateHitObjects(Table.Table table, EventProxy events)
		{
			var hitObjects = new List<HitObject>();
			var rv = _meshGenerator.GetRampVertex(table, PhysicsConstants.HitShapeDetailLevel, true);
			var rgvLocal = rv.RgvLocal;
			var rgHeight1 = rv.PointHeights;
			var vertexCount = rv.VertexCount;

			var (wallHeightRight, wallHeightLeft) = GetWallHeights();

			Vertex2D pv1, pv2, pv3 = new Vertex2D(), pv4 = new Vertex2D();

			// Add line segments for right ramp wall.
			if (wallHeightRight > 0.0f) {
				for (var i = 0; i < vertexCount - 1; i++) {
					pv2 = rgvLocal[i];
					pv3 = rgvLocal[i + 1];

					hitObjects.AddRange(GenerateWallLineSeg(pv2, pv3, i > 0,
						rgHeight1[i], rgHeight1[i + 1], wallHeightRight));
					hitObjects.AddRange(GenerateWallLineSeg(pv3, pv2, i < vertexCount - 2,
						rgHeight1[i], rgHeight1[i + 1], wallHeightRight));

					// add joints at start and end of right wall
					if (i == 0) {
						hitObjects.Add(GenerateJoint2D(pv2, rgHeight1[0], rgHeight1[0] + wallHeightRight));
					}

					if (i == vertexCount - 2) {
						hitObjects.Add(GenerateJoint2D(pv3, rgHeight1[vertexCount - 1],
							rgHeight1[vertexCount - 1] + wallHeightRight));
					}
				}
			}

			// Add line segments for left ramp wall.
			if (wallHeightLeft > 0.0f) {
				for (var i = 0; i < vertexCount - 1; i++) {
					pv2 = rgvLocal[vertexCount + i];
					pv3 = rgvLocal[vertexCount + i + 1];

					hitObjects.AddRange(GenerateWallLineSeg(pv2, pv3, i > 0,
						rgHeight1[vertexCount - i - 2],  rgHeight1[vertexCount - i - 1], wallHeightLeft));
					hitObjects.AddRange(GenerateWallLineSeg(pv3, pv2, i < vertexCount - 2,
						rgHeight1[vertexCount - i - 2], rgHeight1[vertexCount - i - 1], wallHeightLeft));

					// add joints at start and end of left wall
					if (i == 0) {
						hitObjects.Add(GenerateJoint2D(pv2, rgHeight1[vertexCount - 1],
							rgHeight1[vertexCount - 1] + wallHeightLeft));
					}

					if (i == vertexCount - 2) {
						hitObjects.Add(GenerateJoint2D(pv3, rgHeight1[0], rgHeight1[0] + wallHeightLeft));
					}
				}
			}

			// Add hit triangles for the ramp floor.
			HitTriangle ph3dpoly, ph3dpolyOld = null;
			Vertex3D[] rgv3D;

			for (var i = 0; i < vertexCount - 1; i++) {
				/*
				* Layout of one ramp quad seen from above, ramp direction is bottom to top:
				*
				*    3 - - 4
				*    | \   |
				*    |   \ |
				*    2 - - 1
				*/
				pv1 = rgvLocal[i]; // i-th right
				pv2 = rgvLocal[vertexCount * 2 - i - 1]; // i-th left
				pv3 = rgvLocal[vertexCount * 2 - i - 2]; // (i+1)-th left
				pv4 = rgvLocal[i + 1]; // (i+1)-th right

				// left ramp floor triangle, CCW order
				rgv3D = new [] {
					new Vertex3D(pv2.X, pv2.Y, rgHeight1[i]),
					new Vertex3D(pv1.X, pv1.Y, rgHeight1[i]),
					new Vertex3D(pv3.X, pv3.Y, rgHeight1[i + 1])
				};

				// add joint for starting edge of ramp
				if (i == 0) {
					hitObjects.Add(GenerateJoint(rgv3D[0], rgv3D[1]));
				}

				// add joint for left edge
				hitObjects.Add(GenerateJoint(rgv3D[0], rgv3D[2]));

				//!! this is not efficient at all, use native triangle-soup directly somehow
				ph3dpoly = new HitTriangle(rgv3D);

				if (!ph3dpoly.IsDegenerate) { // degenerate triangles happen if width is 0 at some point
					hitObjects.Add(ph3dpoly);
					hitObjects.AddRange(CheckJoint(ph3dpolyOld, ph3dpoly));
					ph3dpolyOld = ph3dpoly;
				}

				// right ramp floor triangle, CCW order
				rgv3D = new [] {
					new Vertex3D(pv3.X, pv3.Y, rgHeight1[i + 1]),
					new Vertex3D(pv1.X, pv1.Y, rgHeight1[i]),
					new Vertex3D(pv4.X, pv4.Y, rgHeight1[i + 1])
				};

				// add joint for right edge
				hitObjects.Add(GenerateJoint(rgv3D[1], rgv3D[2]));

				ph3dpoly = new HitTriangle(rgv3D);
				if (!ph3dpoly.IsDegenerate)
					hitObjects.Add(ph3dpoly);

				hitObjects.AddRange(CheckJoint(ph3dpolyOld, ph3dpoly));
				ph3dpolyOld = ph3dpoly;
			}

			if (vertexCount >= 2) {
				// add joint for final edge of ramp
				var v1 = new Vertex3D(pv4.X, pv4.Y, rgHeight1[vertexCount - 1]);
				var v2 = new Vertex3D(pv3.X, pv3.Y, rgHeight1[vertexCount - 1]);
				hitObjects.Add(GenerateJoint(v1, v2));
			}

			// add outside bottom,
			// joints at the intersections are not needed since the inner surface has them
			// this surface is identical... except for the direction of the normal face.
			// hence the joints protect both surface edges from having a fall through

			for (var i = 0; i < vertexCount - 1; i++) {
				// see sketch above
				pv1 = rgvLocal[i];
				pv2 = rgvLocal[vertexCount * 2 - i - 1];
				pv3 = rgvLocal[vertexCount * 2 - i - 2];
				pv4 = rgvLocal[i + 1];

				// left ramp triangle, order CW
				rgv3D = new[] {
					new Vertex3D(pv1.X, pv1.Y, rgHeight1[i]),
					new Vertex3D(pv2.X, pv2.Y, rgHeight1[i]),
					new Vertex3D(pv3.X, pv3.Y, rgHeight1[i + 1])
				};

				ph3dpoly = new HitTriangle(rgv3D);
				if (!ph3dpoly.IsDegenerate) {
					hitObjects.Add(ph3dpoly);
				}

				// right ramp triangle, order CW
				rgv3D = new[] {
					new Vertex3D(pv3.X, pv3.Y, rgHeight1[i + 1]),
					new Vertex3D(pv4.X, pv4.Y, rgHeight1[i + 1]),
					new Vertex3D(pv1.X, pv1.Y, rgHeight1[i])
				};

				ph3dpoly = new HitTriangle(rgv3D);
				if (!ph3dpoly.IsDegenerate) {
					hitObjects.Add(ph3dpoly);
				}
			}

			return hitObjects
				.Select(obj => SetupHitObject(obj, events, table))
				.ToArray();
		}

		private Tuple<float, float> GetWallHeights()
		{
			switch (_data.RampType) {
				case RampType.RampTypeFlat: return new Tuple<float, float>(_data.RightWallHeight, _data.LeftWallHeight);
				case RampType.RampType1Wire: return new Tuple<float, float>(31.0f, 31.0f);
				case RampType.RampType2Wire: return new Tuple<float, float>(31.0f, 31.0f);
				case RampType.RampType4Wire: return new Tuple<float, float>(62.0f, 62.0f);
				case RampType.RampType3WireRight: return new Tuple<float, float>(62.0f, (float)(6 + 12.5));
				case RampType.RampType3WireLeft: return new Tuple<float, float>((float)(6 + 12.5), 62.0f);
				default:
					throw new InvalidOperationException($"Unknown ramp type {_data.RampType}");
			}
		}

		private List<HitObject> GenerateWallLineSeg(Vertex2D pv1, Vertex2D pv2, bool pv3Exists, float height1, float height2, float wallHeight)
		{
			var hitObjects = new List<HitObject>();

			//!! Hit-walls are still done via 2D line segments with only a single lower and upper border, so the wall will always reach below and above the actual ramp -between- two points of the ramp
			// Thus, subdivide until at some point the approximation error is 'subtle' enough so that one will usually not notice (i.e. dependent on ball size)
			if (height2 - height1 > 2.0 * PhysicsConstants.PhysSkin) { //!! use ballsize
				hitObjects.AddRange(GenerateWallLineSeg(pv1, pv1.Clone().Add(pv2).MultiplyScalar(0.5f), pv3Exists, height1, (height1 + height2) * 0.5f, wallHeight));
				hitObjects.AddRange(GenerateWallLineSeg(pv1.Clone().Add(pv2).MultiplyScalar(0.5f), pv2, true, (height1 + height2) * 0.5f, height2, wallHeight));

			} else {
				hitObjects.Add(new LineSeg(pv1, pv2, height1, height2 + wallHeight));
				if (pv3Exists) {
					hitObjects.Add(GenerateJoint2D(pv1, height1, height2 + wallHeight));
				}
			}
			return hitObjects;
		}

		private static HitLineZ GenerateJoint2D(Vertex2D p, float zLow, float zHigh) => new HitLineZ(p, zLow, zHigh);

		private static HitLine3D GenerateJoint(Vertex3D v1, Vertex3D v2) => new HitLine3D(v1, v2);

		private static List<HitObject> CheckJoint(HitTriangle ph3d1, HitTriangle ph3d2)
		{
			var hitObjects = new List<HitObject>();
			if (ph3d1 != null) {   // may be null in case of degenerate triangles
				var jointNormal = Vertex3D.CrossProduct(ph3d1.Normal, ph3d2.Normal);
				if (jointNormal.LengthSq() < 1e-8) { // coplanar triangles need no joints
					return hitObjects;
				}
			}
			// By convention of the calling function, points 1 [0] and 2 [1] of the second polygon will
			// be the common-edge points
			hitObjects.Add(GenerateJoint(ph3d2.Rgv[0], ph3d2.Rgv[1]));
			return hitObjects;
		}

		private HitObject SetupHitObject(HitObject obj, EventProxy events, Table.Table table) {
			obj.ApplyPhysics(_data, table);

			// the rubber is of type ePrimitive for triggering the event in HitTriangle::Collide()
			obj.SetType(CollisionType.Primitive);

			// hard coded threshold for now
			obj.Threshold = _data.Threshold;
			obj.Obj = events;
			obj.FireEvents = _data.HitEvent;
			return obj;
		}
	}
}
