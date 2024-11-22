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
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Unity.Collections;

namespace VisualPinball.Unity
{
	/// <summary>
	/// A wrapper class used to pass around colliders during collider generation.
	/// This isn't used during the physics runtime, where it's copied into NativeColliders.
	/// </summary>
	public struct ColliderReference : IDisposable
	{
		internal NativeList<CircleCollider> CircleColliders;
		internal NativeList<FlipperCollider> FlipperColliders;
		internal NativeList<GateCollider> GateColliders;
		internal NativeList<Line3DCollider> Line3DColliders;
		internal NativeList<LineSlingshotCollider> LineSlingshotColliders;
		internal NativeList<LineCollider> LineColliders;
		internal NativeList<LineZCollider> LineZColliders;
		internal NativeList<PlungerCollider> PlungerColliders;
		internal NativeList<PointCollider> PointColliders;
		internal NativeList<SpinnerCollider> SpinnerColliders;
		internal NativeList<TriangleCollider> TriangleColliders;
		internal NativeList<PlaneCollider> PlaneColliders;

		public NativeList<ColliderLookup> Lookups; // collider id -> collider type + index within collider type list

		public readonly bool KinematicColliders; // if set, populate _itemIdToColliderIds
		private NativeParallelHashMap<int, NativeList<int>> _itemIdToColliderIds;
		private NativeParallelHashMap<int, float4x4> _nonTransformableColliderMatrices;

		public ColliderReference(ref NativeParallelHashMap<int, float4x4> nonTransformableColliderMatrices, Allocator allocator, bool kinematicColliders = false)
		{
			CircleColliders = new NativeList<CircleCollider>(allocator);
			FlipperColliders = new NativeList<FlipperCollider>(allocator);
			GateColliders = new NativeList<GateCollider>(allocator);
			Line3DColliders = new NativeList<Line3DCollider>(allocator);
			LineSlingshotColliders = new NativeList<LineSlingshotCollider>(allocator);
			LineColliders = new NativeList<LineCollider>(allocator);
			LineZColliders = new NativeList<LineZCollider>(allocator);
			PlungerColliders = new NativeList<PlungerCollider>(allocator);
			PointColliders = new NativeList<PointCollider>(allocator);
			SpinnerColliders = new NativeList<SpinnerCollider>(allocator);
			TriangleColliders = new NativeList<TriangleCollider>(allocator);
			PlaneColliders = new NativeList<PlaneCollider>(allocator);

			Lookups = new NativeList<ColliderLookup>(allocator);

			KinematicColliders = kinematicColliders;
			_itemIdToColliderIds = new NativeParallelHashMap<int, NativeList<int>>(0, allocator);
			_nonTransformableColliderMatrices = nonTransformableColliderMatrices;
		}

		public void Dispose()
		{
			CircleColliders.Dispose();
			FlipperColliders.Dispose();
			GateColliders.Dispose();
			Line3DColliders.Dispose();
			LineSlingshotColliders.Dispose();
			LineColliders.Dispose();
			LineZColliders.Dispose();
			PlungerColliders.Dispose();
			PointColliders.Dispose();
			SpinnerColliders.Dispose();
			TriangleColliders.Dispose();
			PlaneColliders.Dispose();
			using (var enumerator = _itemIdToColliderIds.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					enumerator.Current.Value.Dispose();
				}
			}
			_itemIdToColliderIds.Dispose();
			Lookups.Dispose();
		}

		public int Count => Lookups.Length;

		public ICollider this[int i] => LookupCollider(i);

		public void TransformToIdentity(NativeParallelHashMap<int, float4x4> itemIdToTransformationMatrix)
		{
			using var enumerator = _itemIdToColliderIds.GetEnumerator();
			while (enumerator.MoveNext()) {
				var itemId = enumerator.Current.Key;
				ref var colliderIds = ref enumerator.Current.Value;
				foreach (var colliderId in colliderIds) {
					var matrix = itemIdToTransformationMatrix[itemId];
					var lookup = Lookups[colliderId];
					switch (lookup.Type) {
						case ColliderType.Bumper:
						case ColliderType.Circle:
							ref var circleCollider = ref CircleColliders.GetElementAsRef(lookup.Index);
							circleCollider.Transform(CircleColliders[lookup.Index], math.inverse(matrix));
							break;
						case ColliderType.Point:
							ref var pointCollider = ref PointColliders.GetElementAsRef(lookup.Index);
							pointCollider.Transform(PointColliders[lookup.Index], math.inverse(matrix));
							break;
						case ColliderType.Line3D:
							ref var line3DCollider = ref Line3DColliders.GetElementAsRef(lookup.Index);
							line3DCollider.Transform(Line3DColliders[lookup.Index], math.inverse(matrix));
							break;
						case ColliderType.Triangle:
							ref var triangleCollider = ref TriangleColliders.GetElementAsRef(lookup.Index);
							triangleCollider.Transform(TriangleColliders[lookup.Index], math.inverse(matrix));
							break;
						case ColliderType.Spinner:
							ref var spinnerCollider = ref SpinnerColliders.GetElementAsRef(lookup.Index);
							spinnerCollider.Transform(SpinnerColliders[lookup.Index], math.inverse(matrix));
							break;
						case ColliderType.Gate:
							ref var gateCollider = ref GateColliders.GetElementAsRef(lookup.Index);
							gateCollider.Transform(GateColliders[lookup.Index], math.inverse(matrix));
							break;
					}
				}
			}
		}

