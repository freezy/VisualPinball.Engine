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
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.CodeGeneratedJobForEach;
using Unity.Jobs;
using Unity.Profiling;
using UnityEngine;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	[BurstCompile]
	internal struct ColliderAllocationJob : IJob
	{
		[ReadOnly] public NativeList<CircleCollider> CircleColliders;
		[ReadOnly] public NativeList<FlipperCollider> FlipperColliders;
		[ReadOnly] public NativeList<GateCollider> GateColliders;
		[ReadOnly] public NativeList<Line3DCollider> Line3DColliders;
		[ReadOnly] public NativeList<LineSlingshotCollider> LineSlingshotColliders;
		[ReadOnly] public NativeList<LineCollider> LineColliders;
		[ReadOnly] public NativeList<LineZCollider> LineZColliders;
		[ReadOnly] public NativeList<PlungerCollider> PlungerColliders;
		[ReadOnly] public NativeList<PointCollider> PointColliders;
		[ReadOnly] public NativeList<SpinnerCollider> SpinnerColliders;
		[ReadOnly] public NativeList<TriangleCollider> TriangleColliders;
		[ReadOnly] public NativeArray<PlaneCollider> PlaneColliders;

		public NativeArray<BlobAssetReference<ColliderBlob>> BlobAsset;

		public ColliderAllocationJob(NativeList<CircleCollider> circleColliders, NativeList<FlipperCollider> flipperColliders,
			NativeList<GateCollider> gateColliders, NativeList<Line3DCollider> line3DColliders,
			NativeList<LineSlingshotCollider> lineSlingshotColliders, NativeList<LineCollider> lineColliders,
			NativeList<LineZCollider> lineZColliders, NativeList<PlungerCollider> plungerColliders,
			NativeList<PointCollider> pointColliders, NativeList<SpinnerCollider> spinnerColliders,
			NativeList<TriangleCollider> triangleColliders, NativeArray<PlaneCollider> planeColliders) : this()
		{
			CircleColliders = circleColliders;
			FlipperColliders = flipperColliders;
			GateColliders = gateColliders;
			Line3DColliders = line3DColliders;
			LineSlingshotColliders = lineSlingshotColliders;
			LineColliders = lineColliders;
			LineZColliders = lineZColliders;
			PlungerColliders = plungerColliders;
			PointColliders = pointColliders;
			SpinnerColliders = spinnerColliders;
			TriangleColliders = triangleColliders;
			PlaneColliders = planeColliders;
		}
		public void Execute()
		{
			var builder = new BlobBuilder(Allocator.TempJob);
			var colliderId = 0;
			ref var root = ref builder.ConstructRoot<ColliderBlob>();
			var count = CircleColliders.Length + FlipperColliders.Length + GateColliders.Length + Line3DColliders.Length
			            + LineSlingshotColliders.Length + LineColliders.Length + LineZColliders.Length + PlungerColliders.Length
			            + PointColliders.Length + SpinnerColliders.Length + TriangleColliders.Length + PlaneColliders.Length;

			var colliders = builder.Allocate(ref root.Colliders, count);

			PlaneColliders[0].Allocate(builder, ref colliders, colliderId++);
			PlaneColliders[1].Allocate(builder, ref colliders, colliderId++);

			root.PlayfieldColliderId = PlaneColliders[0].Id;
			root.GlassColliderId = PlaneColliders[1].Id;

			// copy generated colliders into blob array
			foreach (var collider in CircleColliders) {
				collider.Allocate(builder, ref colliders, colliderId++);
			}
			foreach (var collider in FlipperColliders) {
				collider.Allocate(builder, ref colliders, colliderId++);
			}
			foreach (var collider in GateColliders) {
				collider.Allocate(builder, ref colliders, colliderId++);
			}
			foreach (var collider in Line3DColliders) {
				collider.Allocate(builder, ref colliders, colliderId++);
			}
			foreach (var collider in LineSlingshotColliders) {
				collider.Allocate(builder, ref colliders, colliderId++);
			}
			foreach (var collider in LineColliders) {
				collider.Allocate(builder, ref colliders, colliderId++);
			}
			foreach (var collider in LineZColliders) {
				collider.Allocate(builder, ref colliders, colliderId++);
			}
			foreach (var collider in PlungerColliders) {
				collider.Allocate(builder, ref colliders, colliderId++);
			}
			foreach (var collider in PointColliders) {
				collider.Allocate(builder, ref colliders, colliderId++);
			}
			foreach (var collider in SpinnerColliders) {
				collider.Allocate(builder, ref colliders, colliderId++);
			}
			foreach (var collider in TriangleColliders) {
				collider.Allocate(builder, ref colliders, colliderId++);
			}
			foreach (var collider in PlaneColliders) {
				collider.Allocate(builder, ref colliders, colliderId++);
			}

			BlobAsset[0] = builder.CreateBlobAssetReference<ColliderBlob>(Allocator.Persistent);
			builder.Dispose();
		}
	}
}
