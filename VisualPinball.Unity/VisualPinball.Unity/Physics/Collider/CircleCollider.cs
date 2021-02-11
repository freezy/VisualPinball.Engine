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

using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity
{
	internal struct CircleCollider : ICollider
	{
		private readonly ColliderHeader _header;

		public readonly float2 Center;
		public readonly float Radius;

		private readonly float _zHigh;
		private readonly float _zLow;

		public Aabb Aabb => new Aabb {
			Left = Center.x - Radius,
			Right = Center.x + Radius,
			Top = Center.y - Radius,
			Bottom = Center.y + Radius,
			ZLow = _zLow,
			ZHigh = _zHigh,
			ColliderEntity = _header.Entity,
			ColliderId = _header.Id
		};

		public CircleCollider(float2 center, float radius, float zLow, float zHigh, ColliderInfo info) : this()
		{
			_header.Init(info);
			Center = center;
			Radius = radius;
			_zHigh = zHigh;
			_zLow = zLow;
		}

		public unsafe void Allocate(BlobBuilder builder, ref BlobBuilderArray<BlobPtr<Collider>> colliders)
		{
			ref var ptr = ref UnsafeUtility.As<BlobPtr<Collider>, BlobPtr<CircleCollider>>(ref colliders[_header.Id]);
			ref var collider = ref builder.Allocate(ref ptr);
			UnsafeUtility.MemCpy(
				UnsafeUtility.AddressOf(ref collider),
				UnsafeUtility.AddressOf(ref this),
				sizeof(CircleCollider)
			);
		}

		#region Narrowphase

		public float HitTest(ref CollisionEventData collEvent, ref DynamicBuffer<BallInsideOfBufferElement> insideOfs, in BallData ball, float dTime)
		{
			// normal face, lateral, rigid
			return HitTestBasicRadius(ref collEvent, ref insideOfs, ball, dTime, true, true, true);
		}

		public float HitTestBasicRadius(ref CollisionEventData collEvent, ref DynamicBuffer<BallInsideOfBufferElement> insideOfs, in BallData ball, float dTime, bool direction, bool lateral, bool rigid)
		{
			// todo IsEnabled
			if (/*!IsEnabled || */ball.IsFrozen) {
				return -1.0f;
			}

			var c = new float3(Center.x, Center.y, 0.0f);
			var dist = ball.Position - c; // relative ball position
			var dv = ball.Velocity;

			var capsule3D = !lateral && ball.Position.z > _zHigh;
			var isKicker = _header.ItemType == ItemType.Kicker;
			var isKickerOrTrigger = _header.ItemType == ItemType.Trigger || _header.ItemType == ItemType.Kicker;

			float targetRadius;
			if (capsule3D) {
				targetRadius = Radius * (float) (13.0 / 5.0);
				c.z = _zHigh - Radius * (float) (12.0 / 5.0);
				dist.z = ball.Position.z - c.z; // ball rolling point - capsule center height
			}
			else {
				targetRadius = Radius;
				if (lateral) {
					targetRadius += ball.Radius;
				}

				dist.z = 0.0f;
				dv.z = 0.0f;
			}

			var bcddsq = math.lengthsq(dist);        // ball center to circle center distance ... squared
			var bcdd = math.sqrt(bcddsq);       // distance center to center
			if (bcdd <= 1.0e-6) {
				// no hit on exact center
				return -1.0f;
			}

			var b = math.dot(dist, dv);
			var bnv = b / bcdd;                  // ball normal velocity

			if (direction && bnv > PhysicsConstants.LowNormVel) {
				// clearly receding from radius
				return -1.0f;
			}

			var bnd = bcdd - targetRadius;       // ball normal distance to

			var a = math.lengthsq(dv);

			var hitTime = 0f;
			var isUnhit = false;
			var isContact = false;

			// Kicker is special.. handle ball stalled on kicker, commonly hit while receding, knocking back into kicker pocket
			if (isKicker && bnd <= 0 && bnd >= -Radius && a < PhysicsConstants.ContactVel * PhysicsConstants.ContactVel/* && ball.Hit.IsRealBall()*/) {
				BallData.SetOutsideOf(ref insideOfs, _header.Entity);
			}

			// contact positive possible in future ... objects Negative in contact now
			if (rigid && bnd < PhysicsConstants.PhysTouch) {
				if (bnd < -ball.Radius) {
					return -1.0f;
				}

				if (math.abs(bnv) <= PhysicsConstants.ContactVel) {
					isContact = true;

				} else {
					// estimate based on distance and speed along distance
					// the ball can be that fast that in the next hit cycle the ball will be inside the hit shape of a bumper or other element.
					// if that happens bnd is negative and greater than the negative bnv value that results in a negative hittime
					// below the "if (infNan(hittime) || hittime <0.F...)" will then be true and the hit function will return -1.0f = no hit
					hitTime = math.max(0.0f, (float) (-bnd / bnv));
				}

			} else if (isKickerOrTrigger /*&& ball.Hit.IsRealBall()*/ && bnd < 0 == BallData.IsOutsideOf(in insideOfs, in _header.Entity)) {
				// triggers & kickers

				// here if ... ball inside and no hit set .... or ... ball outside and hit set
				if (math.abs(bnd - Radius) < 0.05) {
					// if ball appears in center of trigger, then assumed it was gen"ed there
					BallData.SetInsideOf(ref insideOfs, _header.Entity); // special case for trigger overlaying a kicker

				} else {
					// this will add the ball to the trigger space without a Hit
					isUnhit = bnd > 0; // ball on outside is UnHit, otherwise it's a Hit
				}

			} else {
				if (!rigid && bnd * bnv > 0 || a < 1.0e-8) {
					// (outside and receding) or (inside and approaching)
					// no hit ... ball not moving relative to object
					return -1.0f;
				}

				var solved = Math.SolveQuadraticEq(a, 2.0f * b, bcddsq - targetRadius * targetRadius,
					out var time1, out var time2);
				if (!solved) {
					return -1.0f;
				}

				isUnhit = time1 * time2 < 0;
				hitTime = isUnhit ? math.max(time1, time2) : math.min(time1, time2); // ball is inside the circle
			}

			if (float.IsNaN(hitTime) || float.IsInfinity(hitTime) || hitTime < 0 || hitTime > dTime) {
				// contact out of physics frame
				return -1.0f;
			}

			var hitZ = ball.Position.z + ball.Velocity.z * hitTime; // rolling point
			if (hitZ + ball.Radius * 0.5 < _zLow
			    || !capsule3D && hitZ - ball.Radius * 0.5 > _zHigh
			    || capsule3D && hitZ < _zHigh) {
				return -1.0f;
			}

			var hitX = ball.Position.x + ball.Velocity.x * hitTime;
			var hitY = ball.Position.y + ball.Velocity.y * hitTime;
			var sqrLen = (hitX - c.x) * (hitX - c.x) + (hitY - c.y) * (hitY - c.y);

			// over center?
			if (sqrLen > 1.0e-8) {
				// no
				var invLen = 1.0f / math.sqrt(sqrLen);
				collEvent.HitNormal.x = (hitX - c.x) * invLen;
				collEvent.HitNormal.y = (hitY - c.y) * invLen;
				collEvent.HitNormal.z = 0;

			} else {
				// yes, over center
				collEvent.HitNormal.x = 0.0f; // make up a value, any direction is ok
				collEvent.HitNormal.y = 1.0f;
				collEvent.HitNormal.z = 0.0f;
			}

			if (!rigid) {
				// non rigid body collision? return direction
				collEvent.HitFlag = isUnhit; // UnHit signal is receding from target
			}

			collEvent.IsContact = isContact;
			if (isContact) {
				collEvent.HitOrgNormalVelocity = bnv;
			}

			collEvent.HitDistance = bnd; // actual contact distance ...
			//coll.M_hitRigid = rigid;                         // collision type

			return hitTime;
		}

		#endregion

		public void Collide(ref BallData ball, in CollisionEventData collEvent, ref Random random)
		{
			BallCollider.Collide3DWall(ref ball, in _header.Material, in collEvent, in collEvent.HitNormal, ref random);
		}
	}
}