		private ICollider LookupCollider(int i)
		{
			if (i < 0 || i >= Lookups.Length) {
				throw new IndexOutOfRangeException($"Invalid index {i} when looking up collider.");
			}

			ref var lookup = ref Lookups.GetElementAsRef(i);
			switch (lookup.Type) {
				case ColliderType.Circle: return CircleColliders.GetElementAsRef(lookup.Index);
				case ColliderType.Flipper: return FlipperColliders.GetElementAsRef(lookup.Index);
				case ColliderType.Gate: return GateColliders.GetElementAsRef(lookup.Index);
				case ColliderType.Line3D: return Line3DColliders.GetElementAsRef(lookup.Index);
				case ColliderType.LineSlingShot: return LineSlingshotColliders.GetElementAsRef(lookup.Index);
				case ColliderType.Line: return LineColliders.GetElementAsRef(lookup.Index);
				case ColliderType.LineZ: return LineZColliders.GetElementAsRef(lookup.Index);
				case ColliderType.Plunger: return PlungerColliders.GetElementAsRef(lookup.Index);
				case ColliderType.Point: return PointColliders.GetElementAsRef(lookup.Index);
				case ColliderType.Spinner: return SpinnerColliders.GetElementAsRef(lookup.Index);
				case ColliderType.Triangle: return TriangleColliders.GetElementAsRef(lookup.Index);
				case ColliderType.Plane: return PlaneColliders.GetElementAsRef(lookup.Index);
			}
			throw new ArgumentException($"Unknown lookup type.");
		}

		#region Add

		private void TrackReference(int itemId, int colliderId)
		{
			if (!KinematicColliders) {
				return;
			}

			if (!_itemIdToColliderIds.ContainsKey(itemId)) {
				_itemIdToColliderIds[itemId] = new NativeList<int>(Allocator.Temp);
			}
			_itemIdToColliderIds[itemId].Add(colliderId);
		}

		internal int Add(CircleCollider collider, float4x4 matrix)
		{
			if (CircleCollider.IsTransformable(matrix)) {
				collider.Header.IsTransformed = true;
				collider.Transform(matrix);

			} else {
				// save matrix for use during runtime
				if (!_nonTransformableColliderMatrices.ContainsKey(collider.Header.ItemId)) {
					_nonTransformableColliderMatrices.Add(collider.Header.ItemId, matrix);
				}
				collider.Header.IsTransformed = false;
				collider.TransformAabb(matrix);
			}

			collider.Id = Lookups.Length;
			TrackReference(collider.Header.ItemId, collider.Header.Id);
			Lookups.Add(new ColliderLookup(ColliderType.Circle, CircleColliders.Length));
			CircleColliders.Add(collider);

			return collider.Id;
		}

		[Obsolete("Add with matrix only.")]
		internal int Add(CircleCollider collider)
		{
			collider.Id = Lookups.Length;
			TrackReference(collider.Header.ItemId, collider.Header.Id);
			Lookups.Add(new ColliderLookup(ColliderType.Circle, CircleColliders.Length));
			CircleColliders.Add(collider);
			return collider.Id;
		}

		internal int Add(FlipperCollider collider, float4x4 matrix)
		{
			if (FlipperCollider.IsTransformable(matrix)) {
				collider.Header.IsTransformed = true;
				collider.Transform(matrix);

			} else {
				// save matrix for use during runtime
				if (!_nonTransformableColliderMatrices.ContainsKey(collider.Header.ItemId)) {
					_nonTransformableColliderMatrices.Add(collider.Header.ItemId, matrix);
				}
				collider.Header.IsTransformed = false;
				collider.TransformAabb(matrix);
			}

			collider.Id = Lookups.Length;
			TrackReference(collider.Header.ItemId, collider.Header.Id);
			Lookups.Add(new ColliderLookup(ColliderType.Flipper, FlipperColliders.Length));
			FlipperColliders.Add(collider);
			return collider.Id;
		}

