// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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

using Unity.Collections;
using Unity.Mathematics;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity
{
	internal struct TriangleCollider : ICollider
	{
		public int Id
		{
			get => Header.Id;
			set => Header.Id = value;
		}

		public ColliderHeader Header;

		public float3 Rgv0;
		public float3 Rgv1;
		public float3 Rgv2;
		private float3 _normal;

		public float3 Normal() => _normal;
		
		public override string ToString() => $"TriangleCollider[{Header.ItemId}] ({Rgv0.x}/{Rgv0.y}/{Rgv0.z}), ({Rgv1.x}/{Rgv1.y}/{Rgv1.z}), ({Rgv2.x}/{Rgv2.y}/{Rgv2.z}) at ({_normal.x}/{_normal.y}/{_normal.z})";

		public ColliderBounds Bounds { get; private set; }

		public TriangleCollider(float3 rgv0, float3 rgv1, float3 rgv2, ColliderInfo info) : this()
		{
			Header.Init(info, ColliderType.Triangle);
			Rgv0 = rgv0;
			Rgv1 = rgv1;
			Rgv2 = rgv2;
			CalculateBounds();

			var e0 = rgv2 - rgv0;
			var e1 = rgv1 - rgv0;
			_normal = math.normalizesafe(math.cross(e0, e1));
		}

		public static bool IsDegenerate(float3 rg0, float3 rg1, float3 rg2)
		{
			var e0 = rg2 - rg0;
			var e1 = rg1 - rg0;
			var normal = math.cross(e0, e1);
			normal.NormalizeSafe();
			return normal.IsZero();
		}

		#region Narrowphase

		public float HitTest(ref CollisionEventData collEvent, in InsideOfs insideOfs, in BallState ball, float dTime)
		{
			// if (!this.isEnabled) {
			// 	return -1.0;
			// }

			var bnv = math.dot(_normal, ball.Velocity);         // speed in Normal-vector direction
			if (bnv > PhysicsConstants.ContactVel) {                          // return if clearly ball is receding from object
				return -1.0f;
			}

			// Point on the ball that will hit the polygon, if it hits at all
			var normRadius = ball.Radius * _normal;
			var hitPos = ball.Position - normRadius;     // nearest point on ball ... projected radius along norm
			var hpSubRgv0 = hitPos - Rgv0;
			var bnd = math.dot(_normal, hpSubRgv0);                                // distance from plane to ball

			if (bnd < -ball.Radius) {
				// (ball normal distance) excessive penetration of object skin ... no collision HACK
				return -1.0f;
			}

			var isContact = false;
			float hitTime;

			if (bnd <= PhysicsConstants.PhysTouch) {
				if (math.abs(bnv) <= PhysicsConstants.ContactVel) {
					hitTime = 0;
					isContact = true;

				} else if (bnd <= 0) {
					hitTime = 0;                               // zero time for rigid fast bodies

				} else {
					hitTime = bnd / -bnv;
				}

			} else if (math.abs(bnv) > PhysicsConstants.LowNormVel) {         // not velocity low?
				hitTime = bnd / -bnv;                          // rate ok for safe divide

			} else {
				return -1.0f;                // wait for touching
			}

			if (float.IsNaN(hitTime) || float.IsInfinity(hitTime) || hitTime < 0 || hitTime > dTime) {
				return -1.0f;                // time is outside this frame ... no collision
			}

			// advance hit point to contact
			var adv = hitTime * ball.Velocity;
			hitPos += adv;

			// Check if hitPos is within the triangle
			// 1. Compute vectors
			var v0 = Rgv2 - Rgv0;
			var v1 = Rgv1 - Rgv0;
			var v2 = hitPos - Rgv0;

			// 2. Compute dot products
			var dot00 = math.dot(v0, v0);
			var dot01 = math.dot(v0, v1);
			var dot02 = math.dot(v0, v2);
			var dot11 = math.dot(v1, v1);
			var dot12 = math.dot(v1, v2);

			// 3. Compute barycentric coordinates
			var invDenom = 1.0 / (dot00 * dot11 - dot01 * dot01);
			var u = (dot11 * dot02 - dot01 * dot12) * invDenom;
			var v = (dot00 * dot12 - dot01 * dot02) * invDenom;

			// 4. Check if point is in triangle
			var pointInTriangle = u >= 0 && v >= 0 && u + v <= 1;

			if (pointInTriangle) {

				if (Header.ItemType == ItemType.Trigger && bnd < 0 == insideOfs.IsOutsideOf(Header.ItemId, ball.Id)) {
					collEvent.HitFlag = bnd > 0;
				}

				collEvent.HitNormal = _normal;
				collEvent.HitDistance = bnd;                        // 3dhit actual contact distance ...
				//coll.m_hitRigid = true;                      // collision type

				if (isContact) {
					collEvent.IsContact = true;
					collEvent.HitOrgNormalVelocity = bnv;
				}
				return hitTime;
			}

			return -1.0f;
		}

		#endregion

		#region Collision

		public void Collide(ref BallState ball, ref NativeQueue<EventData>.ParallelWriter hitEvents,
			in CollisionEventData collEvent, ref Random random)
		{
			var dot = -math.dot(collEvent.HitNormal, ball.Velocity);
			BallCollider.Collide3DWall(ref ball, in Header.Material, in collEvent, in _normal, ref random);

			if (Header.FireEvents && dot >= Header.Threshold && Header.IsPrimitive) {
				// todo m_obj->m_currentHitThreshold = dot;
				Collider.FireHitEvent(ref ball, ref hitEvents, in Header);
			}
		}

		#endregion

		#region Transformation

		public TriangleCollider Transform(float4x4 matrix)
		{
			Transform(this, matrix);
			return this;
		}

		public void Transform(TriangleCollider triangle, float4x4 matrix)
		{
			Rgv0 = math.mul(matrix, new float4(triangle.Rgv0, 1f)).xyz;
			Rgv1 = math.mul(matrix, new float4(triangle.Rgv1, 1f)).xyz;
			Rgv2 = math.mul(matrix, new float4(triangle.Rgv2, 1f)).xyz;
			_normal = math.normalizesafe(math.cross(Rgv2 - Rgv0, Rgv1 - Rgv0));
			CalculateBounds();
		}

		public TriangleCollider TransformAabb(float4x4 matrix)
		{
			var p1 = matrix.MultiplyPoint(Rgv0);
			var p2 = matrix.MultiplyPoint(Rgv1);
			var p3 = matrix.MultiplyPoint(Rgv2);

			var min = math.min(p1, math.min(p2, p3));
			var max = math.max(p1, math.max(p2, p3));

			Bounds = new ColliderBounds(Header.ItemId, Header.Id, new Aabb(min, max));

			return this;
		}

		private void CalculateBounds()
		{
			Bounds = new ColliderBounds(Header.ItemId, Header.Id, new Aabb(
				math.min(Rgv0.x, math.min(Rgv1.x, Rgv2.x)),
				math.max(Rgv0.x, math.max(Rgv1.x, Rgv2.x)),
				math.min(Rgv0.y, math.min(Rgv1.y, Rgv2.y)),
				math.max(Rgv0.y, math.max(Rgv1.y, Rgv2.y)),
				math.min(Rgv0.z, math.min(Rgv1.z, Rgv2.z)),
				math.max(Rgv0.z, math.max(Rgv1.z, Rgv2.z))
			));
		}

		#endregion
	}
}
