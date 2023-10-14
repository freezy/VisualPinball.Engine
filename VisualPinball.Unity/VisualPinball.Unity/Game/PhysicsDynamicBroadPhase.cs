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
using VisualPinball.Unity.Collections;

namespace VisualPinball.Unity
{
	public static class PhysicsDynamicBroadPhase
	{
		internal static NativeOctree<int> CreateOctree(ref NativeParallelHashMap<int, BallData> balls, in AABB playfieldBounds)
		{
			var octree = new NativeOctree<int>(playfieldBounds, 16, 10, Allocator.TempJob);
			using var enumerator = balls.GetEnumerator();
			while (enumerator.MoveNext()) {
				ref var ball = ref enumerator.Current.Value;
				octree.Insert(ball.Id, ball.Aabb);
			}
			return octree;
		}

		internal static void FindOverlaps(in NativeOctree<int> octree, in BallData ball, ref NativeParallelHashSet<int> overlappingBalls, ref NativeParallelHashMap<int, BallData> balls)
		{
			overlappingBalls.Clear();
			octree.RangeAABBUnique(ball.Aabb, overlappingBalls);
			using var ob = overlappingBalls.ToNativeArray(Allocator.Temp);
			for (var i = 0; i < ob.Length; i ++) {
				var overlappingBallId = ob[i];
				ref var overlappingBall = ref balls.GetValueByRef(overlappingBallId);
				if (overlappingBallId == ball.Id || overlappingBall.IsFrozen) {
					overlappingBalls.Remove(overlappingBallId);
				}
			}
		}
	}
}
