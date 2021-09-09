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
	internal static class QuadTreeCreator
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private static readonly ProfilerMarker PerfMarkerTotal = new ProfilerMarker("QuadTreeCreator");
		private static readonly ProfilerMarker PerfMarkerGenerateColliders = new ProfilerMarker("QuadTreeCreator (1 - generate colliders)");
		private static readonly ProfilerMarker PerfMarkerCreateColliders = new ProfilerMarker("IColliderGenerator.CreateColliders");
		private static readonly ProfilerMarker PerfMarkerCreateBlobAsset = new ProfilerMarker("QuadTreeCreator (2 - allocate blob asset)");
		private static readonly ProfilerMarker PerfMarkerCreateQuadTree = new ProfilerMarker("QuadTreeCreator (3 - create quad tree)");
		private static readonly ProfilerMarker PerfMarkerSaveToEntity = new ProfilerMarker("QuadTreeCreator (4 - save to entity)");

		public static void Create(EntityManager entityManager, out NativeHashMap<Entity, bool> itemsColliding)
		{
			PerfMarkerTotal.Begin();

			var player = Object.FindObjectOfType<Player>();
			var playfieldComponent = player.GetComponentInChildren<PlayfieldComponent>();
			var itemApis = player.ColliderGenerators.ToArray();

			// 1. generate colliders
			PerfMarkerGenerateColliders.Begin();
			var colliderList = new List<ICollider>();
			var (playfieldCollider, glassCollider) = player.PlayfieldApi.CreateColliders();
			itemsColliding = new NativeHashMap<Entity, bool>(itemApis.Length, Allocator.Persistent);
			foreach (var itemApi in itemApis) {
				PerfMarkerCreateColliders.Begin();
				if (itemApi.ColliderEntity != Entity.Null) {
					itemsColliding.Add(itemApi.ColliderEntity, itemApi.IsColliderEnabled);
				}
				itemApi.CreateColliders(colliderList);
				PerfMarkerCreateColliders.End();
			}
			PerfMarkerGenerateColliders.End();

			// 2. allocate created colliders
			PerfMarkerCreateBlobAsset.Begin();
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
				QuadTree.Create(builder, ref colliderBlobAssetRef.Value.Colliders, ref rootQuadTree.QuadTree,
					playfieldComponent.BoundingBox.ToAabb());

				quadTreeBlobAssetRef = builder.CreateBlobAssetReference<QuadTreeBlob>(Allocator.Persistent);
			}
			PerfMarkerCreateQuadTree.End();

			// save it to entity
			PerfMarkerSaveToEntity.Begin();
			//Debug.Log(quadTreeBlobAssetRef.Value.QuadTree.ToString(0));
			var collEntity = entityManager.CreateEntity(ComponentType.ReadOnly<QuadTreeData>(), ComponentType.ReadOnly<ColliderData>());
			entityManager.SetComponentData(collEntity, new QuadTreeData { Value = quadTreeBlobAssetRef });
			entityManager.SetComponentData(collEntity, new ColliderData { Value = colliderBlobAssetRef });
			PerfMarkerSaveToEntity.End();

			Logger.Info("Static QuadTree initialized.");

			PerfMarkerTotal.End();
		}
	}
}
