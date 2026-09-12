// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
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
	/// <summary>
	/// A kinematic transform together with the Unity simulation-clock time at
	/// which it was sampled on the main thread.
	/// </summary>
	internal struct KinematicTransformSample
	{
		internal float4x4 Matrix;
		internal ulong SampleTimeUsec;
	}

	/// <summary>
	/// Records when the main thread first observed that a kinematic item had
	/// stopped moving.
	/// </summary>
	internal struct KinematicStopSample
	{
		internal int ItemId;
		internal ulong SampleTimeUsec;
	}

	/// <summary>
	/// The velocity of a kinematic item, derived from its transform updates
	/// (see <see cref="PhysicsKinematics.DeriveVelocity"/>).
	/// </summary>
	/// <remarks>
	/// Collision and contact resolution use this to compute the surface
	/// velocity of a kinematic collider at the contact point, so that a
	/// moving collider imparts momentum and friction to the ball instead of
	/// acting like a static wall that teleports between poses.
	/// </remarks>
	internal struct KinematicVelocityState
	{
		/// <summary>
		/// Linear velocity of the transform origin, in VPX units per
		/// <see cref="VisualPinball.Engine.Common.PhysicsConstants.DefaultStepTime"/>
		/// (10 ms — the VP convention, same time base as <see cref="BallState.Velocity"/>),
		/// playfield space.
		/// </summary>
		internal float3 LinearVelocity;

		/// <summary>
		/// Angular velocity in radians per
		/// <see cref="VisualPinball.Engine.Common.PhysicsConstants.DefaultStepTime"/>
		/// (10 ms), playfield space.
		/// </summary>
		internal float3 AngularVelocity;

		/// <summary>
		/// Current position of the transform origin in playfield space, i.e. the point
		/// that <see cref="LinearVelocity"/> refers to and <see cref="AngularVelocity"/>
		/// rotates around.
		/// </summary>
		internal float3 Pivot;

		/// <summary>
		/// Unity simulation-clock time at which the transform was sampled.
		/// </summary>
		internal ulong LastUpdateUsec;

		/// <summary>
		/// Simulation-clock time at which the simulation thread consumed the latest
		/// transform sample. This is deliberately separate from
		/// <see cref="LastUpdateUsec"/>, whose Unity sample clock can drift from the
		/// independently paced simulation clock.
		/// </summary>
		internal ulong LastAppliedUsec;

		/// <summary>
		/// Linear and angular speeds used to pace collider pose catch-up. These are
		/// kept separate from the actual step velocities so the catch-up factor is
		/// applied once instead of feeding back and compounding every tick.
		/// </summary>
		internal float PaceSpeed;
		internal float PaceAngularSpeed;

		/// <summary>
		/// Instantaneous velocity of the actual pose step this tick (same unit
		/// as <see cref="LinearVelocity"/>), written by
		/// <see cref="PhysicsKinematics.StepKinematics"/> — nonzero only while
		/// the pose catch-up is rate-limited (clamped stepping). During
		/// catch-up, the true surface velocity is the step rate, which can
		/// exceed the update-derived velocity; hit tests and contact response
		/// must see it, or the stepping face outruns the balls it hammers and
		/// swallows them.
		/// </summary>
		internal float3 StepVelocity;

		/// <summary>
		/// Instantaneous angular velocity of the actual pose step this tick
		/// (same unit as <see cref="AngularVelocity"/>).
		/// </summary>
		internal float3 StepAngularVelocity;

		internal bool IsMoving => math.lengthsq(LinearVelocity) > 1e-8f || math.lengthsq(AngularVelocity) > 1e-8f;

		/// <summary>
		/// Velocity of the item's surface at a given point, in playfield space.
		/// </summary>
		internal float3 GetVelocityAt(in float3 position) => LinearVelocity + math.cross(AngularVelocity, position - Pivot);
	}
}
