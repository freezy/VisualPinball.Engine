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

using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Engine.Common;

namespace VisualPinball.Unity
{
	internal struct PlaneCollider : ICollider
	{
		public int Id => _header.Id;
		public PhysicsMaterialData Material => _header.Material;
			

		private ColliderHeader _header;

		public readonly float3 Normal;
		public readonly float Distance;

		public ColliderBounds Bounds => new ColliderBounds(_header.ItemId, _header.Id, new Aabb(float.MinValue, float.MaxValue, float.MinValue, float.MaxValue, float.MinValue, float.MaxValue));

		public PlaneCollider(float3 normal, float distance, ColliderInfo info) : this()
		{
			_header.Init(info, ColliderType.Plane);
			Normal = normal;
			Distance = distance;
		}

		public unsafe void Allocate(BlobBuilder builder, ref BlobBuilderArray<BlobPtr<Collider>> colliders, int colliderId)
		{
			_header.Id = colliderId;
			ref var ptr = ref UnsafeUtility.As<BlobPtr<Collider>, BlobPtr<PlaneCollider>>(ref colliders[_header.Id]);
			ref var collider = ref builder.Allocate(ref ptr);
			UnsafeUtility.MemCpy(
				UnsafeUtility.AddressOf(ref collider),
				UnsafeUtility.AddressOf(ref this),
				sizeof(PlaneCollider)
			);
		}

		public override string ToString()
		{
			return $"PlaneCollider[{_header.ItemId}] {Distance} at ({Normal.x}/{Normal.y}/{Normal.z})";
		}

		#region Narrowphase

		public static float HitTest(in PlaneCollider planeColl, ref CollisionEventData collEvent, in BallData ball, float dTime)
		{
			// speed in normal direction
			var bnv = math.dot(planeColl.Normal, ball.Velocity);

			// return if clearly ball is receding from object
			if (bnv > PhysicsConstants.ContactVel) {
				return -1.0f;
			}

			// distance from plane to ball surface
			var bnd = math.dot(planeColl.Normal, ball.Position) - ball.Radius - planeColl.Distance;

			//!! solely responsible for ball through playfield?? check other places, too (radius*2??)
			if (bnd < ball.Radius * -2.0) {
				// excessive penetration of plane ... no collision HACK
				return -1.0f;
			}

			if (math.abs(bnv) <= PhysicsConstants.ContactVel) {
				if (math.abs(bnd) <= PhysicsConstants.PhysTouch) {
					collEvent.IsContact = true;
					collEvent.HitNormal = planeColl.Normal;
					collEvent.HitOrgNormalVelocity = bnv; // remember original normal velocity
					collEvent.HitDistance = bnd;

					// hit time is ignored for contacts
					return 0.0f;
				}

				// large distance, small velocity -> no hit
				return -1.0f;
			}

			var hitTime = bnd / -bnv;

			// already penetrating? then collide immediately
			if (hitTime < 0) {
				hitTime = 0.0f;
			}

			// time is outside this frame ... no collision
			if (float.IsNaN(hitTime) || float.IsInfinity(hitTime) || hitTime < 0 || hitTime > dTime) {
				return -1.0f;
			}

			collEvent.HitNormal = planeColl.Normal;
			collEvent.HitDistance = bnd; // actual contact distance

			return hitTime;
		}

		public float HitTest(ref CollisionEventData collEvent, in BallData ball, float dTime) => HitTest(this, ref collEvent, ball, dTime);

		#endregion

		#region Collision

		public static void Collide(in PlaneCollider planeColl, ref BallData ball, in CollisionEventData collEvent, ref Random random)
		{
			BallCollider.Collide3DWall(ref ball, in planeColl._header.Material, in collEvent, in collEvent.HitNormal, ref random);

			// distance from plane to ball surface
			var bnd = math.dot(planeColl.Normal, ball.Position) - ball.Radius - planeColl.Distance;
			if (bnd < 0) {
				// if ball has penetrated, push it out of the plane
				ball.Position -= planeColl.Normal * bnd;
			}
		}

		public void Collide(ref BallData ball, in CollisionEventData collEvent, ref Random random) => Collide(this, ref ball, in collEvent, ref random);

		#endregion
	}
}
