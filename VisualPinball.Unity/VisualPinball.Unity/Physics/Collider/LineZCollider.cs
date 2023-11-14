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

namespace VisualPinball.Unity
{
	internal struct LineZCollider : ICollider
	{
		public int Id
		{
			get => Header.Id;
			set => Header.Id = value;
		}

		public ColliderHeader Header;

		public float2 XY;
		private float _zLow;
		private float _zHigh;

		public float XyY { set => XY.y = value; }

		public ColliderBounds Bounds => new ColliderBounds(Header.ItemId, Header.Id, new Aabb (
			XY.x,
			XY.x,
			XY.y,
			XY.y,
			_zLow,
			_zHigh
		));

		public LineZCollider(float2 xy, float zLow, float zHigh, ColliderInfo info) : this()
		{
			Header.Init(info, ColliderType.LineZ);
			XY = xy;
			_zLow = zLow;
			_zHigh = zHigh;
		}

		#region Narrowphase

		public float HitTest(ref CollisionEventData collEvent, in BallState ball, float dTime)
		{
			return HitTest(ref collEvent, in this, in ball, dTime);
		}

		public static float HitTest(ref CollisionEventData collEvent, in LineZCollider coll, in BallState ball, float dTime)
		{
			var bp2d = new float2(ball.Position.x, ball.Position.y);
			var dist = bp2d - coll.XY;                                       // relative ball position
			var dv = new float2(ball.Velocity.x, ball.Velocity.y);

			var bcddsq = math.lengthsq(dist);                             // ball center to line distance squared
			var bcdd = math.sqrt(bcddsq);                                     // distance ball to line
			if (bcdd <= 1.0e-6) {
				// no hit on exact center
				return -1.0f;
			}

			var b = math.dot(dist, dv);
			var bnv = b / bcdd;                                                // ball normal velocity

			if (bnv > PhysicsConstants.ContactVel) {
				// clearly receding from radius
				return -1.0f;
			}

			var bnd = bcdd - ball.Radius;                                 // ball distance to line
			var a = math.lengthsq(dv);

			float hitTime;
			var isContact = false;

			if (bnd < PhysicsConstants.PhysTouch) {
				// already in collision distance?
				if (math.abs(bnv) <= PhysicsConstants.ContactVel) {
					isContact = true;
					hitTime = 0f;

				} else {
					// estimate based on distance and speed along distance
					hitTime = -bnd / bnv;
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

			var hitZ = ball.Position.z + hitTime * ball.Velocity.z;            // ball z position at hit time

			if (hitZ < coll._zLow || hitZ > coll._zHigh) {
				// check z coordinate
				return -1.0f;
			}

			var hitX = ball.Position.x + hitTime * ball.Velocity.x;            // ball x position at hit time
			var hitY = ball.Position.y + hitTime * ball.Velocity.y;            // ball y position at hit time

			var norm = math.normalize(new float2(hitX - coll.XY.x, hitY - coll.XY.y));
			collEvent.HitNormal.x = norm.x;
			collEvent.HitNormal.y = norm.y;
			collEvent.HitNormal.z = 0f;

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

		public void Collide(ref BallState ball, ref NativeQueue<EventData>.ParallelWriter hitEvents,
			in CollisionEventData collEvent, ref Random random)
		{
			var dot = math.dot(collEvent.HitNormal, ball.Velocity);
			BallCollider.Collide3DWall(ref ball, in Header.Material, in collEvent, in collEvent.HitNormal, ref random);

			if (dot <= -Header.Threshold) {
				Collider.FireHitEvent(ref ball, ref hitEvents, in Header);
			}
		}

		#endregion

		public LineZCollider Transform(float4x4 matrix)
		{
			var t = matrix.GetTranslation();

			XY += t.xy;
			_zLow += t.z;
			_zHigh += t.z;

			return this;
		}
	}
}
