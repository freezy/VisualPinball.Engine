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

		/// <summary>
		/// If true, then all colliders are kinematic.
		/// </summary>
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

		public void TransformToIdentity(ref NativeParallelHashMap<int, float4x4> itemIdToTransformationMatrix)
		{
			#if UNITY_EDITOR
			if (!KinematicColliders) {
				throw new InvalidOperationException("Cannot transform non-kinetic colliders to identity.");
			}
			#endif
			using var enumerator = _itemIdToColliderIds.GetEnumerator();
			while (enumerator.MoveNext()) {
				var itemId = enumerator.Current.Key;
				ref var colliderIds = ref enumerator.Current.Value;
				foreach (var colliderId in colliderIds) {
					var matrix = itemIdToTransformationMatrix[itemId];
					var lookup = Lookups[colliderId];
					switch (lookup.Type) {

						case ColliderType.TriggerCircle:
						case ColliderType.KickerCircle:
						case ColliderType.Bumper:
						case ColliderType.Circle:
							ref var circleCollider = ref CircleColliders.GetElementAsRef(lookup.Index);
							#if UNITY_EDITOR
							if (circleCollider.Header.IsTransformed) {
								throw new InvalidOperationException("A transformed circle collider shouldn't have been added as a kinetic collider.");
							}
							#endif
							circleCollider.TransformAabb(math.inverse(matrix));
							break;

						case ColliderType.Point:
							ref var pointCollider = ref PointColliders.GetElementAsRef(lookup.Index);
							#if UNITY_EDITOR
							if (!pointCollider.Header.IsTransformed) {
								throw new InvalidOperationException("Points are fully transformable, so they should always be transformed.");
							}
							#endif
							pointCollider.Transform(PointColliders[lookup.Index], math.inverse(matrix));
							break;

						case ColliderType.Line3D:
							ref var line3DCollider = ref Line3DColliders.GetElementAsRef(lookup.Index);
							#if UNITY_EDITOR
							if (!line3DCollider.Header.IsTransformed) {
								throw new InvalidOperationException("Line3D colliders are fully transformable, so they should always be transformed.");
							}
							#endif
							line3DCollider.Transform(Line3DColliders[lookup.Index], math.inverse(matrix));
							break;

						case ColliderType.Triangle:
							ref var triangleCollider = ref TriangleColliders.GetElementAsRef(lookup.Index);
							#if UNITY_EDITOR
							if (!triangleCollider.Header.IsTransformed) {
								throw new InvalidOperationException("Triangles are fully transformable, so they should always be transformed.");
							}
							#endif
							triangleCollider.Transform(TriangleColliders[lookup.Index], math.inverse(matrix));
							break;

						case ColliderType.Spinner:
							ref var spinnerCollider = ref SpinnerColliders.GetElementAsRef(lookup.Index);
							#if UNITY_EDITOR
							if (spinnerCollider.Header.IsTransformed) {
								throw new InvalidOperationException("A transformed spinner collider shouldn't have been added as a kinetic collider.");
							}
							#endif
							spinnerCollider.TransformAabb(math.inverse(matrix));
							break;

						case ColliderType.Gate:
							ref var gateCollider = ref GateColliders.GetElementAsRef(lookup.Index);
							#if UNITY_EDITOR
							if (gateCollider.Header.IsTransformed) {
								throw new InvalidOperationException("A transformed gate collider shouldn't have been added as a kinetic collider.");
							}
							#endif
							gateCollider.TransformAabb(math.inverse(matrix));
							break;

						case ColliderType.Flipper:
							ref var flipperCollider = ref FlipperColliders.GetElementAsRef(lookup.Index);
							#if UNITY_EDITOR
							if (flipperCollider.Header.IsTransformed) {
								throw new InvalidOperationException("A transformed flipper collider shouldn't have been added as a kinetic collider.");
							}
							#endif
							flipperCollider.TransformAabb(math.inverse(matrix));
							break;

						case ColliderType.LineSlingShot:
							ref var slingshotCollider = ref LineSlingshotColliders.GetElementAsRef(lookup.Index);
							#if UNITY_EDITOR
							if (slingshotCollider.Header.IsTransformed) {
								throw new InvalidOperationException("A transformed slingshot collider shouldn't have been added as a kinetic collider.");
							}
							#endif
							slingshotCollider.TransformAabb(math.inverse(matrix));
							break;

						case ColliderType.Plunger:
							ref var plungerCollider = ref PlungerColliders.GetElementAsRef(lookup.Index);
							#if UNITY_EDITOR
							if (plungerCollider.Header.IsTransformed) {
								throw new InvalidOperationException("A transformed plunger collider shouldn't have been added as a kinetic collider.");
							}
							#endif
							plungerCollider.TransformAabb(math.inverse(matrix));
							break;

						case ColliderType.Line:
							#if UNITY_EDITOR
							throw new InvalidOperationException("Line colliders shouldn't exist as kinetic colliders, but converted to line 3D colliders.");
							#endif
						case ColliderType.LineZ:
							#if UNITY_EDITOR
							throw new InvalidOperationException("Line-Z colliders shouldn't exist as kinetic colliders, but converted to line 3D colliders.");
							#endif
						case ColliderType.Plane:
							#if UNITY_EDITOR
							throw new InvalidOperationException("Planes cannot be be kinematic.");
							#endif
						case ColliderType.None:
							break;
						default:
							throw new ArgumentOutOfRangeException();
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
			throw new ArgumentException("Unknown lookup type.");
		}

		#region Add

		private void TrackReference(int itemId, int colliderId)
		{
			#if !UNITY_EDITOR
			if (!KinematicColliders) {
				return;
			}
			#endif

			if (!_itemIdToColliderIds.ContainsKey(itemId)) {
				_itemIdToColliderIds[itemId] = new NativeList<int>(Allocator.Temp);
			}
			_itemIdToColliderIds[itemId].Add(colliderId);
		}

		internal int Add(CircleCollider collider, float4x4 matrix)
		{
			if (!KinematicColliders && CircleCollider.IsTransformable(matrix)) {
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

		internal int Add(FlipperCollider collider, float4x4 matrix)
		{
			if (!KinematicColliders && FlipperCollider.IsTransformable(matrix)) {
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

		internal void Add(GateCollider collider, float4x4 matrix)
		{
			if (!KinematicColliders && GateCollider.IsTransformable(matrix)) {
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
		}

		internal void Add(Line3DCollider collider, float4x4 matrix)
		{
			collider.Header.IsTransformed = true;
			collider.Transform(matrix);

			collider.Id = Lookups.Length;
			TrackReference(collider.Header.ItemId, collider.Header.Id);
			Lookups.Add(new ColliderLookup(ColliderType.Line3D, Line3DColliders.Length));
			Line3DColliders.Add(collider);
		}

		internal void Add(LineSlingshotCollider collider, float4x4 matrix)
		{
			if (!KinematicColliders && LineSlingshotCollider.IsTransformable(matrix)) {
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
			Lookups.Add(new ColliderLookup(ColliderType.LineSlingShot, LineSlingshotColliders.Length));
			LineSlingshotColliders.Add(collider);
		}

		internal void Add(LineCollider collider) => Add(collider, float4x4.identity); // used for the playfield only
		internal void Add(LineCollider collider, float4x4 matrix)
		{
			if (!KinematicColliders && LineCollider.IsTransformable(matrix)) {
				collider.Header.IsTransformed = true;
				collider.Transform(matrix);

				collider.Id = Lookups.Length;
				TrackReference(collider.Header.ItemId, collider.Header.Id);
				Lookups.Add(new ColliderLookup(ColliderType.Line, LineColliders.Length));
				LineColliders.Add(collider);

			} else {

				// convert line collider to two triangle colliders
				var p1 = new float3(collider.V1.xy, collider.ZLow);
				var p2 = new float3(collider.V1.xy, collider.ZHigh);
				var p3 = new float3(collider.V2.xy, collider.ZLow);
				var p4 = new float3(collider.V2.xy, collider.ZHigh);

				var t1 = new TriangleCollider(p1, p3, p2, collider.Header.ColliderInfo);
				var t2 = new TriangleCollider(p3, p4, p2, collider.Header.ColliderInfo);

				t1.Header.IsTransformed = true;
				t2.Header.IsTransformed = true;

				Add(t1, matrix);
				Add(t2, matrix);
			}
		}

		internal void Add(LineZCollider collider, float4x4 matrix)
		{
			if (KinematicColliders || !LineZCollider.IsTransformable(matrix)) {
				// use line 3d collider instead
				Add(new Line3DCollider(new float3(collider.XY, collider.ZLow), new float3(collider.XY, collider.ZHigh), collider.Header.ColliderInfo), matrix);
				return;
			}

			collider.Header.IsTransformed = true;
			collider.Transform(matrix);

			collider.Id = Lookups.Length;
			TrackReference(collider.Header.ItemId, collider.Header.Id);
			Lookups.Add(new ColliderLookup(ColliderType.LineZ, LineZColliders.Length));
			LineZColliders.Add(collider);
		}

		internal void Add(PlungerCollider collider, float4x4 matrix)
		{
			if (!KinematicColliders && PlungerCollider.IsTransformable(matrix)) {
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
		}

		internal void Add(PointCollider collider, float4x4 matrix)
		{
			collider.Header.IsTransformed = true;
			collider.Transform(matrix);

			collider.Id = Lookups.Length;
			TrackReference(collider.Header.ItemId, collider.Header.Id);
			Lookups.Add(new ColliderLookup(ColliderType.Point, PointColliders.Length));
			PointColliders.Add(collider);
		}

		internal void Add(SpinnerCollider collider, float4x4 matrix)
		{
			if (!KinematicColliders && SpinnerCollider.IsTransformable(matrix)) {
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

		internal void Add(PlaneCollider collider) // used for the playfield only
		{
			collider.Id = Lookups.Length;
			TrackReference(collider.Header.ItemId, collider.Header.Id);
			Lookups.Add(new ColliderLookup(ColliderType.Plane, PlaneColliders.Length));
			PlaneColliders.Add(collider);
		}

		#endregion

		// ReSharper disable once UnusedMember.Global
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