		internal int Add(GateCollider collider, float4x4 matrix)
		{
			if (GateCollider.IsTransformable(matrix)) {
				collider.Header.IsTransformed = true;
				collider.Transform(matrix);

			} else {
				// save matrix for use during runtime
				if (!_nonTransformableColliderMatrices.ContainsKey(collider.Header.ItemId)) {
					_nonTransformableColliderMatrices.Add(collider.Header.ItemId, matrix);
				}

				collider.Header.IsTransformed = false;
				collider.TransformAabb(matrix);
			}

			collider.Id = Lookups.Length;
			TrackReference(collider.Header.ItemId, collider.Header.Id);
			Lookups.Add(new ColliderLookup(ColliderType.Gate, GateColliders.Length));
			GateColliders.Add(collider);
			return collider.Id;
		}

		internal int Add(Line3DCollider collider, float4x4 matrix)
		{
			collider.Header.IsTransformed = true;
			collider.Transform(matrix);

			collider.Id = Lookups.Length;
			TrackReference(collider.Header.ItemId, collider.Header.Id);
			Lookups.Add(new ColliderLookup(ColliderType.Line3D, Line3DColliders.Length));
			Line3DColliders.Add(collider);
			return collider.Id;
		}

		[Obsolete("Add with matrix only.")]
		internal int Add(Line3DCollider collider)
		{
			collider.Id = Lookups.Length;
			TrackReference(collider.Header.ItemId, collider.Header.Id);
			Lookups.Add(new ColliderLookup(ColliderType.Line3D, Line3DColliders.Length));
			Line3DColliders.Add(collider);
			return collider.Id;
		}

		internal int Add(LineSlingshotCollider collider)
		{
			collider.Id = Lookups.Length;
			TrackReference(collider.Header.ItemId, collider.Header.Id);
			Lookups.Add(new ColliderLookup(ColliderType.LineSlingShot, LineSlingshotColliders.Length));
			LineSlingshotColliders.Add(collider);
			return collider.Id;
		}

		internal int Add(LineCollider collider)
		{
			collider.Id = Lookups.Length;
			TrackReference(collider.Header.ItemId, collider.Header.Id);
			Lookups.Add(new ColliderLookup(ColliderType.Line, LineColliders.Length));
			LineColliders.Add(collider);
			return collider.Id;
		}

		internal int Add(LineZCollider collider)
		{
			collider.Id = Lookups.Length;
			TrackReference(collider.Header.ItemId, collider.Header.Id);
			Lookups.Add(new ColliderLookup(ColliderType.LineZ, LineZColliders.Length));
			LineZColliders.Add(collider);
			return collider.Id;
		}

		internal int Add(PlungerCollider collider, float4x4 matrix)
		{
			if (PlungerCollider.IsTransformable(matrix)) {
				collider.Header.IsTransformed = true;
				collider.Transform(matrix);

			} else {
				// save matrix for use during runtime
				if (!_nonTransformableColliderMatrices.ContainsKey(collider.Header.ItemId)) {
					_nonTransformableColliderMatrices.Add(collider.Header.ItemId, matrix);
				}

				collider.Header.IsTransformed = false;
				collider.TransformAabb(matrix);
			}

			collider.Id = Lookups.Length;
			TrackReference(collider.Header.ItemId, collider.Header.Id);
			Lookups.Add(new ColliderLookup(ColliderType.Plunger, PlungerColliders.Length));
			PlungerColliders.Add(collider);
			return collider.Id;
		}

		internal int Add(PointCollider collider, float4x4 matrix)
		{
			collider.Header.IsTransformed = true;
			collider.Transform(matrix);

			collider.Id = Lookups.Length;
			TrackReference(collider.Header.ItemId, collider.Header.Id);
			Lookups.Add(new ColliderLookup(ColliderType.Point, PointColliders.Length));
			PointColliders.Add(collider);
			return collider.Id;
		}

		[Obsolete("Add with matrix only.")]
		internal int Add(PointCollider collider)
		{
			collider.Id = Lookups.Length;
			TrackReference(collider.Header.ItemId, collider.Header.Id);
			Lookups.Add(new ColliderLookup(ColliderType.Point, PointColliders.Length));
			PointColliders.Add(collider);
			return collider.Id;
		}

		internal int Add(SpinnerCollider collider, float4x4 matrix)
		{
			if (SpinnerCollider.IsTransformable(matrix)) {
				collider.Header.IsTransformed = true;
				collider.Transform(matrix);

			} else {
				// save matrix for use during runtime
				if (!_nonTransformableColliderMatrices.ContainsKey(collider.Header.ItemId)) {
					_nonTransformableColliderMatrices.Add(collider.Header.ItemId, matrix);
				}

				collider.Header.IsTransformed = false;
				collider.TransformAabb(matrix);
			}

			collider.Id = Lookups.Length;
			TrackReference(collider.Header.ItemId, collider.Header.Id);
			Lookups.Add(new ColliderLookup(ColliderType.Spinner, SpinnerColliders.Length));
			SpinnerColliders.Add(collider);
			return collider.Id;
		}

