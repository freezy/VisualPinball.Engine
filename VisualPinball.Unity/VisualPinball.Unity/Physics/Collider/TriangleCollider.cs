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
	/// <summary>
	/// A triangle in 3D space.
	/// </summary>
	///
	/// <remarks>
	/// Defined by three points (float3)
	/// </remarks>
	internal struct TriangleCollider : ICollider
	{
		/// <summary>Minimal upward normal component for a playfield triangle to count as floor.</summary>
		private const float FloorNormalMinZ = 0.7f;

		/// <summary>
		/// Barycentric slack for the triangles of a trigger's polygon. Without it, rounding can
		/// reject a point on the seam of two triangles in both, and the crossing goes unnoticed.
		/// </summary>
		private const float TriggerSeamTolerance = 1e-5f;

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

			if (Header.ItemType == ItemType.Trigger) {
				return HitTestTrigger(ref collEvent, in insideOfs, in ball, dTime);
			}

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

			if (IsInTriangle(hitPos)) {

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

		/// <summary>
		/// The non-rigid branch of VP's <c>Hit3DPoly::HitTest</c>, for the polygon of a trigger.
		/// </summary>
		///
		/// <remarks>
		/// The ball passes through a trigger's polygon. It only reports when the ball crosses it, so
		/// it never makes a contact. Otherwise, the contact solver would treat it as a floor the ball
		/// can rest on.
		/// </remarks>
		private float HitTestTrigger(ref CollisionEventData collEvent, in InsideOfs insideOfs, in BallState ball, float dTime)
		{
			var bnv = math.dot(_normal, ball.Velocity);         // speed in Normal-vector direction
			var hitPos = ball.Position - ball.Radius * _normal; // nearest point on ball ... projected radius along norm
			var bnd = math.dot(_normal, hitPos - Rgv0);         // distance from plane to ball

			var isUnHit = bnv > PhysicsConstants.LowNormVel;
			var inside = bnd <= 0;                              // ball inside object volume

			float hitTime;
			if (bnv * bnd >= 0) {                               // outside-receding || inside-approaching
				if (math.abs(bnd) >= ball.Radius * 0.5f         // not too close ... nor too far away
				    || inside != insideOfs.IsOutsideOf(Header.ItemId, ball.Id)) { // ...ball outside and hit set or ball inside and no hit set
					return -1.0f;
				}
				hitTime = 0;
				isUnHit = !inside;                              // ball on outside is UnHit, otherwise it's a Hit

			} else {
				hitTime = bnd / -bnv;
			}

			if (float.IsNaN(hitTime) || float.IsInfinity(hitTime) || hitTime < 0 || hitTime > dTime) {
				return -1.0f;                                   // time is outside this frame ... no collision
			}

			if (!IsInTriangle(hitPos + hitTime * ball.Velocity, TriggerSeamTolerance)) {
				return -1.0f;
			}

			collEvent.HitNormal = _normal;
			collEvent.HitFlag = isUnHit;                        // UnHit signal -> is receding from outside target
			collEvent.HitDistance = bnd;
			return hitTime;
		}

		private bool IsInTriangle(float3 pos, float tolerance = 0f)
		{
			// 1. Compute vectors
			var v0 = Rgv2 - Rgv0;
			var v1 = Rgv1 - Rgv0;
			var v2 = pos - Rgv0;

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
			return u >= -tolerance && v >= -tolerance && u + v <= 1 + tolerance;
		}

		#endregion

		#region Collision

		public void Collide(ref BallState ball, ref NativeQueue<EventData>.ParallelWriter hitEvents,
			in CollisionEventData collEvent, ref PhysicsState state)
		{
			var dot = -math.dot(collEvent.HitNormal, ball.Velocity);
			BallCollider.Collide3DWall(ref ball, in Header.Material, in collEvent, in _normal, ref state);

			// The playfield surface is authoritative, as VP's playfield plane is (see
			// PlaneCollider.Collide): a ball that something pressed into the floor is
			// pushed all the way out, not by the per-impact DispLimit only. Floor-like
			// triangles only, so a wall of a cutout cannot shove a ball sideways.
			if (Header.ItemType == ItemType.Playfield && _normal.z > FloorNormalMinZ) {
				var bnd = math.dot(_normal, ball.Position - Rgv0) - ball.Radius;
				if (bnd < 0) {
					ball.Position -= _normal * bnd;
				}
			}

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

		public Aabb GetTransformedAabb(float4x4 matrix)
		{
			var p1 = matrix.MultiplyPoint(Rgv0);
			var p2 = matrix.MultiplyPoint(Rgv1);
			var p3 = matrix.MultiplyPoint(Rgv2);

			var min = math.min(p1, math.min(p2, p3));
			var max = math.max(p1, math.max(p2, p3));

			return new Aabb(min, max);
		}

		public TriangleCollider TransformAabb(float4x4 matrix)
		{
			Bounds = new ColliderBounds(Header.ItemId, Header.Id, GetTransformedAabb(matrix));
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
