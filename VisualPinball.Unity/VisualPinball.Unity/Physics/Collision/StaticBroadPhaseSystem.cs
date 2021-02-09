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

		protected override void OnStartRunning()
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

			var circleColliders = new NativeList<CircleCollider>(Allocator.TempJob);
			var flipperColliders = new NativeList<FlipperCollider>(Allocator.TempJob);
			var gateColliders = new NativeList<GateCollider>(Allocator.TempJob);
			var line3dColliders = new NativeList<Line3DCollider>(Allocator.TempJob);
			var lineColliders = new NativeList<LineCollider>(Allocator.TempJob);
			var lineSlingshotColliders = new NativeList<LineSlingshotCollider>(Allocator.TempJob);
			var lineZColliders = new NativeList<LineZCollider>(Allocator.TempJob);
			var plungerColliders = new NativeList<PlungerCollider>(Allocator.TempJob);
			var pointColliders = new NativeList<PointCollider>(Allocator.TempJob);
			var spinnerColliders = new NativeList<SpinnerCollider>(Allocator.TempJob);
			var triangleColliders = new NativeList<TriangleCollider>(Allocator.TempJob);

			foreach (var collider in colliderList) {
				switch (collider) {
					case CircleCollider circleCollider: circleColliders.Add(circleCollider); break;
					case FlipperCollider flipperCollider: flipperColliders.Add(flipperCollider); break;
					case GateCollider gateCollider: gateColliders.Add(gateCollider); break;
					case Line3DCollider line3DCollider: line3dColliders.Add(line3DCollider); break;
					case LineCollider lineCollider: lineColliders.Add(lineCollider); break;
					case LineSlingshotCollider lineSlingshotCollider: lineSlingshotColliders.Add(lineSlingshotCollider); break;
					case LineZCollider lineZCollider: lineZColliders.Add(lineZCollider); break;
					case PlungerCollider plungerCollider: plungerColliders.Add(plungerCollider); break;
					case PointCollider pointCollider: pointColliders.Add(pointCollider); break;
					case SpinnerCollider spinnerCollider: spinnerColliders.Add(spinnerCollider); break;
					case TriangleCollider triangleCollider: triangleColliders.Add(triangleCollider); break;
				}
			}

			// 2. now we know how many there are, create a blob asset reference
			PerfMarkerCreateBlobAsset.Begin();
			var planeColliders = new NativeArray<PlaneCollider>(2, Allocator.TempJob) {
				[0] = playfieldCollider,
				[1] = glassCollider
			};
			var allocateColliderJob = new ColliderAllocationJob(circleColliders, flipperColliders, gateColliders, line3dColliders,
				lineSlingshotColliders, lineColliders, lineZColliders, plungerColliders, pointColliders, spinnerColliders,
				triangleColliders, planeColliders);
			allocateColliderJob.Run();

			var colliderBlobAssetRef = allocateColliderJob.BlobAsset[0];
			allocateColliderJob.BlobAsset.Dispose();

			circleColliders.Dispose();
			flipperColliders.Dispose();
			gateColliders.Dispose();
			line3dColliders.Dispose();
			lineColliders.Dispose();
			lineSlingshotColliders.Dispose();
			lineZColliders.Dispose();
			plungerColliders.Dispose();
			pointColliders.Dispose();
			spinnerColliders.Dispose();
			triangleColliders.Dispose();

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
			var collEntity = EntityManager.CreateEntity(ComponentType.ReadOnly<QuadTreeData>(), ComponentType.ReadOnly<ColliderData>());
			EntityManager.SetComponentData(collEntity, new QuadTreeData { Value = quadTreeBlobAssetRef });
			EntityManager.SetComponentData(collEntity, new ColliderData { Value = colliderBlobAssetRef });
			PerfMarkerSaveToEntity.End();

			Logger.Info("Static QuadTree initialized.");

			PerfMarkerTotal.End();
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
