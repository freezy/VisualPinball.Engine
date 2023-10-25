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

using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Profiling;

namespace VisualPinball.Unity
{
	/// <summary>
	/// This job converts a list of managed ICollider objects into unmanaged Collider structs.<br/>
	///
	/// However, the output is not a list, but a NativeArray of length of 1, containing a BlobAssetReference
	/// of ColliderBlob, which contains a BlobArray of BlobPtr of Collider.
	/// </summary>
	/// <example>
	/// <code>
	/// for (var i = 0; i &lt; BlobAsset[0].Value.Colliders.Length; i++) {
	///		var collider = BlobAsset[0].Value.Colliders[i].Value;
	/// }
	/// </code>
	/// </example>
	[BurstCompile]
	internal struct ColliderAllocationJob : IJob, IDisposable
	{
		[ReadOnly] private NativeList<CircleCollider> _circleColliders;
		[ReadOnly] private NativeList<FlipperCollider> _flipperColliders;
		[ReadOnly] private NativeList<GateCollider> _gateColliders;
		[ReadOnly] private NativeList<Line3DCollider> _line3DColliders;
		[ReadOnly] private NativeList<LineSlingshotCollider> _lineSlingshotColliders;
		[ReadOnly] private NativeList<LineCollider> _lineColliders;
		[ReadOnly] private NativeList<LineZCollider> _lineZColliders;
		[ReadOnly] private NativeList<PlungerCollider> _plungerColliders;
		[ReadOnly] private NativeList<PointCollider> _pointColliders;
		[ReadOnly] private NativeList<SpinnerCollider> _spinnerColliders;
		[ReadOnly] private NativeList<TriangleCollider> _triangleColliders;
		[ReadOnly] private NativeList<PlaneCollider> _planeColliders;

		/// <summary>
		/// The result: A blob asset of allocated Collider structs that can be casted to
		/// their respective type.
		/// </summary>
		public NativeArray<BlobAssetReference<ColliderBlob>> BlobAsset;

		public ColliderAllocationJob(ref ColliderReference colliderList) : this()
		{
			var perfMarker = new ProfilerMarker("ColliderAllocationJob.ctr");
			perfMarker.Begin();

			_circleColliders = colliderList.CircleColliders;
			_flipperColliders = colliderList.FlipperColliders;
			_gateColliders = colliderList.GateColliders;
			_line3DColliders = colliderList.Line3DColliders;
			_lineSlingshotColliders = colliderList.LineSlingshotColliders;
			_lineColliders = colliderList.LineColliders;
			_lineZColliders = colliderList.LineZColliders;
			_plungerColliders = colliderList.PlungerColliders;
			_pointColliders = colliderList.PointColliders;
			_spinnerColliders = colliderList.SpinnerColliders;
			_triangleColliders = colliderList.TriangleColliders;
			_planeColliders = colliderList.PlaneColliders;

			BlobAsset = new NativeArray<BlobAssetReference<ColliderBlob>>(1, Allocator.Persistent);

			perfMarker.End();
		}
		public void Execute()
		{
			var builder = new BlobBuilder(Allocator.Temp);
			ref var root = ref builder.ConstructRoot<ColliderBlob>();
			var count = _circleColliders.Length + _flipperColliders.Length + _gateColliders.Length + _line3DColliders.Length
			            + _lineSlingshotColliders.Length + _lineColliders.Length + _lineZColliders.Length + _plungerColliders.Length
			            + _pointColliders.Length + _spinnerColliders.Length + _triangleColliders.Length + _planeColliders.Length;

			var colliders = builder.Allocate(ref root.Colliders, count);

			// copy generated colliders into blob array
			for (var i = 0; i < _circleColliders.Length; i++) {
				_circleColliders[i].Allocate(builder, ref colliders);
			}
			for (var i = 0; i < _flipperColliders.Length; i++) {
				_flipperColliders[i].Allocate(builder, ref colliders);
			}
			for (var i = 0; i < _gateColliders.Length; i++) {
				_gateColliders[i].Allocate(builder, ref colliders);
			}
			for (var i = 0; i < _line3DColliders.Length; i++) {
				_line3DColliders[i].Allocate(builder, ref colliders);
			}
			for (var i = 0; i < _lineSlingshotColliders.Length; i++) {
				_lineSlingshotColliders[i].Allocate(builder, ref colliders);
			}
			for (var i = 0; i < _lineColliders.Length; i++) {
				_lineColliders[i].Allocate(builder, ref colliders);
			}
			for (var i = 0; i < _lineZColliders.Length; i++) {
				_lineZColliders[i].Allocate(builder, ref colliders);
			}
			for (var i = 0; i < _planeColliders.Length; i++) {
				_planeColliders[i].Allocate(builder, ref colliders);
			}
			for (var i = 0; i < _plungerColliders.Length; i++) {
				_plungerColliders[i].Allocate(builder, ref colliders);
			}
			for (var i = 0; i < _pointColliders.Length; i++) {
				_pointColliders[i].Allocate(builder, ref colliders);
			}
			for (var i = 0; i < _spinnerColliders.Length; i++) {
				_spinnerColliders[i].Allocate(builder, ref colliders);
			}
			for (var i = 0; i < _triangleColliders.Length; i++) {
				_triangleColliders[i].Allocate(builder, ref colliders);
			}

			BlobAsset[0] = builder.CreateBlobAssetReference<ColliderBlob>(Allocator.Persistent);
			builder.Dispose();
		}

		public void Dispose()
		{
			_circleColliders.Dispose();
			_flipperColliders.Dispose();
			_gateColliders.Dispose();
			_line3DColliders.Dispose();
			_lineSlingshotColliders.Dispose();
			_lineColliders.Dispose();
			_lineZColliders.Dispose();
			_planeColliders.Dispose();
			_plungerColliders.Dispose();
			_pointColliders.Dispose();
			_spinnerColliders.Dispose();
			_triangleColliders.Dispose();
		}
	}
}
