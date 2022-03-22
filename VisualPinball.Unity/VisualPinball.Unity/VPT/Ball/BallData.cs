// Visual Pinball Engine
// Copyright (C) 2022 freezy and VPE Team
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

		/// <summary>
		/// AngularVelocity  -  german: Winkelgeschwindigkeit

		///		* Set to 0 at Manual Roll
		///			(in BallManualRoll(in Entity entity, in float3 targetWorldPosition)
		///			(which is not used anywhere in this Project, but is at least used in Ravarcade's ImGui Physics Debugger - Addon)
		///		* Is set to zero At RotatorComponent. Possibly an error and should be AngularVelocity 
		///			(in UpdateRotation(float angleDeg))
		///		* Calculated from AngularMomentum / inertia 
		///			(In BallDisplacementSystem.OnUpdate())
		///			(Where Inertia is a "constant" based on radius and mass (2/5 m r^2))
		///		* Used to get tangential velocity due to rotation when rolling / colliding on surfaces (alsways added to normal velocity) 
		///			(in BallData.SurfaceVelocity(in BallData ball, in float3 surfP))
		/// </summary>
		public float3 AngularVelocity;
		/// <summary>
		/// AngularMomentum  - german: drehimpuls, Impulsmomemt
		///		* Set to 0 at Manual Roll
		///			(in BallManualRoll(in Entity entity, in float3 targetWorldPosition)
		///		* Set to 0 at every new ball	
		///			(in Ballmanager.CreateEntity(GameObject ballGo, int id, in float3 worldPos, in float3 localPos, in float3 localVel, in float scale, in float mass, in float radius, in Entity kickerEntity)
		///		* Set to 0 in KickerApi, KickerCollider and RotatorComponent
		///			(in several places)
		///		* Calculated when a survace applies an impulse, it applies it to velocity (div by mass) and to angMom fully. 
		///			(ApplySurfaceImpulse(in float3 rotI, in float3 impulse))
		///			(Where rotI seems to be the Rotation impulse and impulse is the (non angular)velocity (makes sense to divide by mass)
		///			(angularMomenmtom = rotI;)
		///		* used to calculate Angular Velocity (ball.AngularVelocity = ball.AngularMomentum / inertia;)
		///			(in BalldisplacementSystem.OnUpdate())
		///		* used to add and thus calculate Orientation 
		///			(in BalldisplacementSystem.OnUpdate())
		///			skewSymmetricMatrix is created from the Angular Momentum divided by Inertia
		///			The original orientation is multiplied with the skewSymmetric Matrix
		///			and added to the old Orientation to form new orientation
		///			
		/// </summary>
		public float3 AngularMomentum;
		public float3x3 Orientation;
		public float Radius;
		public float Mass;
		public bool IsFrozen;
		public int RingCounterOldPos;

		public bool ManualControl;
		public float2 ManualPosition;

		public float3 OldVelocity;

		public Aabb Aabb {
			get {
				var vl = math.length(Velocity) + Radius + 0.05f; // 0.05f = paranoia
				return new Aabb(
					Position.x - vl,
					Position.x + vl,
					Position.y - vl,
					Position.y + vl,
					Position.z - vl,
					Position.z + vl
				);
			}
		}

		public BallColliderBounds Bounds(Entity entity) {
			var vl = math.length(Velocity) + Radius + 0.05f; // 0.05f = paranoia
			return new BallColliderBounds(entity, new Aabb(
				Position.x - vl,
				Position.x + vl,
				Position.y - vl,
				Position.y + vl,
				Position.z - vl,
				Position.z + vl
			));
		}

		public float CollisionRadiusSqr {
			get {
				var v1 = math.length(Velocity) + Radius + 0.05f;
				return v1 * v1;
			}
		}

		/// <summary>
		/// Calculates Moment of Inertia for a Ball 
		/// https://en.wikipedia.org/wiki/Moment_of_inertia#Examples_2
		/// </summary>
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

		public static bool IsOutsideOf(in DynamicBuffer<BallInsideOfBufferElement> insideOfs, in Entity entity)
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
