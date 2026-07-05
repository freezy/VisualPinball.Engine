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
		/// Per-update displacement above which an update is treated as a teleport,
		/// i.e. no velocity is imparted. This only guards <i>mid-motion</i> warps —
		/// isolated jumps are already handled by the continuity window and the
		/// isolated-hold disambiguation. It must be generous: at 400 units per
		/// ~16 ms frame it corresponds to ~26,000 u/s (14 m/s) sustained motion,
		/// so even violent editor drags derive a velocity and stream instead of
		/// snap-teleporting through balls.
		/// </summary>
		private const float TeleportDistance = 400f; // VPX units

		/// <summary>
		/// Per-update rotation above which the update is treated as a teleport.
		/// </summary>
		private const float TeleportAngle = math.PI / 3f; // 60°

		/// <summary>
		/// Remaining delta up to which the pose is applied as a single jump, in VPX
		/// units (below a ball radius, with margin). This preserves the classic
		/// per-frame-jump behavior for all normal speeds: a touching ball ends up
		/// slightly embedded in the collider skin, where the contact/embedded code
		/// <i>carries</i> it gently at the surface velocity. Only larger deltas —
		/// which could skip past a ball entirely — are spread into paced sub-steps
		/// (where a pushed ball bounces harder; acceptable for rare fast movement).
		/// </summary>
		private const float MaxLinearJumpPerTick = 20f;

		/// <summary>
		/// Rotational analog of <see cref="MaxLinearJumpPerTick"/> (5° per update
		/// ≈ 300°/s at 60 fps before sub-stepping engages).
		/// </summary>
		private const float MaxAngularJumpPerTick = math.PI * 5f / 180f;

		/// <summary>
		/// Maximal collider pose movement per 1 ms tick during paced sub-stepping,
		/// in VPX units (half a ball radius). Guarantees no sub-tick step can pass
		/// a ball. This is only the anti-tunneling ceiling — the actual step size
		/// is paced at the item's own velocity, so balls feel the true surface
		/// speed, never the ceiling rate.
		/// </summary>
		private const float MaxLinearStepPerTick = 12.5f;

		/// <summary>
		/// Maximal collider pose rotation per 1 ms tick (3° — 3000°/s, far above
		/// any mech; caps the sweep of long rotating colliders per tick).
		/// </summary>
		private const float MaxAngularStepPerTick = math.PI * 3f / 180f;

		/// <summary>
		/// Pose steps are paced slightly above the item's measured velocity, so a
		/// lagging pose (frame hitches, hold resolution) converges back onto its
		/// target over a few frames instead of bursting at the anti-tunneling
		/// ceiling — a burst would hammer touching balls at the ceiling rate
		/// instead of the true surface speed.
		/// </summary>
		private const float CatchUpFactor = 1.25f;

		/// <summary>
		/// Steps every moving kinematic item's current pose toward its target pose
		/// and re-transforms its colliders. Called once per tick; a no-op for items
		/// that have reached their target.
		/// </summary>
		internal static void StepKinematics(ref PhysicsState state)
		{
			PerfMarkerTransform.Begin();
			using var enumerator = state.KinematicTargetTransforms.GetEnumerator();
			while (enumerator.MoveNext()) {
				ref var target = ref enumerator.Current.Value;
				var itemId = enumerator.Current.Key;

				ref var current = ref state.KinematicTransforms.GetValueByRef(itemId);
				var hasVelocity = state.KinematicVelocities.TryGetValue(itemId, out var velocity);
				if (current.Equals(target)) {
					// pose settled: clear the step-velocity fallback
					if (hasVelocity && (math.lengthsq(velocity.StepVelocity) > 0f || math.lengthsq(velocity.StepAngularVelocity) > 0f)) {
						velocity.StepVelocity = float3.zero;
						velocity.StepAngularVelocity = float3.zero;
						state.KinematicVelocities[itemId] = velocity;
					}
					continue;
				}

				// pace the step at the item's own speed (per tick), so touching balls
				// feel the true surface velocity; StepVelocity carries the pace across
				// the final catch-up after the derived velocity was zeroed by a stop
				var maxLinearStep = MaxLinearStepPerTick;
				var maxAngularStep = MaxAngularStepPerTick;
				if (hasVelocity) {
					var paceLin = math.max(math.length(velocity.LinearVelocity), math.length(velocity.StepVelocity))
						* CatchUpFactor * PhysicsConstants.PhysFactor;
					var paceAng = math.max(math.length(velocity.AngularVelocity), math.length(velocity.StepAngularVelocity))
						* CatchUpFactor * PhysicsConstants.PhysFactor;
					if (paceLin > 1e-6f) {
						maxLinearStep = math.min(paceLin, MaxLinearStepPerTick);
					}
					if (paceAng > 1e-8f) {
						maxAngularStep = math.min(paceAng, MaxAngularStepPerTick);
					}
				}

				// active step motion also counts as continuous: after a stop event, the
				// derived velocity is already zeroed while the pose may still be
				// catching up — the remaining gap must keep streaming (paced by
				// StepVelocity), not get mistaken for an isolated warp and snapped
				// through balls by the teleport branch of StepTowards
				var continuous = hasVelocity && (velocity.IsMoving
					|| math.lengthsq(velocity.StepVelocity) > 0f
					|| math.lengthsq(velocity.StepAngularVelocity) > 0f);

				var before = current;
				current = StepTowards(in current, in target, continuous, maxLinearStep, maxAngularStep, out var jumped);

				if (hasVelocity) {
					if (jumped) {
						// single-jump application (normal speeds) or teleport snap: the
						// derived velocity is authoritative for the ball response — the
						// jump itself must not be reported as velocity
						velocity.StepVelocity = float3.zero;
						velocity.StepAngularVelocity = float3.zero;

					} else {
						// record the actual step rate; paced at the item's velocity, this
						// stays at (or slightly above) the derived velocity, so the ball
						// response matches the true surface speed
						velocity.StepVelocity = (current.c3.xyz - before.c3.xyz) / PhysicsConstants.PhysFactor;
						var q0 = RotationOf(in before);
						var q1 = RotationOf(in current);
						var qd = math.mul(q1, math.inverse(q0));
						if (qd.value.w < 0f) {
							qd.value = -qd.value;
						}
						var stepAngle = 2f * math.acos(math.clamp(qd.value.w, -1f, 1f));
						var stepAxisLenSq = math.lengthsq(qd.value.xyz);
						velocity.StepAngularVelocity = stepAngle > 1e-6f && stepAxisLenSq > 1e-12f
							? qd.value.xyz * math.rsqrt(stepAxisLenSq) * (stepAngle / PhysicsConstants.PhysFactor)
							: float3.zero;
					}
					state.KinematicVelocities[itemId] = velocity;
				}

				if (state.KinematicColliderLookups.TryGetValue(itemId, out var colliderLookups)) {
					for (var i = 0; i < colliderLookups.Length; i++) {
						state.TransformKinematicColliders(colliderLookups[i], current);
					}
				}
			}
			PerfMarkerTransform.End();
		}

		/// <summary>
		/// Returns the pose one tick-step closer to the target. Deltas within
		/// <see cref="MaxLinearJumpPerTick"/> / <see cref="MaxAngularJumpPerTick"/>
		/// are applied as a single jump (the classic behavior, gentle embedded
		/// carry); larger ones step at the given paced limits. An <i>isolated</i>
		/// delta beyond the teleport thresholds snaps to the target directly (a warp
		/// shouldn't sweep through balls); during continuous motion, large deltas
		/// keep stepping, so fast drags stream instead of teleporting. Scale is
		/// taken from the target (scale animation is unsupported, see
		/// <see cref="RotationOf"/>).
		/// </summary>
		private static float4x4 StepTowards(in float4x4 current, in float4x4 target, bool continuous,
			float maxLinearStep, float maxAngularStep, out bool jumped)
		{
			var pC = current.c3.xyz;
			var pT = target.c3.xyz;
			var delta = pT - pC;
			var dist = math.length(delta);

			var qC = RotationOf(in current);
			var qT = RotationOf(in target);
			var qDot = math.abs(math.dot(qC.value, qT.value));
			var angle = 2f * math.acos(math.clamp(qDot, -1f, 1f));

			// within a jump-sized delta: take the target directly
			if (dist <= MaxLinearJumpPerTick && angle <= MaxAngularJumpPerTick) {
				jumped = true;
				return target;
			}

			// an isolated teleport-sized warp snaps without imparting anything
			if (!continuous && (dist > TeleportDistance || angle > TeleportAngle)) {
				jumped = true;
				return target;
			}
			jumped = false;

			var t = 1f;
			if (dist > maxLinearStep) {
				t = maxLinearStep / dist;
			}
			if (angle > maxAngularStep) {
				t = math.min(t, maxAngularStep / angle);
			}

			var p = pC + delta * t;
			var q = math.slerp(qC, qT, t);
			var scale = new float3(math.length(target.c0.xyz), math.length(target.c1.xyz), math.length(target.c2.xyz));
			var r = new float3x3(q);
			return new float4x4(
				new float4(r.c0 * scale.x, 0f),
				new float4(r.c1 * scale.y, 0f),
				new float4(r.c2 * scale.z, 0f),
				new float4(p, 1f)
			);
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
		/// <param name="isIsolated">True if this update didn't derive a velocity: first
		/// update after idle (outside the continuity window) or a teleport-sized jump.
		/// Isolated updates are applied with teleport semantics (snap, no impulse).</param>
		internal static KinematicVelocityState DeriveVelocity(in KinematicVelocityState prev, in float4x4 prevMatrix,
			in float4x4 currMatrix, ulong nowUsec, out bool isIsolated)
		{
			var pivot = currMatrix.c3.xyz;
			isIsolated = false;

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
				isIsolated = true;
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
				isIsolated = true;
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
		/// Distance and angle below which an isolated update is applied immediately
		/// as a snap. Half a ball radius: a snap this small can never critically
		/// embed a resting ball (the contact displacement correction recovers it).
		/// Larger isolated jumps are held for up to <see cref="IsolatedHoldTimeoutUsec"/>:
		/// if a follow-up update arrives, it was the first frame of continuous motion
		/// (stream it with derived velocity); if not, it was a genuine teleport (snap
		/// silently). This is what disambiguates "fast drag" from "teleport" — at the
		/// first update, they look identical.
		/// </summary>
		internal const float IsolatedHoldDistance = MaxLinearJumpPerTick;
		internal const float IsolatedHoldAngle = MaxAngularJumpPerTick;

		/// <summary>
		/// Generous enough to survive editor frame hitches — a follow-up update that
		/// arrives late must still resolve the hold as motion, not misfire a teleport
		/// through a resting ball. Well below the continuity window, so a resolved
		/// hold always derives a velocity.
		/// </summary>
		internal const ulong IsolatedHoldTimeoutUsec = 120_000;

		/// <summary>
		/// Returns whether an isolated update's delta is small enough to apply
		/// immediately (snap) instead of being held for disambiguation.
		/// </summary>
		internal static bool IsSmallIsolatedDelta(in float4x4 prevMatrix, in float4x4 currMatrix)
		{
			var dist = math.length(currMatrix.c3.xyz - prevMatrix.c3.xyz);
			var qDot = math.abs(math.dot(RotationOf(in prevMatrix).value, RotationOf(in currMatrix).value));
			var angle = 2f * math.acos(math.clamp(qDot, -1f, 1f));
			return dist <= IsolatedHoldDistance && angle <= IsolatedHoldAngle;
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

			// the sweep inflation is per item; colliders of the same item are usually
			// contiguous, so a one-entry memo avoids recomputing it per collider
			var memoItemId = -1;
			var memoAngle = 0f;
			var memoPivot = float3.zero;

			for (var i = 0; i < state.KinematicCollidersAtIdentity.Length; i++) {
				// while an item steps toward its target pose, cover the whole swept
				// range: union of the AABBs at the current and at the target pose,
				// so the octree stays valid for every sub-tick step of this frame
				var aabb = state.KinematicCollidersAtIdentity.GetTransformedAabb(i, ref state.KinematicTransforms);
				var itemId = state.KinematicCollidersAtIdentity.GetItemId(i);
				if (state.KinematicTargetTransforms.ContainsKey(itemId)) {
					var targetAabb = state.KinematicCollidersAtIdentity.GetTransformedAabb(i, ref state.KinematicTargetTransforms);
					aabb = new Aabb(
						math.min(aabb.Left, targetAabb.Left), math.max(aabb.Right, targetAabb.Right),
						math.min(aabb.Top, targetAabb.Top), math.max(aabb.Bottom, targetAabb.Bottom),
						math.min(aabb.ZLow, targetAabb.ZLow), math.max(aabb.ZHigh, targetAabb.ZHigh)
					);

					// a rotating collider can swing outside the union of its endpoint
					// boxes mid-sweep (e.g. a long thin collider rotating 45° bulges
					// out at 22.5°); inflate by the maximal arc protrusion,
					// reach × (1 - cos(angle/2)), so intermediate stepped poses stay
					// inside the broad-phase bounds.
					//
					// This bound is conservative for any shape, including long thin
					// colliders rotating about one end: every material point's two
					// endpoint positions lie inside this single (hence convex) box,
					// which therefore contains the straight chord between them; the
					// stepped path (lerped translation + slerped rotation) deviates
					// from that chord only by the rotational arc's sagitta,
					// r × (1 - cos(angle/2)) with r ≤ reach — exactly the inflation.
					// The rotate-about-one-end case attains this bound with equality.
					if (itemId != memoItemId) {
						memoItemId = itemId;
						ref var currMatrix = ref state.KinematicTransforms.GetValueByRef(itemId);
						var targetMatrix = state.KinematicTargetTransforms[itemId];
						var qDot = math.abs(math.dot(RotationOf(in currMatrix).value, RotationOf(in targetMatrix).value));
						memoAngle = 2f * math.acos(math.clamp(qDot, -1f, 1f));
						memoPivot = currMatrix.c3.xyz;
					}
					if (memoAngle > 1e-4f) {
						var dx = math.max(math.abs(aabb.Left - memoPivot.x), math.abs(aabb.Right - memoPivot.x));
						var dy = math.max(math.abs(aabb.Top - memoPivot.y), math.abs(aabb.Bottom - memoPivot.y));
						var dz = math.max(math.abs(aabb.ZLow - memoPivot.z), math.abs(aabb.ZHigh - memoPivot.z));
						var reach = math.length(new float3(dx, dy, dz));
						var inflate = reach * (1f - math.cos(memoAngle * 0.5f));
						aabb = new Aabb(
							aabb.Left - inflate, aabb.Right + inflate,
							aabb.Top - inflate, aabb.Bottom + inflate,
							aabb.ZLow - inflate, aabb.ZHigh + inflate
						);
					}
				}
				octree.Insert(i, aabb);
			}

			PerfMarkerBallOctree.End();
		}
	}
}
