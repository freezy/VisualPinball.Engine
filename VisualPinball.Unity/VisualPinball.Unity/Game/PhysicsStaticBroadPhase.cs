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

namespace VisualPinball.Unity
{
	public static class PhysicsStaticBroadPhase
	{
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("StaticBroadPhase");
		
		internal static void FindOverlaps(in NativeOctree<Collider> octree, in BallData ball, ref NativeList<Collider> overlappingColliders)
		{
			PerfMarker.Begin();
			overlappingColliders.Clear();
			octree.RangeAABB(ball.Aabb, overlappingColliders);
			PerfMarker.End();
		}
	}
}
