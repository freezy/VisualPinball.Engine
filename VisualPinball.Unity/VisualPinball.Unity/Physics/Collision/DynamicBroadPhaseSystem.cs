﻿// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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

using Unity.Collections;
using Unity.Entities;
using Unity.Profiling;

namespace VisualPinball.Unity
{
	[DisableAutoCreation]
	internal class DynamicBroadPhaseSystem : SystemBase
	{
		private EntityQuery _ballQuery;
		private static readonly ProfilerMarker PerfMarker1 = new ProfilerMarker("DynamicBroadPhaseSystem.CreateKdTree");
		private static readonly ProfilerMarker PerfMarker2 = new ProfilerMarker("DynamicBroadPhaseSystem.GetAabbOverlaps");

		protected override void OnCreate() {
			_ballQuery = GetEntityQuery(ComponentType.ReadOnly<BallData>());
		}

		protected override void OnUpdate()
		{
			// create kdtree
			PerfMarker1.Begin();

			var ballEntities = _ballQuery.ToEntityArray(Allocator.TempJob);
			var balls = GetComponentDataFromEntity<BallData>(true);
			var kdRoot = new KdRoot();
			Job.WithCode(() => {
				var ballBounds = new NativeArray<Aabb>(ballEntities.Length, Allocator.Temp);
				for (var i = 0; i < ballEntities.Length; i++) {
					ballBounds[i] = balls[ballEntities[i]].GetAabb(ballEntities[i]);
				}
				kdRoot.Init(ballBounds, Allocator.TempJob);
			}).Run();

			ballEntities.Dispose();
			PerfMarker1.End();

			var overlappingEntities = GetBufferFromEntity<OverlappingDynamicBufferElement>();
			var marker = PerfMarker2;

			Entities
				.WithName("StaticBroadPhaseJob")
				.WithNativeDisableParallelForRestriction(overlappingEntities)
				.ForEach((Entity entity, in BallData ball) => {

					// don't play with frozen balls
					if (ball.IsFrozen) {
						return;
					}

					marker.Begin();

					var colliderEntities = overlappingEntities[entity];
					colliderEntities.Clear();
					kdRoot.GetAabbOverlaps(in entity, in ball, ref colliderEntities);

					marker.End();

				}).Run();

			kdRoot.Dispose();
		}
	}
}
