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

using System.Collections.Generic;
using System.Linq;
using NLog;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Profiling;
using UnityEngine;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	[DisableAutoCreation]
	internal class StaticBroadPhaseSystem : SystemBase
	{
		private EntityQuery _quadTreeEntityQuery;
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("StaticBroadPhaseSystem");

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private static readonly ProfilerMarker PerfMarkerTotal = new ProfilerMarker("QuadTreeCreationSystem");
		private static readonly ProfilerMarker PerfMarkerGenerateColliders = new ProfilerMarker("QuadTreeCreationSystem (1 - generate colliders)");
		private static readonly ProfilerMarker PerfMarkerCreateBlobAsset = new ProfilerMarker("QuadTreeCreationSystem (2 - create blob asset)");
		private static readonly ProfilerMarker PerfMarkerCreateQuadTree = new ProfilerMarker("QuadTreeCreationSystem (3 - create quad tree)");
		private static readonly ProfilerMarker PerfMarkerSaveToEntity = new ProfilerMarker("QuadTreeCreationSystem (4 - save to entity)");
		private static readonly ProfilerMarker PerfMarkerCreateColliders = new ProfilerMarker("IColliderGenerator.CreateColliders");

		protected override void OnCreate()
		{
			_quadTreeEntityQuery = EntityManager.CreateEntityQuery(typeof(QuadTreeData));
		}

		protected override void OnUpdate()
		{
			// retrieve reference to static quad tree data
			var collEntity = _quadTreeEntityQuery.GetSingletonEntity();
			var collData = EntityManager.GetComponentData<QuadTreeData>(collEntity);
			var marker = PerfMarker;

			Entities
				.WithName("StaticBroadPhaseJob")
				.ForEach((ref DynamicBuffer<OverlappingStaticColliderBufferElement> colliderIds, in BallData ballData) => {

				// don't play with frozen balls
				if (ballData.IsFrozen) {
					return;
				}

				marker.Begin();

				ref var quadTree = ref collData.Value.Value.QuadTree;
				colliderIds.Clear();
				quadTree.GetAabbOverlaps(in ballData, ref colliderIds);

				marker.End();

			}).Run();
		}
	}
}
