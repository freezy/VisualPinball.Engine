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
using Unity.Profiling;
using VisualPinball.Unity.Collections;

namespace VisualPinball.Unity
{
	public static class PhysicsKinematics
	{
		private static readonly ProfilerMarker PerfMarkerTransform = new("TransformKinematicColliders");
		private static readonly ProfilerMarker PerfMarkerBallOctree = new("CreateKinematicOctree");

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
