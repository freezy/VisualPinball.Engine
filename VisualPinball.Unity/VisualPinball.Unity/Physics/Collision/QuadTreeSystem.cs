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
using System.Diagnostics;
using System.Linq;
using NLog;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	[DisableAutoCreation]
	public class QuadTreeSystem : SystemBase
	{
		public NativeQuadTree<int> QuadTree;
		public NativeHashMap<Entity, bool> ItemsColliding;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private static readonly ProfilerMarker PerfMarkerTotal = new ProfilerMarker("QuadTreeCreator");
		private static readonly ProfilerMarker PerfMarkerGenerateColliders = new ProfilerMarker("QuadTreeCreator (1 - generate colliders)");
		private static readonly ProfilerMarker PerfMarkerCreateColliders = new ProfilerMarker("IColliderGenerator.CreateColliders");
		private static readonly ProfilerMarker PerfMarkerCreateBlobAsset = new ProfilerMarker("QuadTreeCreator (2 - allocate blob asset)");
		private static readonly ProfilerMarker PerfMarkerCreateQuadTree = new ProfilerMarker("QuadTreeCreator (3 - create quad tree)");
		private static readonly ProfilerMarker PerfMarkerSaveToEntity = new ProfilerMarker("QuadTreeCreator (4 - save to entity)");

		private bool _doUpdate = true;

		protected override void OnUpdate()
		{
			if (_doUpdate) {
				Create(out QuadTree, out ItemsColliding);
				_doUpdate = false;
			}
		}

		private void Create(out NativeQuadTree<int> quadTree, out NativeHashMap<Entity, bool> itemsColliding)
		{
			PerfMarkerTotal.Begin();

			var player = Object.FindObjectOfType<Player>();
			var itemApis = player.ColliderGenerators.ToArray();

			// 1. generate colliders
			PerfMarkerGenerateColliders.Begin();
			var colliderList = new List<ICollider>();
			var (playfieldCollider, glassCollider) = player.TableApi.CreateColliders(player.Table);
			itemsColliding = new NativeHashMap<Entity, bool>(itemApis.Length, Allocator.Persistent);
			foreach (var itemApi in itemApis) {
				PerfMarkerCreateColliders.Begin();
				if (itemApi.ColliderEntity != Entity.Null) {
					itemsColliding.Add(itemApi.ColliderEntity, itemApi.IsColliderEnabled);
				}
				itemApi.CreateColliders(player.Table, colliderList);
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
			var quadStart = Stopwatch.StartNew();

			var elements = new NativeArray<QuadElement<int>>(colliderBlobAssetRef.Value.Colliders.Length, Allocator.TempJob);
			for (var i = 0; i < colliderBlobAssetRef.Value.Colliders.Length; i++) {
				if (colliderBlobAssetRef.Value.Colliders[i].Value.Type != ColliderType.Plane) {
					elements[i] = new QuadElement<int> {
						bounds = colliderBlobAssetRef.Value.Colliders[i].Value.Bounds().Aabb.Bounds2D,
						element = colliderBlobAssetRef.Value.Colliders[i].Value.Bounds().ColliderId
					};
				}
			}
			var qt = new NativeQuadTree<int>(new Aabb2D(new float2(0, 0), new float2(2000, 4000)));
			Job.WithCode(() => qt.ClearAndBulkInsert(elements)).Run();

			quadTree = qt;

			//quadTree = new NativeQuadTree<int>(player.Table.BoundingBox.ToAabb().Bounds2D);
			elements.Dispose();
			Logger.Info($"Quadtree created in {quadStart.ElapsedMilliseconds}ms with {colliderBlobAssetRef.Value.Colliders.Length} elements.");
			PerfMarkerCreateQuadTree.End();

			// save it to entity
			PerfMarkerSaveToEntity.Begin();
			//Debug.Log(quadTreeBlobAssetRef.Value.QuadTree.ToString(0));
			var collEntity = EntityManager.CreateEntity(ComponentType.ReadOnly<ColliderData>());
			EntityManager.SetComponentData(collEntity, new ColliderData { Value = colliderBlobAssetRef });
			PerfMarkerSaveToEntity.End();

			Logger.Info("Static QuadTree initialized.");

			PerfMarkerTotal.End();
		}
	}
}
