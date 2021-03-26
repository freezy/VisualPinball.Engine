// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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

using NLog;
using Unity.Collections;
using Unity.Entities;
using Unity.Profiling;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	[DisableAutoCreation]
	internal class StaticBroadPhaseSystem : SystemBase
	{
		private EntityQuery _quadTreeEntityQuery;
		private QuadTreeSystem _quadTreeSystem;

		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("StaticBroadPhaseSystem");
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		protected override void OnCreate()
		{
			_quadTreeSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<QuadTreeSystem>();
		}

		protected override void OnUpdate()
		{
			// retrieve reference to static quad tree data
			var quadTree = _quadTreeSystem.QuadTree;
			var itemsColliding = _quadTreeSystem.ItemsColliding;
			var marker = PerfMarker;

			Entities
				.WithName("StaticBroadPhaseJob")
				.WithReadOnly(itemsColliding)
				.ForEach((ref DynamicBuffer<OverlappingStaticColliderBufferElement> colliderIds, in BallData ballData) => {

				// don't play with frozen balls
				if (ballData.IsFrozen) {
					return;
				}

				marker.Begin();
				var results = new NativeList<QuadElement<int>>(0, Allocator.TempJob);
				quadTree.RangeQuery(ballData.Aabb.Bounds2D, results);

				colliderIds.Clear();
				foreach (var quadElement in results) {
					colliderIds.Add(new OverlappingStaticColliderBufferElement { Value = quadElement.element });
				}
				//quadTree.GetAabbOverlaps(in ballData, in itemsColliding, ref colliderIds);

				marker.End();

			}).Run();
		}
	}
}
