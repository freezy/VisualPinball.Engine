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

		public ColliderReference(Allocator allocator)
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
		}

		public int Count => Lookup.Length;

		public ICollider this[int i] => LookupCollider(i);

		private ICollider LookupCollider(int i)
		{
			if (i < 0 || i >= Lookup.Length) {
				throw new IndexOutOfRangeException($"Invalid index {i} when looking up collider.");
			}

			var lookup = Lookup[i];
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

		internal void Add(CircleCollider collider)
		{
			collider.Header.Id = Lookup.Length;
			Lookup.Add(new ColliderLookup(ColliderType.Circle, CircleColliders.Length));
			CircleColliders.Add(collider);
		}

		internal void Add(FlipperCollider collider)
		{
			collider.Header.Id = Lookup.Length;
			var bounds = collider.Bounds;
			bounds.ColliderId = collider.Header.Id;
			collider.Bounds = bounds;
			Lookup.Add(new ColliderLookup(ColliderType.Flipper, FlipperColliders.Length));
			FlipperColliders.Add(collider);
		}

		internal void Add(GateCollider collider)
		{
			collider.Header.Id = Lookup.Length;
			var bounds = collider.Bounds;
			bounds.ColliderId = collider.Header.Id;
			collider.Bounds = bounds;
			Lookup.Add(new ColliderLookup(ColliderType.Gate, GateColliders.Length));
			GateColliders.Add(collider);
		}

		internal void Add(Line3DCollider collider)
		{
			collider.Header.Id = Lookup.Length;
			var bounds = collider.Bounds;
			bounds.ColliderId = collider.Header.Id;
			collider.Bounds = bounds;
			Lookup.Add(new ColliderLookup(ColliderType.Line3D, Line3DColliders.Length));
			Line3DColliders.Add(collider);
		}

		internal void Add(LineSlingshotCollider collider)
		{
			collider.Header.Id = Lookup.Length;
			Lookup.Add(new ColliderLookup(ColliderType.LineSlingShot, LineSlingshotColliders.Length));
			LineSlingshotColliders.Add(collider);
		}

		internal void Add(LineCollider collider)
		{
			collider.Header.Id = Lookup.Length;
			Lookup.Add(new ColliderLookup(ColliderType.Line, LineColliders.Length));
			LineColliders.Add(collider);
		}

		internal void Add(LineZCollider collider)
		{
			collider.Header.Id = Lookup.Length;
			Lookup.Add(new ColliderLookup(ColliderType.LineZ, LineZColliders.Length));
			LineZColliders.Add(collider);
		}

		internal void Add(PlungerCollider collider)
		{
			collider.Header.Id = Lookup.Length;
			var bounds = collider.Bounds;
			bounds.ColliderId = collider.Header.Id;
			collider.Bounds = bounds;
			Lookup.Add(new ColliderLookup(ColliderType.Plunger, PlungerColliders.Length));
			PlungerColliders.Add(collider);
		}

		internal void Add(PointCollider collider)
		{
			collider.Header.Id = Lookup.Length;
			Lookup.Add(new ColliderLookup(ColliderType.Point, PointColliders.Length));
			PointColliders.Add(collider);
		}

		internal void Add(SpinnerCollider collider)
		{
			collider.Header.Id = Lookup.Length;
			var bounds = collider.Bounds;
			bounds.ColliderId = collider.Header.Id;
			collider.Bounds = bounds;
			Lookup.Add(new ColliderLookup(ColliderType.Spinner, SpinnerColliders.Length));
			SpinnerColliders.Add(collider);
		}

		internal void Add(TriangleCollider collider)
		{
			collider.Header.Id = Lookup.Length;
			Lookup.Add(new ColliderLookup(ColliderType.Triangle, TriangleColliders.Length));
			TriangleColliders.Add(collider);
		}

		internal void Add(PlaneCollider collider)
		{
			collider.Header.Id = Lookup.Length;
			Lookup.Add(new ColliderLookup(ColliderType.Plane, PlaneColliders.Length));
			PlaneColliders.Add(collider);
		}

		#endregion

		public readonly struct ColliderLookup
		{
			public readonly ColliderType Type;
			public readonly int Index;

			public ColliderLookup(ColliderType type, int index)
			{
				Type = type;
				Index = index;
			}
		}
	}
}
