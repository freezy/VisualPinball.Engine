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
using Unity.Profiling;
using VisualPinball.Unity.Collections;

namespace VisualPinball.Unity
{
	public static class PhysicsDynamicBroadPhase
	{
		private static readonly ProfilerMarker PerfMarkerBallOctree = new("CreateBallOctree");
		private static readonly ProfilerMarker PerfMarkerDynamicBroadPhase = new("DynamicBroadPhase");

		internal static void RebuildOctree(ref NativeOctree<int> octree, ref NativeParallelHashMap<int, BallState> balls)
		{
			PerfMarkerBallOctree.Begin();
			octree.Clear();
			using var enumerator = balls.GetEnumerator();
			while (enumerator.MoveNext()) {
				ref var ball = ref enumerator.Current.Value;
				octree.Insert(ball.Id, ball.Aabb);
			}
			PerfMarkerBallOctree.End();
		}

		internal static void FindOverlaps(in NativeOctree<int> octree, in BallState ball, ref NativeParallelHashSet<int> overlappingBalls, ref NativeParallelHashMap<int, BallState> balls)
		{
			PerfMarkerDynamicBroadPhase.Begin();
			overlappingBalls.Clear();
			octree.RangeAABBUnique(ball.Aabb, overlappingBalls);

			// Collect IDs to remove into a stack-allocated list to avoid copying the hash set to a NativeArray.
			var toRemove = new FixedList64Bytes<int>();
			using var enumerator = overlappingBalls.GetEnumerator();
			while (enumerator.MoveNext()) {
				var overlappingBallId = enumerator.Current;
				ref var overlappingBall = ref balls.GetValueByRef(overlappingBallId);
				if (overlappingBallId == ball.Id || overlappingBall.IsFrozen) {
					toRemove.Add(overlappingBallId);
				}
			}
			for (var i = 0; i < toRemove.Length; i++) {
				overlappingBalls.Remove(toRemove[i]);
			}
			PerfMarkerDynamicBroadPhase.End();
		}
	}
}
