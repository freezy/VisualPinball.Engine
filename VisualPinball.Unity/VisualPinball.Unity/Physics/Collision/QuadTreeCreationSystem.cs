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
using Unity.Profiling;
using UnityEngine;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	internal static class QuadTreeCreationSystem
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private static readonly ProfilerMarker PerfMarkerTotal = new ProfilerMarker("QuadTreeCreationSystem");
		private static readonly ProfilerMarker PerfMarkerInitItems = new ProfilerMarker("QuadTreeCreationSystem (1 - init items)");
		private static readonly ProfilerMarker PerfMarkerGenerateColliders = new ProfilerMarker("QuadTreeCreationSystem (2 - generate colliders)");
		private static readonly ProfilerMarker PerfMarkerCreateQuadTree = new ProfilerMarker("QuadTreeCreationSystem (3 - create quad tree)");
		private static readonly ProfilerMarker PerfMarkerAllocate = new ProfilerMarker("QuadTreeCreationSystem (4 - allocate)");
		private static readonly ProfilerMarker PerfMarkerSaveToEntity = new ProfilerMarker("QuadTreeCreationSystem (5 - save to entity)");

		public static void Create(EntityManager entityManager)
		{
			var player = Object.FindObjectOfType<Player>();
			var itemApis = player.ColliderGenerators.ToArray();

			// 1. generate colliders
			var colliderId = 0;
			var colliderList = new List<ICollider>();
			var (playfieldCollider, glassCollider) = player.TableApi.CreateColliders(player.Table, ref colliderId);
			foreach (var itemApi in itemApis) {
				itemApi.CreateColliders(player.Table, colliderList, ref colliderId);
			}

			// 2. now we know how many there are, create a blob asset reference
			BlobAssetReference<ColliderBlob> colliderBlobAssetRef;
			using (var builder = new BlobBuilder(Allocator.TempJob)) {
				ref var root = ref builder.ConstructRoot<ColliderBlob>();
				var colliders = builder.Allocate(ref root.Colliders, colliderId);

				playfieldCollider.Allocate(builder, ref colliders);
				glassCollider.Allocate(builder, ref colliders);

				root.PlayfieldColliderId = playfieldCollider.Id;
				root.GlassColliderId = glassCollider.Id;

				// copy generated colliders into blob array
				foreach (var collider in colliderList) {
					collider.Allocate(builder, ref colliders);
				}
				colliderBlobAssetRef = builder.CreateBlobAssetReference<ColliderBlob>(Allocator.Persistent);
			}

			// 3. Create quadtree blob (BlobAssetReference<QuadTreeBlob>) from AABBs
			BlobAssetReference<QuadTreeBlob> quadTreeBlobAssetRef;
			using (var builder = new BlobBuilder(Allocator.Temp)) {
				ref var rootQuadTree = ref builder.ConstructRoot<QuadTreeBlob>();
				QuadTree.Create(player, builder, ref colliderBlobAssetRef.Value.Colliders, ref rootQuadTree.QuadTree,
					player.Table.BoundingBox.ToAabb());

				quadTreeBlobAssetRef = builder.CreateBlobAssetReference<QuadTreeBlob>(Allocator.Persistent);
			}


			// save it to entity
			var collEntity = entityManager.CreateEntity(ComponentType.ReadOnly<QuadTreeData>(), ComponentType.ReadOnly<ColliderData>());
			//DstEntityManager.SetName(collEntity, "Collision Data Holder");
			entityManager.SetComponentData(collEntity, new QuadTreeData { Value = quadTreeBlobAssetRef });
			entityManager.SetComponentData(collEntity, new ColliderData { Value = colliderBlobAssetRef });

			Logger.Info("Static QuadTree initialized.");
		}
	}
}
