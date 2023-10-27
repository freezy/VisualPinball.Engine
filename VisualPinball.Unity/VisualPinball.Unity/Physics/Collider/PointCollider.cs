﻿// Visual Pinball Engine
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
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Engine.Common;

namespace VisualPinball.Unity
{
	internal struct PointCollider : ICollider
	{
		public int Id => Header.Id;

		public ColliderHeader Header;

		public readonly float3 P;

		public ColliderBounds Bounds => new ColliderBounds(Header.ItemId, Header.Id, new Aabb(
			P.x,
			P.x,
			P.y,
			P.y,
			P.z,
			P.z
		));

		public PointCollider(float3 p, ColliderInfo info) : this()
		{
			Header.Init(info, ColliderType.Point);
			P = p;
		}

		public unsafe void Allocate(BlobBuilder builder, ref BlobBuilderArray<BlobPtr<Collider>> colliders, int colliderId)
		{
			Header.Id = colliderId;
			ref var ptr = ref UnsafeUtility.As<BlobPtr<Collider>, BlobPtr<PointCollider>>(ref colliders[Header.Id]);
			ref var collider = ref builder.Allocate(ref ptr);
			UnsafeUtility.MemCpy(
				UnsafeUtility.AddressOf(ref collider),
				UnsafeUtility.AddressOf(ref this),
				sizeof(PointCollider)
			);
		}

		#region Narrowphase

		public float HitTest(ref CollisionEventData collEvent, in BallState ball, float dTime)
		{
			// relative ball position
			var dist = ball.Position - P;

			var bcddsq = math.lengthsq(dist);                                  // ball center to line distance squared
			var bcdd = math.sqrt(bcddsq);                                      // distance ball to line
			if (bcdd <= 1.0e-6) {
				// no hit on exact center
				return -1.0f;
			}

			var b = math.dot(dist, ball.Velocity);
			var bnv = b / bcdd;                                                // ball normal velocity

			if (bnv > PhysicsConstants.ContactVel) {
				// clearly receding from radius
				return -1.0f;
			}

			var bnd = bcdd - ball.Radius;                                      // ball distance to line
			var a = math.lengthsq(ball.Velocity);

			float hitTime;
			var isContact = false;

			if (bnd < PhysicsConstants.PhysTouch) {
				// already in collision distance?
				if (math.abs(bnv) <= PhysicsConstants.ContactVel) {
					isContact = true;
					hitTime = 0;

				} else {
					// estimate based on distance and speed along distance
					hitTime = math.max(0.0f, -bnd / bnv);
				}

			} else {
				if (a < 1.0e-8) {
					// no hit - ball not moving relative to object
					return -1.0f;
				}

				var solved = Math.SolveQuadraticEq(a, 2.0f * b, bcddsq - ball.Radius * ball.Radius,
					out var time1, out var time2);
				if (!solved) {
					return -1.0f;
				}

				// find smallest non-negative solution
				hitTime = time1 * time2 < 0 ? math.max(time1, time2) : math.min(time1, time2);
			}

			if (float.IsNaN(hitTime) || float.IsInfinity(hitTime) || hitTime < 0 || hitTime > dTime) {
				// contact out of physics frame
				return -1.0f;
			}

			var hitPos = ball.Position + hitTime * ball.Velocity;
			collEvent.HitNormal = math.normalize(hitPos - P);

			collEvent.IsContact = isContact;
			if (isContact) {
				collEvent.HitOrgNormalVelocity = bnv;
			}

			collEvent.HitDistance = bnd; // actual contact distance
			//coll.M_hitRigid = true;

			return hitTime;
		}

		#endregion

		#region Collision

		public void Collide(ref BallState ball,  ref NativeQueue<EventData>.ParallelWriter hitEvents,
			in CollisionEventData collEvent, ref Random random)
		{
			var dot = math.dot(collEvent.HitNormal, ball.Velocity);
			BallCollider.Collide3DWall(ref ball, in Header.Material, in collEvent, in collEvent.HitNormal, ref random);

			if (dot <= -Header.Threshold) {
				Collider.FireHitEvent(ref ball, ref hitEvents, in Header);
			}
		}

		#endregion
	}
}