		internal int Add(TriangleCollider collider, float4x4 matrix)
		{
			collider.Header.IsTransformed = true;
			collider.Transform(matrix);

			collider.Id = Lookups.Length;
			TrackReference(collider.Header.ItemId, collider.Header.Id);
			Lookups.Add(new ColliderLookup(ColliderType.Triangle, TriangleColliders.Length));
			TriangleColliders.Add(collider);
			return collider.Id;
		}

		[Obsolete("Add with matrix only.")]
		internal int Add(TriangleCollider collider)
		{
			collider.Id = Lookups.Length;
			TrackReference(collider.Header.ItemId, collider.Header.Id);
			Lookups.Add(new ColliderLookup(ColliderType.Triangle, TriangleColliders.Length));
			TriangleColliders.Add(collider);
			return collider.Id;
		}

		internal int Add(PlaneCollider collider)
		{
			collider.Id = Lookups.Length;
			TrackReference(collider.Header.ItemId, collider.Header.Id);
			Lookups.Add(new ColliderLookup(ColliderType.Plane, PlaneColliders.Length));
			PlaneColliders.Add(collider);
			return collider.Id;
		}

		internal void AddLine(float2 v1, float2 v2, float zLow, float zHigh, ColliderInfo info, float4x4 matrix)
		{
			if (KinematicColliders || !matrix.IsPureTranslationMatrix()) {
				var p1 = new float3(v1.xy, zLow);
				var p2 = new float3(v1.xy, zHigh);
				var p3 = new float3(v2.xy, zLow);
				var p4 = new float3(v2.xy, zHigh);

				Add(new TriangleCollider(p1, p3, p2, info).Transform(matrix));
				Add(new TriangleCollider(p3, p4, p2, info).Transform(matrix));

			} else {
				Add(new LineCollider(v1, v2, zLow, zHigh, info).Transform(matrix));
			}
		}

		internal void AddLineZ(float2 xy, float zLow, float zHigh, ColliderInfo info, float4x4 matrix)
		{
			if (KinematicColliders || !matrix.IsPureTranslationMatrix()) {
				Add(new Line3DCollider(new float3(xy.xy, zLow), new float3(xy.xy, zHigh), info).Transform(matrix));
			} else {
				Add(new LineZCollider(xy, zLow, zHigh, info).Transform(matrix));
			}
		}

		#endregion

		public ICollider[] ToArray()
		{
			var array = new ICollider[Lookups.Length];
			for (var i = 0; i < Lookups.Length; i++) {
				var lookup = Lookups[i];
				switch (lookup.Type) {
					case ColliderType.Circle:
						array[i] = CircleColliders[lookup.Index];
						break;
					case ColliderType.Flipper:
						array[i] = FlipperColliders[lookup.Index];
						break;
					case ColliderType.Gate:
						array[i] = GateColliders[lookup.Index];
						break;
					case ColliderType.Line3D:
						array[i] = Line3DColliders[lookup.Index];
						break;
					case ColliderType.LineSlingShot:
						array[i] = LineSlingshotColliders[lookup.Index];
						break;
					case ColliderType.Line:
						array[i] = LineColliders[lookup.Index];
						break;
					case ColliderType.LineZ:
						array[i] = LineZColliders[lookup.Index];
						break;
					case ColliderType.Plunger:
						array[i] = PlungerColliders[lookup.Index];
						break;
					case ColliderType.Point:
						array[i] = PointColliders[lookup.Index];
						break;
					case ColliderType.Spinner:
						array[i] = SpinnerColliders[lookup.Index];
						break;
					case ColliderType.Triangle:
						array[i] = TriangleColliders[lookup.Index];
						break;
					case ColliderType.Plane:
						array[i] = PlaneColliders[lookup.Index];
						break;
				}
			}
			return array;
		}

		public NativeParallelHashMap<int, NativeColliderIds> CreateLookup(Allocator allocator)
		{
			var lookup = new NativeParallelHashMap<int, NativeColliderIds>(_itemIdToColliderIds.Count(), allocator);
			using var enumerator = _itemIdToColliderIds.GetEnumerator();
			while (enumerator.MoveNext()) {
				lookup.Add(enumerator.Current.Key, new NativeColliderIds(enumerator.Current.Value, allocator));
			}
			return lookup;
		}
	}
}
