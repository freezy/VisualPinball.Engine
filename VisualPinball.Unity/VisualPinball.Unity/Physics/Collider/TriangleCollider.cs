// Visual Pinball Engine
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

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity
{
	internal struct TriangleCollider : ICollider
	{
		private ColliderHeader _header;

		public readonly float3 _rgv0;
		public readonly float3 _rgv1;
		private readonly float3 _rgv2;
		private readonly float3 _normal;

		public float3 Normal() => _normal;

		public Aabb Aabb => new Aabb {
			Left = math.min(_rgv0.x, math.min(_rgv1.x, _rgv2.x)),
			Right = math.max(_rgv0.x, math.max(_rgv1.x, _rgv2.x)),
			Top = math.min(_rgv0.y, math.min(_rgv1.y, _rgv2.y)),
			Bottom = math.max(_rgv0.y, math.max(_rgv1.y, _rgv2.y)),
			ZLow = math.min(_rgv0.z, math.min(_rgv1.z, _rgv2.z)),
			ZHigh = math.max(_rgv0.z, math.max(_rgv1.z, _rgv2.z)),
			ColliderEntity = _header.Entity,
			ColliderId = _header.Id
		};

		public TriangleCollider(float3 rgv0, float3 rgv1, float3 rgv2, ColliderInfo info) : this()
		{
			_header.Init(info, ColliderType.Triangle);
			_rgv0 = rgv0;
			_rgv1 = rgv1;
			_rgv2 = rgv2;

			var e0 = rgv2 - rgv0;
			var e1 = rgv1 - rgv0;
			_normal = math.normalizesafe(math.cross(e0, e1));
		}

		public unsafe void Allocate(BlobBuilder builder, ref BlobBuilderArray<BlobPtr<Collider>> colliders)
		{
			ref var ptr = ref UnsafeUtility.As<BlobPtr<Collider>, BlobPtr<TriangleCollider>>(ref colliders[_header.Id]);
			ref var collider = ref builder.Allocate(ref ptr);
			UnsafeUtility.MemCpy(
				UnsafeUtility.AddressOf(ref collider),
				UnsafeUtility.AddressOf(ref this),
				sizeof(TriangleCollider)
			);
		}

		public static bool IsDegenerate(float3 rg0, float3 rg1, float3 rg2)
		{
			var e0 = rg2 - rg0;
			var e1 = rg1 - rg0;
			var normal = math.normalizesafe(math.cross(e0, e1));
			return normal.IsZero();
		}

		#region Narrowphase

		public float HitTest(ref CollisionEventData collEvent, in DynamicBuffer<BallInsideOfBufferElement> insideOfs, in BallData ball, float dTime)
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
			var hpSubRgv0 = hitPos - _rgv0;
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
			var v0 = _rgv2 - _rgv0;
			var v1 = _rgv1 - _rgv0;
			var v2 = hitPos - _rgv0;

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

				if (_header.ItemType == ItemType.Trigger && bnd < 0 == BallData.IsOutsideOf(in insideOfs, in _header.Entity)) {
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

		public void Collide(ref BallData ball, ref NativeQueue<EventData>.ParallelWriter hitEvents,
			in Entity ballEntity, in CollisionEventData collEvent, ref Random random)
		{
			var dot = -math.dot(collEvent.HitNormal, ball.Velocity);
			BallCollider.Collide3DWall(ref ball, in _header.Material, in collEvent, in _normal, ref random);

			if (_header.FireEvents && dot >= _header.Threshold && _header.IsPrimitive) {
				// todo m_obj->m_currentHitThreshold = dot;
				Collider.FireHitEvent(ref ball, ref hitEvents, in ballEntity, in _header);
			}
		}
	}
}
