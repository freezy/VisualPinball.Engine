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

using Unity.Mathematics;

namespace VisualPinball.Unity
{
	internal struct BallState
	{
		public int Id;
		public float3 Position;
		public float3 EventPosition; // m_lastEventPos
		public float3 Velocity;

		/// <summary>
		/// AngularMomentum  - german: drehimpuls, Impulsmomemt
		///		* Set to 0 at Manual Roll
		///			(in BallManualRoll(in Entity entity, in float3 targetWorldPosition)
		///		* Set to 0 at every new ball
		///			(in Ballmanager.CreateEntity(GameObject ballGo, int id, in float3 worldPos, in float3 localPos, in float3 localVel, in float scale, in float mass, in float radius, in Entity kickerEntity)
		///		* Set to 0 in KickerApi, KickerCollider and RotatorComponent
		///			(in several places)
		///		* Calculated when a survace applies an impulse, it applies it to angMom fully.
		///			(ApplySurfaceImpulse(in float3 rotI, in float3 impulse))
		///			(Where rotI seems to be the Rotation impulse and impulse is the (non angular)velocity (makes sense to divide by mass)
		///			(angularMomenmtom = rotI;)
		///		* used to add and thus calculate Orientation
		///			(in BalldisplacementSystem.OnUpdate())
		///			skewSymmetricMatrix is created from the Angular Momentum divided by Inertia
		///			The original orientation is multiplied with the skewSymmetric Matrix
		///			and added to the old Orientation to form new orientation
		///
		/// </summary>
		public float3 AngularMomentum;

		public float3x3 BallOrientation;
		public float3x3 BallOrientationForUnity;
		public float Radius;
		public float Mass;
		public bool IsFrozen;
		public int RingCounterOldPos;

		public bool ManualControl;
		public float2 ManualPosition;

		public float3 OldVelocity;

		public CollisionEventData CollisionEvent;

		public BallPositions LastPositions;

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

		public static float3 SurfaceVelocity(in BallState ball, in float3 surfP)
		{
			// linear velocity plus tangential velocity due to rotation
			return ball.Velocity + math.cross(ball.AngularMomentum / ball.Inertia, surfP);
			/*
			This was (from freezy's first implementation):
				return ball.Velocity + math.cross(ball.AngularVelocity, surfP);
				(angular velocity should not be used to calculate the surface velocity, since angVel is the imapct of the ball from a collider, not the angular "speed" of a ball.
				Only after all collision-calculations angVel is set to AngMom / inertia)

			Original code from VPX
				Vertex3Ds Ball::SurfaceVelocity(const Vertex3Ds& surfP) const
				{
				return m_d.m_vel + CrossProduct(m_angularmomentum / Inertia(), surfP); // linear velocity plus tangential velocity due to rotation
				}
			*/
		}

		public static float3 SurfaceAcceleration(in BallState ball, in float3 surfP, in float3 gravity)
		{
			var currentAngularVelocity = ball.AngularMomentum / ball.Inertia;

			// if we had any external torque, we would have to add "(deriv. of ang.Vel.) x surfP" here
			return gravity / ball.Mass // linear acceleration
				+ math.cross(currentAngularVelocity, math.cross(currentAngularVelocity, surfP)); // centripetal acceleration

			/* This was  (from freezy's first implementation):
				return gravity / ball.Mass // linear acceleration
					+ math.cross(ball.angularVelocity, math.cross(ball.angularVelocity, surfP)); // centripetal acceleration
			 * Original Code:
				const Vertex3Ds angularvelocity = m_angularmomentum / Inertia();
					// if we had any external torque, we would have to add "(deriv. of ang.vel.) x surfP" here
				return g_pplayer->m_gravity/m_d.m_mass    // linear acceleration
					 + CrossProduct(angularvelocity, CrossProduct(angularvelocity, surfP)); // centripetal acceleration

				the angular velocity used here is not the angular velocity that the ball has (which is more like an angular impulse which is added to the angMom).
			*/
		}

		public void UpdateVelocities(float3 gravity) => BallVelocityPhysics.UpdateVelocities(ref this, gravity);

		public override string ToString()
		{
			return $"Ball{Id} ({Position.x}/{Position.y})";
		}

		public void Transform(float4x4 matrix)
		{
			Position = matrix.MultiplyPoint(Position);
			EventPosition = matrix.MultiplyPoint(EventPosition);
			Velocity = matrix.MultiplyVector(Velocity);
			AngularMomentum = matrix.MultiplyVector(AngularMomentum);

			//BallOrientation = math.mul(matrix, BallOrientation)
			//BallOrientationForUnity;

			OldVelocity = matrix.MultiplyVector(OldVelocity);
			//CollisionEvent.Transform(matrix);
		}
	}
}
