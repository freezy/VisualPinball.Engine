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

using NativeTrees;
using Unity.Mathematics;
using Unity.Profiling;
using VisualPinball.Engine.Common;
using VisualPinball.Unity.Collections;

namespace VisualPinball.Unity
{
	public static class PhysicsKinematics
	{
		private static readonly ProfilerMarker PerfMarkerTransform = new("TransformKinematicColliders");
		private static readonly ProfilerMarker PerfMarkerBallOctree = new("CreateKinematicOctree");

		/// <summary>
		/// Maximal gap between two transform updates for them to count as continuous
		/// motion. The first update after a longer pause imparts no velocity — it's a
		/// discrete re-position (editor nudge, scripted placement), not motion; velocity
		/// kicks in from the second update of a motion sequence. The window is generous
		/// enough that low-cadence updates (e.g. 10 Hz mech events) stay continuous,
		/// with the actual gap as dt, so the derived velocity is the true average.
		/// </summary>
		private const float ContinuityWindowSec = 0.25f;

		/// <summary>
		/// Per-update displacement above which the update is treated as a teleport,
		/// i.e. no velocity is imparted. Protects against scripted warps and
		/// initial placement.
		/// </summary>
		private const float TeleportDistance = 100f; // VPX units

		/// <summary>
		/// Per-update rotation above which the update is treated as a teleport.
		/// </summary>
		private const float TeleportAngle = math.PI / 3f; // 60°

		internal static void TransformFullyTransformableColliders(ref PhysicsState state)
		{
			PerfMarkerTransform.Begin();
			using var enumerator = state.UpdatedKinematicTransforms.GetEnumerator();
			while (enumerator.MoveNext()) {
				ref var matrix = ref enumerator.Current.Value;
				var itemId = enumerator.Current.Key;

				ref var colliderLookups = ref state.KinematicColliderLookups.GetValueByRef(itemId);
				for (var i = 0; i < colliderLookups.Length; i++) {
					state.TransformKinematicColliders(colliderLookups[i], matrix);
				}
			}
			PerfMarkerTransform.End();
		}

		/// <summary>
		/// Derive an item's velocity from two consecutive transform updates.
		/// </summary>
		/// <remarks>
		/// Same approach as Bullet's <c>btRigidBody::saveKinematicState</c> /
		/// <c>btTransformUtil::calculateVelocity</c>: linear velocity from the
		/// translation delta, angular velocity from the axis and angle of the
		/// delta quaternion. The time base is the physics time elapsed between
		/// the two transform applications — transforms arrive at Unity frame
		/// rate, not at tick rate, so dividing by the tick length would
		/// massively overestimate the velocity.
		/// </remarks>
		/// <param name="prev">Velocity state from the previous update (provides the previous timestamp)</param>
		/// <param name="prevMatrix">LocalToPlayfieldMatrixInVpx before this update</param>
		/// <param name="currMatrix">LocalToPlayfieldMatrixInVpx of this update</param>
		/// <param name="nowUsec">Current physics time</param>
		internal static KinematicVelocityState DeriveVelocity(in KinematicVelocityState prev, in float4x4 prevMatrix,
			in float4x4 currMatrix, ulong nowUsec)
		{
			var pivot = currMatrix.c3.xyz;

			// no time elapsed (e.g. multiple updates within the same tick): keep the current velocity
			if (nowUsec <= prev.LastUpdateUsec) {
				return new KinematicVelocityState {
					LinearVelocity = prev.LinearVelocity,
					AngularVelocity = prev.AngularVelocity,
					Pivot = pivot,
					LastUpdateUsec = prev.LastUpdateUsec,
				};
			}

			var dtSec = (nowUsec - prev.LastUpdateUsec) * 1e-6f;

			// isolated update after idle: re-baseline without imparting velocity
			if (dtSec > ContinuityWindowSec) {
				return new KinematicVelocityState { Pivot = pivot, LastUpdateUsec = nowUsec };
			}

			// IMPORTANT: physics velocities are VPX units per DefaultStepTime (10 ms) —
			// the VP convention, same time base as BallState.Velocity. Deriving per
			// second yields values 100× too large and catapults balls on contact.
			var dt = (float)((nowUsec - prev.LastUpdateUsec) / PhysicsConstants.DefaultStepTime);

			var deltaPos = pivot - prevMatrix.c3.xyz;

			var q0 = RotationOf(in prevMatrix);
			var q1 = RotationOf(in currMatrix);
			var qd = math.mul(q1, math.inverse(q0));
			if (qd.value.w < 0f) { // nearest-neighbor: q and -q are the same rotation
				qd.value = -qd.value;
			}
			var angle = 2f * math.acos(math.clamp(qd.value.w, -1f, 1f));

			// teleport guard: a jump this large in a single update imparts no velocity
			if (math.lengthsq(deltaPos) > TeleportDistance * TeleportDistance || angle > TeleportAngle) {
				return new KinematicVelocityState { Pivot = pivot, LastUpdateUsec = nowUsec };
			}

			var angVel = float3.zero;
			var axisLenSq = math.lengthsq(qd.value.xyz);
			if (angle > 1e-6f && axisLenSq > 1e-12f) {
				angVel = qd.value.xyz * math.rsqrt(axisLenSq) * (angle / dt);
			}

			return new KinematicVelocityState {
				LinearVelocity = deltaPos / dt,
				AngularVelocity = angVel,
				Pivot = pivot,
				LastUpdateUsec = nowUsec,
			};
		}

		/// <summary>
		/// Extracts the rotation of a transformation matrix, stripping scale.
		/// </summary>
		/// <remarks>
		/// Full Gram-Schmidt with normalization. Don't replace this with
		/// <c>math.orthonormalize</c>: that one assumes a near-orthonormal
		/// input, and LocalToPlayfieldMatrixInVpx matrices can carry a large
		/// uniform scale (the scene scale vs VPX units), which corrupts its
		/// result. Scale animation isn't rigid motion and is unsupported here;
		/// stripping it keeps the derived rotation exact for rigid transforms.
		/// </remarks>
		private static quaternion RotationOf(in float4x4 m)
		{
			var c0 = math.normalizesafe(m.c0.xyz, new float3(1f, 0f, 0f));
			var c1 = math.normalizesafe(m.c1.xyz - c0 * math.dot(m.c1.xyz, c0), new float3(0f, 1f, 0f));
			var c2 = math.cross(c0, c1);
			return new quaternion(new float3x3(c0, c1, c2));
		}

		/// <summary>
		/// Clear and repopulate an existing persistent kinematic octree.
		/// </summary>
		/// <remarks>
		/// The kinematic octree is allocated once with
		/// <c>Allocator.Persistent</c> and reused across frames. This
		/// method clears and re-inserts all entries, avoiding per-tick
		/// allocation overhead. It is only called when kinematic
		/// transforms have actually changed.
		/// </remarks>
		internal static void RebuildOctree(ref NativeOctree<int> octree, ref PhysicsState state)
		{
			PerfMarkerBallOctree.Begin();
			octree.Clear();

			for (var i = 0; i < state.KinematicCollidersAtIdentity.Length; i++) {
				octree.Insert(i, state.KinematicCollidersAtIdentity.GetTransformedAabb(i, ref state.KinematicTransforms));
			}

			PerfMarkerBallOctree.End();
		}
	}
}
