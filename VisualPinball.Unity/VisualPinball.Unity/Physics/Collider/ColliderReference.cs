﻿// Visual Pinball Engine
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
using VisualPinball.Unity.Collections;

namespace VisualPinball.Unity
{
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

		public NativeList<ColliderLookup> Lookup;

		public readonly bool KinematicColliders;
		private NativeParallelHashMap<int, NativeList<int>> _references;


		public ColliderReference(Allocator allocator, bool kinematicColliders = false)
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
			Lookup = new NativeList<ColliderLookup>(allocator);

			KinematicColliders = kinematicColliders;
			_references = new NativeParallelHashMap<int, NativeList<int>>(0, allocator);
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
			using (var enumerator = _references.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					enumerator.Current.Value.Dispose();
				}
			}
			_references.Dispose();
		}

		public int Count => Lookup.Length;

		public ICollider this[int i] => LookupCollider(i);

		private ICollider LookupCollider(int i)
		{
			if (i < 0 || i >= Lookup.Length) {
				throw new IndexOutOfRangeException($"Invalid index {i} when looking up collider.");
			}

			ref var lookup = ref Lookup.GetElementAsRef(i);
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

			if (!_references.ContainsKey(itemId)) {
				_references[itemId] = new NativeList<int>(Allocator.Temp);
			}
			_references[itemId].Add(colliderId);
		}

		internal int Add(CircleCollider collider)
		{
			collider.Id = Lookup.Length;
			TrackReference(collider.Header.ItemId, collider.Header.Id);
			Lookup.Add(new ColliderLookup(ColliderType.Circle, CircleColliders.Length));
			CircleColliders.Add(collider);
			return collider.Id;
		}

		internal int Add(FlipperCollider collider)
		{
			collider.Id = Lookup.Length;
			TrackReference(collider.Header.ItemId, collider.Header.Id);
			Lookup.Add(new ColliderLookup(ColliderType.Flipper, FlipperColliders.Length));
			FlipperColliders.Add(collider);
			return collider.Id;
		}

		internal int Add(GateCollider collider)
		{
			collider.Id = Lookup.Length;
			TrackReference(collider.Header.ItemId, collider.Header.Id);
			Lookup.Add(new ColliderLookup(ColliderType.Gate, GateColliders.Length));
			GateColliders.Add(collider);
			return collider.Id;
		}

		internal int Add(Line3DCollider collider)
		{
			collider.Id = Lookup.Length;
			TrackReference(collider.Header.ItemId, collider.Header.Id);
			Lookup.Add(new ColliderLookup(ColliderType.Line3D, Line3DColliders.Length));
			Line3DColliders.Add(collider);
			return collider.Id;
		}

		internal int Add(LineSlingshotCollider collider)
		{
			collider.Id = Lookup.Length;
			TrackReference(collider.Header.ItemId, collider.Header.Id);
			Lookup.Add(new ColliderLookup(ColliderType.LineSlingShot, LineSlingshotColliders.Length));
			LineSlingshotColliders.Add(collider);
			return collider.Id;
		}

		internal int Add(LineCollider collider)
		{
			collider.Id = Lookup.Length;
			TrackReference(collider.Header.ItemId, collider.Header.Id);
			Lookup.Add(new ColliderLookup(ColliderType.Line, LineColliders.Length));
			LineColliders.Add(collider);
			return collider.Id;
		}

		internal int Add(LineZCollider collider)
		{
			collider.Id = Lookup.Length;
			TrackReference(collider.Header.ItemId, collider.Header.Id);
			Lookup.Add(new ColliderLookup(ColliderType.LineZ, LineZColliders.Length));
			LineZColliders.Add(collider);
			return collider.Id;
		}

		internal int Add(PlungerCollider collider)
		{
			collider.Id = Lookup.Length;
			TrackReference(collider.Header.ItemId, collider.Header.Id);
			Lookup.Add(new ColliderLookup(ColliderType.Plunger, PlungerColliders.Length));
			PlungerColliders.Add(collider);
			return collider.Id;
		}

		internal int Add(PointCollider collider)
		{
			collider.Id = Lookup.Length;
			TrackReference(collider.Header.ItemId, collider.Header.Id);
			Lookup.Add(new ColliderLookup(ColliderType.Point, PointColliders.Length));
			PointColliders.Add(collider);
			return collider.Id;
		}

		internal int Add(SpinnerCollider collider)
		{
			collider.Id = Lookup.Length;
			TrackReference(collider.Header.ItemId, collider.Header.Id);
			Lookup.Add(new ColliderLookup(ColliderType.Spinner, SpinnerColliders.Length));
			SpinnerColliders.Add(collider);
			return collider.Id;
		}

		internal int Add(TriangleCollider collider)
		{
			collider.Id = Lookup.Length;
			TrackReference(collider.Header.ItemId, collider.Header.Id);
			Lookup.Add(new ColliderLookup(ColliderType.Triangle, TriangleColliders.Length));
			TriangleColliders.Add(collider);
			return collider.Id;
		}

		internal int Add(PlaneCollider collider)
		{
			collider.Id = Lookup.Length;
			TrackReference(collider.Header.ItemId, collider.Header.Id);
			Lookup.Add(new ColliderLookup(ColliderType.Plane, PlaneColliders.Length));
			PlaneColliders.Add(collider);
			return collider.Id;
		}

		#endregion

		public ICollider[] ToArray()
		{
			var array = new ICollider[Lookup.Length];
			for (var i = 0; i < Lookup.Length; i++) {
				var lookup = Lookup[i];
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
	}
}
