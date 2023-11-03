﻿// Visual Pinball Engine
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
	public static class PhysicsKinematicBroadPhase
	{
		private static readonly ProfilerMarker PerfMarkerBallOctree = new("CreateKinematicOctree");

		internal static NativeOctree<int> CreateOctree(ref NativeColliders kinematicColliders, in AABB playfieldBounds)
		{
			PerfMarkerBallOctree.Begin();
			var octree = new NativeOctree<int>(playfieldBounds, 1024, 10, Allocator.TempJob);

			for (var i = 0; i < kinematicColliders.Length; i++) {
				octree.Insert(i, kinematicColliders.GetAabb(i));
			}

			PerfMarkerBallOctree.End();
			return octree;
		}
	}
}
