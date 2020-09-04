// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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

using Unity.Entities;
using Unity.Mathematics;

namespace VisualPinball.Unity
{
	// todo split this into at least 2 components
	internal struct BallData : IComponentData
	{
		public int Id;
		public float3 Position;
		public float3 EventPosition; // m_lastEventPos
		public float3 Velocity;
		public float3 AngularVelocity;
		public float3 AngularMomentum;
		public float3x3 Orientation;
		public float Radius;
		public float Mass;
		public bool IsFrozen;
		public int RingCounterOldPos;

		public float3 OldVelocity;

		public Aabb Aabb {
			get {
				var vl = math.length(Velocity) + Radius + 0.05f; // 0.05f = paranoia
				return new Aabb(
					-1,
					Position.x - vl,
					Position.x + vl,
					Position.y - vl,
					Position.y + vl,
					Position.z - vl,
					Position.z + vl
				);
			}
		}

		public Aabb GetAabb(Entity entity) {
			var vl = math.length(Velocity) + Radius + 0.05f; // 0.05f = paranoia
			return new Aabb(
				entity,
				Position.x - vl,
				Position.x + vl,
				Position.y - vl,
				Position.y + vl,
				Position.z - vl,
				Position.z + vl
			);
		}

		public float CollisionRadiusSqr {
			get {
				var v1 = math.length(Velocity) + Radius + 0.05f;
				return v1 * v1;
			}
		}

		public float Inertia => 2.0f / 5.0f * Radius * Radius * Mass;
		public float InvMass => 1f / Mass;

		public void ApplySurfaceImpulse(in float3 rotI, in float3 impulse)
		{
			Velocity += impulse / Mass;
			AngularMomentum += rotI;
		}

		public static float3 SurfaceVelocity(in BallData ball, in float3 surfP)
		{
			// linear velocity plus tangential velocity due to rotation
			return ball.Velocity + math.cross(ball.AngularVelocity, surfP);
		}

		public static float3 SurfaceAcceleration(in BallData ball, in float3 surfP, in float3 gravity)
		{
			// if we had any external torque, we would have to add "(deriv. of ang.Vel.) x surfP" here
			return gravity / ball.Mass // linear acceleration
			       + math.cross(ball.AngularVelocity, math.cross(ball.AngularVelocity, surfP)); // centripetal acceleration
		}

		public static void SetOutsideOf(ref DynamicBuffer<BallInsideOfBufferElement> insideOfs, in Entity entity)
		{
			for (var i = 0; i < insideOfs.Length; i++) {
				if (insideOfs[i].Value == entity) {
					insideOfs.RemoveAt(i);
					return;
				}
			}
		}

		public static void SetInsideOf(ref DynamicBuffer<BallInsideOfBufferElement> insideOfs, Entity entity)
		{
			insideOfs.Add(new BallInsideOfBufferElement {Value = entity});
		}

		public static bool IsOutsideOf(ref DynamicBuffer<BallInsideOfBufferElement> insideOfs, in Entity entity)
		{
			return !IsInsideOf(in insideOfs, in entity);
		}

		public static bool IsInsideOf(in DynamicBuffer<BallInsideOfBufferElement> insideOfs, in Entity entity)
		{
			for (var i = 0; i < insideOfs.Length; i++) {
				if (insideOfs[i].Value == entity) {
					return true;
				}
			}
			return false;
		}

		public override string ToString()
		{
			return $"Ball{Id} ({Position.x}/{Position.y})";
		}
	}
}
