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
		/// Linear velocity of the transform origin, in VPX units per second, playfield space.
		/// </summary>
		internal float3 LinearVelocity;

		/// <summary>
		/// Angular velocity in radians per second, playfield space.
		/// </summary>
		internal float3 AngularVelocity;

		/// <summary>
		/// Current position of the transform origin in playfield space, i.e. the point
		/// that <see cref="LinearVelocity"/> refers to and <see cref="AngularVelocity"/>
		/// rotates around.
		/// </summary>
		internal float3 Pivot;

		/// <summary>
		/// Physics time at which the velocity was last derived.
		/// </summary>
		internal ulong LastUpdateUsec;

		internal bool IsMoving => math.lengthsq(LinearVelocity) > 1e-8f || math.lengthsq(AngularVelocity) > 1e-8f;

		/// <summary>
		/// Velocity of the item's surface at a given point, in playfield space.
		/// </summary>
		internal float3 GetVelocityAt(in float3 position) => LinearVelocity + math.cross(AngularVelocity, position - Pivot);
	}
}
