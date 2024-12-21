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
	internal struct LineCollider : ICollider
	{
		public int Id
		{
			get => Header.Id;
			set => Header.Id = value;
		}

		public ColliderHeader Header;

		public float2 V1;
		public float2 V2;

		public float2 Normal;
		public float ZLow;
		public float ZHigh;
		private float _length;

		internal ItemType ItemType => Header.ItemType;
		private int ItemId => Header.ItemId;

		public float V1y {
			set {
				V1.y = value;
				CalculateBounds();
			}
		}

		public float V2y {
			set {
				V2.y = value;
				CalculateBounds();
			}
		}

		public override string ToString() => $"LineCollider[{Header.ItemId}] ({V1.x}/{V1.y}@{ZLow}) -> ({V2.x}/{V2.y}@{ZHigh}) at ({Normal.x}/{Normal.y}), len: {_length}";

		public ColliderBounds Bounds { get; private set; }

		public LineCollider(float2 v1, float2 v2, float zLow, float zHigh, ColliderInfo info) : this()
		{
			Header.Init(info, ColliderType.Line);
			V1 = v1;
			V2 = v2;
			ZLow = zLow;
			ZHigh = zHigh;
			CalcNormal();
			CalculateBounds();
		}

		public void CalcNormal()
		{
			var vT = new float2(V1.x - V2.x, V1.y - V2.y);
			_length = math.length(vT);

			// Set up line normal
			var invLength = 1.0f / _length;
			Normal.x = vT.y * invLength;
			Normal.y = -vT.x * invLength;
		}

		#region Narrowphase

		public static float HitTest(ref CollisionEventData collEvent,
			ref InsideOfs insideOfs, in LineCollider coll, in BallState ball, float dTime)
		{
			return HitTestBasic(ref collEvent, ref insideOfs, in coll, in ball, dTime, true, true, true); // normal face, lateral, rigid
		}

		public float HitTest(ref CollisionEventData collEvent,
			ref InsideOfs insideOfs, in BallState ball, float dTime)
		{
			return HitTestBasic(ref collEvent, ref insideOfs, in this, in ball, dTime, true, true, true); // normal face, lateral, rigid
		}

		public float HitTestBasic(ref CollisionEventData collEvent, ref InsideOfs insideOfs, in BallState ball, float dTime,
			bool direction, bool lateral, bool rigid)
		{
			return HitTestBasic(ref collEvent, ref insideOfs, in this, in ball, dTime, direction, lateral, rigid);
		}

		public static float HitTestBasic(ref CollisionEventData collEvent, ref InsideOfs insideOfs, in LineCollider coll, in BallState ball, float dTime, bool direction, bool lateral, bool rigid)
		{
			// ball velocity
			var ballVx = ball.Velocity.x;
			var ballVy = ball.Velocity.y;

			// ball velocity normal to segment, positive if receding, zero=parallel
			var bnv = ballVx * coll.Normal.x + ballVy * coll.Normal.y;
			var isUnHit = bnv > PhysicsConstants.LowNormVel;

			// direction true and clearly receding from normal face
			if (direction && bnv > PhysicsConstants.LowNormVel) {
				return -1.0f;
			}

			// ball position
			var ballX = ball.Position.x;
			var ballY = ball.Position.y;

			// ball normal contact distance distance normal to segment. lateral contact subtract the ball radius
			var rollingRadius = lateral ? ball.Radius : PhysicsConstants.ToleranceRadius; // lateral or rolling point
			var bcpd = (ballX - coll.V1.x) * coll.Normal.x + (ballY - coll.V1.y) * coll.Normal.y; // ball center to plane distance
			var bnd = bcpd - rollingRadius;

			if (coll.ItemType == ItemType.Spinner || coll.ItemType == ItemType.Gate) {
				bnd = bcpd + rollingRadius;
			}

			var inside = bnd <= 0; // in ball inside object volume
			float hitTime;
			if (rigid) {
				if (bnd < -ball.Radius || lateral && bcpd < 0) {
					// (ball normal distance) excessive penetration of object skin ... no collision HACK
					return -1.0f;
				}

				if (lateral && bnd <= PhysicsConstants.PhysTouch) {
					if (inside
					    || math.abs(bnv) > PhysicsConstants.ContactVel // fast velocity, return zero time
					    || bnd <= -PhysicsConstants.PhysTouch) {
						// zero time for rigid fast  bodies
						hitTime = 0; // slow moving but embedded

					} else {
						hitTime = bnd * (float)(1.0 / (2.0 * PhysicsConstants.PhysTouch)) + 0.5f; // don't compete for fast zero time events
					}

				} else if (math.abs(bnv) > PhysicsConstants.LowNormVel) {
					// not velocity low ????
					hitTime = bnd / -bnv; // rate ok for safe divide
				} else {
					return -1.0f; // wait for touching
				}

			} else {
				//non-rigid ... target hits
				if (bnv * bnd >= 0) {

					if (coll.ItemType != ItemType.Trigger               // not a trigger
					    /*todo   || !ball.m_vpVolObjs*/
					    // it's a trigger, so test:
					    || math.abs(bnd) >= ball.Radius * 0.5f          // not too close ... nor too far away
					    || inside == insideOfs.IsInsideOf(coll.ItemId, ball.Id))   // ...ball outside and hit set or ball inside and no hit set
					{
						return -1.0f;
					}

					hitTime = 0;
					isUnHit = !inside; // ball on outside is UnHit, otherwise it's a Hit

				} else {
					hitTime = bnd / -bnv;
				}
			}

			if (float.IsNaN(hitTime) || float.IsInfinity(hitTime) || hitTime < 0 || hitTime > dTime) {
				return -1.0f; // time is outside this frame ... no collision
			}

			var btv = ballVx * coll.Normal.y - ballVy * coll.Normal.x; // ball velocity tangent to segment with respect to direction from _v1 to _v2
			var btd = (ballX - coll.V1.x) * coll.Normal.y
			             - (ballY - coll.V1.y) * coll.Normal.x    // ball tangent distance
			             + btv * hitTime;                 // ball tangent distance (projection) (initial position + velocity * hitime)

			if (btd < -PhysicsConstants.ToleranceEndPoints || btd > coll._length + PhysicsConstants.ToleranceEndPoints) {
				// is the contact off the line segment???
				return -1.0f;
			}

			if (!rigid) {
				collEvent.HitFlag = isUnHit; // UnHit signal is receding from outside target
			}

			var ballRadius = ball.Radius;
			var hitZ = ball.Position.z + ball.Velocity.z * hitTime; // check too high or low relative to ball rolling point at hittime

			if (hitZ + ballRadius * 0.5 < coll.ZLow // check limits of object"s height and depth
			    || hitZ - ballRadius * 0.5 > coll.ZHigh) {
				return -1.0f;
			}

			// hit normal is same as line segment normal
			collEvent.HitNormal.x = coll.Normal.x;
			collEvent.HitNormal.y = coll.Normal.y;
			collEvent.HitNormal.z = 0f;
			collEvent.HitDistance = bnd; // actual contact distance ...

			// check for contact
			collEvent.IsContact = math.abs(bnv) <= PhysicsConstants.ContactVel &&
			                      math.abs(bnd) <= PhysicsConstants.PhysTouch;
			if (collEvent.IsContact) {
				collEvent.HitOrgNormalVelocity = bnv;
			}
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

		#region Transformation

		public static bool IsTransformable(float4x4 matrix)
		{
			// position: fully transformable: 3d (center + ZLow)
			// scale: fully scalable
			// rotation: can be z-rotated, x/y rotation is not supported.

			var rotation = matrix.GetRotationVector();
			var xyRotated = math.abs(rotation.x) > Collider.Tolerance || math.abs(rotation.y) > Collider.Tolerance;

			return !xyRotated;
		}

		public LineCollider Transform(float4x4 matrix)
		{
			Transform(this, matrix);
			return this;
		}

		public void Transform(LineCollider lineCollider, float4x4 matrix)
		{
			#if UNITY_EDITOR
			if (!IsTransformable(matrix)) {
				throw new System.InvalidOperationException($"Matrix {matrix} cannot transform line collider.");
			}
			#endif

			var s = matrix.GetScale();
			var t = matrix.GetTranslation();
			V1 = matrix.MultiplyPoint(new float3(lineCollider.V1, 0)).xy;
			V2 = matrix.MultiplyPoint(new float3(lineCollider.V2, 0)).xy;
			ZHigh = t.z + lineCollider.ZHigh * s.z;
			ZLow = t.z + lineCollider.ZLow * s.z;

			CalcNormal();
			CalculateBounds();
		}

		public Aabb GetTransformedAabb(float4x4 matrix)
		{
			var p1 = matrix.MultiplyPoint(new float3(V1, ZLow));
			var p2 = matrix.MultiplyPoint(new float3(V1, ZHigh));
			var p3 = matrix.MultiplyPoint(new float3(V2, ZLow));
			var p4 = matrix.MultiplyPoint(new float3(V2, ZHigh));

			var min = math.min(p1, math.min(p2, math.min(p3, p4)));
			var max = math.max(p1, math.max(p2, math.max(p3, p4)));

			return new Aabb(min, max);
		}

		public LineCollider TransformAabb(float4x4 matrix)
		{
			Bounds = new ColliderBounds(Header.ItemId, Header.Id, GetTransformedAabb(matrix));
			return this;
		}

		#endregion

		private void CalculateBounds()
		{
			// TransformAabb() takes in a matrix, this is faster if the matrix has been applied to the collider already.
			Bounds = new ColliderBounds(Header.ItemId, Header.Id, new Aabb(
				math.min(V1.x, V2.x),
				math.max(V1.x, V2.x),
				math.min(V1.y, V2.y),
				math.max(V1.y, V2.y),
				ZLow,
				ZHigh
			));
		}
	}
}
