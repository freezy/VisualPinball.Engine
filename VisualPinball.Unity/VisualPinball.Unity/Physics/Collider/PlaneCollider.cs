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
using Unity.Profiling;
using VisualPinball.Engine.Common;

namespace VisualPinball.Unity
{
	internal struct PlaneCollider : ICollider
	{
		public int Id => _header.Id;

		private readonly ColliderHeader _header;

		private readonly float3 _normal;
		private readonly float _distance;

		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("PlaneCollider.Allocate");

		public PlaneCollider(float3 normal, float distance, ColliderInfo info) : this()
		{
			_header.Init(info, ColliderType.Plane);
			_normal = normal;
			_distance = distance;
		}

		public unsafe void Allocate(BlobBuilder builder, ref BlobBuilderArray<BlobPtr<Collider>> colliders)
		{
			PerfMarker.Begin();
			ref var ptr = ref UnsafeUtility.As<BlobPtr<Collider>, BlobPtr<PlaneCollider>>(ref colliders[_header.Id]);
			ref var collider = ref builder.Allocate(ref ptr);
			UnsafeUtility.MemCpy(
				UnsafeUtility.AddressOf(ref collider),
				UnsafeUtility.AddressOf(ref this),
				sizeof(PlaneCollider)
			);
			PerfMarker.End();
		}

		public override string ToString()
		{
			return $"PlaneCollider[{_header.Entity}] {_distance} at ({_normal.x}/{_normal.y}/{_normal.z})";
		}

		public float HitTest(ref CollisionEventData collEvent, in BallData ball, float dTime)
		{
			// speed in normal direction
			var bnv = math.dot(_normal, ball.Velocity);

			// return if clearly ball is receding from object
			if (bnv > PhysicsConstants.ContactVel) {
				return -1.0f;
			}

			// distance from plane to ball surface
			var bnd = math.dot(_normal, ball.Position) - ball.Radius - _distance;

			//!! solely responsible for ball through playfield?? check other places, too (radius*2??)
			if (bnd < ball.Radius * -2.0) {
				// excessive penetration of plane ... no collision HACK
				return -1.0f;
			}

			if (math.abs(bnv) <= PhysicsConstants.ContactVel) {
				if (math.abs(bnd) <= PhysicsConstants.PhysTouch) {
					collEvent.IsContact = true;
					collEvent.HitNormal = _normal;
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

			collEvent.HitNormal = _normal;
			collEvent.HitDistance = bnd; // actual contact distance

			return hitTime;
		}

		public void Collide(ref BallData ball, in CollisionEventData collEvent, ref Random random)
		{
			BallCollider.Collide3DWall(ref ball, in _header.Material, in collEvent, in collEvent.HitNormal, ref random);

			// distance from plane to ball surface
			var bnd = math.dot(_normal, ball.Position) - ball.Radius - _distance;
			if (bnd < 0) {
				// if ball has penetrated, push it out of the plane
				ball.Position += _normal * bnd;
			}
		}
	}
}
