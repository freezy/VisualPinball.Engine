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
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using VisualPinball.Engine.Common;

namespace VisualPinball.Unity
{
	public static class PhysicsStaticBroadPhase
	{
		private static readonly ProfilerMarker PerfMarkerBroadPhase = new("BroadPhase");

		/// <summary>
		/// Collects the colliders whose bounds the ball can reach within
		/// <paramref name="dTime"/> (see <see cref="BallState.GetSweptAabb"/>).
		/// </summary>
		internal static void FindOverlaps(in NativeOctree<int> octree, in BallState ball, ref NativeParallelHashSet<int> overlappingColliders, float dTime)
		{
			PerfMarkerBroadPhase.Begin();
			overlappingColliders.Clear();
			octree.RangeAABBUnique(ball.GetSweptAabb(dTime), overlappingColliders);
			PerfMarkerBroadPhase.End();
		}

		/// <summary>
		/// Same as <see cref="FindOverlaps(in NativeOctree{int}, in BallState, ref NativeParallelHashSet{int}, float)"/>,
		/// counting the octree objects visited (bounds tests) for diagnostics.
		/// </summary>
		internal static void FindOverlaps(in NativeOctree<int> octree, in BallState ball, ref NativeParallelHashSet<int> overlappingColliders, float dTime, ref PhysicsCounters counters)
		{
			PerfMarkerBroadPhase.Begin();
			overlappingColliders.Clear();
			var visitor = new CountingRangeVisitor { Results = overlappingColliders };
			octree.Range(ball.GetSweptAabb(dTime), ref visitor);
			counters.BroadPhaseVisits += visitor.Visits;
			PerfMarkerBroadPhase.End();
		}

		private struct CountingRangeVisitor : IOctreeRangeVisitor<int>
		{
			public NativeParallelHashSet<int> Results;
			public int Visits;

			public bool OnVisit(int obj, AABB objBounds, AABB queryRange)
			{
				Visits++;
				if (objBounds.Overlaps(queryRange)) {
					Results.Add(obj);
				}
				return true;
			}
		}

		/// <summary>
		/// Adds the colliders of moving kinematic items that overlap the ball's bounds.
		/// Moving items are excluded from the kinematic octree (see
		/// <see cref="PhysicsState.KinematicItemsOutOfOctree"/>) so the octree does not
		/// have to be rebuilt on every pose update; instead their colliders' current
		/// bounds are tested here directly, after a one-test rejection against the
		/// item's overall bounds. Must run after <see cref="FindOverlaps"/> for the
		/// kinematic octree, which clears the set.
		/// </summary>
		/// <remarks>
		/// The kinematic narrow phase hit-tests in the collider's frame, i.e. with the
		/// ball's velocity relative to the item's surface velocity at the ball position,
		/// so a surface can hit a ball within the tick even though its pose is fixed for
		/// the tick. The ball's bounds are therefore inflated, per axis, by the distance
		/// that surface velocity covers in one tick, which is what the swept octree
		/// bounds used to provide for these items.
		/// </remarks>
		internal static void FindMovingKinematicOverlaps(ref PhysicsState state, in BallState ball, ref NativeParallelHashSet<int> overlappingColliders, float dTime)
		{
			if (!state.KinematicItemsOutOfOctree.IsCreated || state.KinematicItemsOutOfOctree.IsEmpty) {
				return;
			}
			PerfMarkerBroadPhase.Begin();
			var ballAabb = ball.GetSweptAabb(dTime);
			using var items = state.KinematicItemsOutOfOctree.GetEnumerator();
			while (items.MoveNext()) {
				var itemId = items.Current;
				if (!state.KinematicColliderLookups.TryGetValue(itemId, out var colliderIds)) {
					continue;
				}

				// same relative velocity the narrow phase subtracts, over one tick
				var query = ballAabb;
				if (state.TryGetKinematicVelocity(itemId, out var linear, out var angular, out var pivot)) {
					var surfaceVelocity = linear + math.cross(angular, ball.Position - pivot);
					query = Inflate(in ballAabb, math.abs(surfaceVelocity) * PhysicsConstants.PhysFactor);
				}

				if (state.KinematicMovingItemBounds.IsCreated
				    && state.KinematicMovingItemBounds.TryGetValue(itemId, out var itemBounds)
				    && !itemBounds.IntersectRect(query)) {
					continue;
				}
				for (var i = 0; i < colliderIds.Length; i++) {
					var colliderId = colliderIds[i];
					if (state.GetKinematicColliderAabb(colliderId).IntersectRect(query)) {
						overlappingColliders.Add(colliderId);
					}
				}
			}
			PerfMarkerBroadPhase.End();
		}

		private static Aabb Inflate(in Aabb aabb, in float3 margin)
		{
			return new Aabb(aabb.Left - margin.x, aabb.Right + margin.x, aabb.Top - margin.y, aabb.Bottom + margin.y,
				aabb.ZLow - margin.z, aabb.ZHigh + margin.z);
		}
	}
}
