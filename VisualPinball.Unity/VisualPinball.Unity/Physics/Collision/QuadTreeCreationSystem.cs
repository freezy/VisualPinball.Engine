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
	internal static class QuadTreeCreationSystem
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private static readonly ProfilerMarker PerfMarkerTotal = new ProfilerMarker("QuadTreeCreationSystem");
		private static readonly ProfilerMarker PerfMarkerGenerateColliders = new ProfilerMarker("QuadTreeCreationSystem (1 - generate colliders)");
		private static readonly ProfilerMarker PerfMarkerCreateBlobAsset = new ProfilerMarker("QuadTreeCreationSystem (2 - create blob asset)");
		private static readonly ProfilerMarker PerfMarkerCreateQuadTree = new ProfilerMarker("QuadTreeCreationSystem (3 - create quad tree)");
		private static readonly ProfilerMarker PerfMarkerSaveToEntity = new ProfilerMarker("QuadTreeCreationSystem (4 - save to entity)");
		private static readonly ProfilerMarker PerfMarkerCreateColliders = new ProfilerMarker("IColliderGenerator.CreateColliders");

		private static readonly ProfilerMarker PerfMarkerTotal = new ProfilerMarker("QuadTreeCreationSystem");
		private static readonly ProfilerMarker PerfMarkerInitItems = new ProfilerMarker("QuadTreeCreationSystem (1 - init items)");
		private static readonly ProfilerMarker PerfMarkerGenerateColliders = new ProfilerMarker("QuadTreeCreationSystem (2 - generate colliders)");
		private static readonly ProfilerMarker PerfMarkerCreateQuadTree = new ProfilerMarker("QuadTreeCreationSystem (3 - create quad tree)");
		private static readonly ProfilerMarker PerfMarkerAllocate = new ProfilerMarker("QuadTreeCreationSystem (4 - allocate)");
		private static readonly ProfilerMarker PerfMarkerSaveToEntity = new ProfilerMarker("QuadTreeCreationSystem (5 - save to entity)");

		public static void Create(EntityManager entityManager)
		{
			PerfMarkerTotal.Begin();

			var player = Object.FindObjectOfType<Player>();
			var itemApis = player.ColliderGenerators.ToArray();

			// 1. generate colliders
			PerfMarkerGenerateColliders.Begin();
			var colliderList = new List<ICollider>();
			var (playfieldCollider, glassCollider) = player.TableApi.CreateColliders(player.Table);
			foreach (var itemApi in itemApis) {
				var cnt = colliderList.Count;
				PerfMarkerCreateColliders.Begin();
				itemApi.CreateColliders(player.Table, colliderList);
				PerfMarkerCreateColliders.End();
			}
			PerfMarkerGenerateColliders.End();

			// 2. allocate created colliders
			var allocateColliderJob = new ColliderAllocationJob(colliderList, playfieldCollider, glassCollider);
			allocateColliderJob.Run();

			// retrieve result and dispose
			var colliderBlobAssetRef = allocateColliderJob.BlobAsset[0];
			allocateColliderJob.Dispose();
			PerfMarkerCreateBlobAsset.End();

			// 3. Create quadtree blob (BlobAssetReference<QuadTreeBlob>) from AABBs
			PerfMarkerCreateQuadTree.Begin();
			BlobAssetReference<QuadTreeBlob> quadTreeBlobAssetRef;
			using (var builder = new BlobBuilder(Allocator.Temp)) {
				ref var rootQuadTree = ref builder.ConstructRoot<QuadTreeBlob>();
				QuadTree.Create(player, builder, ref colliderBlobAssetRef.Value.Colliders, ref rootQuadTree.QuadTree,
					player.Table.BoundingBox.ToAabb());

				quadTreeBlobAssetRef = builder.CreateBlobAssetReference<QuadTreeBlob>(Allocator.Persistent);
			}
			PerfMarkerCreateQuadTree.End();

			// save it to entity
			PerfMarkerSaveToEntity.Begin();
			var collEntity = entityManager.CreateEntity(ComponentType.ReadOnly<QuadTreeData>(), ComponentType.ReadOnly<ColliderData>());
			entityManager.SetComponentData(collEntity, new QuadTreeData { Value = quadTreeBlobAssetRef });
			entityManager.SetComponentData(collEntity, new ColliderData { Value = colliderBlobAssetRef });
			PerfMarkerSaveToEntity.End();

			Logger.Info("Static QuadTree initialized.");

			PerfMarkerTotal.End();
		}
	}
}
