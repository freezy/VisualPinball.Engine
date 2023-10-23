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
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;

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

		public int Count => CircleColliders.Length + FlipperColliders.Length + GateColliders.Length
			+ Line3DColliders.Length + LineSlingshotColliders.Length + LineColliders.Length + LineZColliders.Length
			+ PlungerColliders.Length + PointColliders.Length + SpinnerColliders.Length + TriangleColliders.Length
			+ PlaneColliders.Length;

		internal List<ICollider> All {
			get {
				var list = new List<ICollider>();
				list.AddRange(CircleColliders.Select(c => (ICollider)c));
				list.AddRange(FlipperColliders.Select(c => (ICollider)c));
				list.AddRange(GateColliders.Select(c => (ICollider)c));
				list.AddRange(Line3DColliders.Select(c => (ICollider)c));
				list.AddRange(LineSlingshotColliders.Select(c => (ICollider)c));
				list.AddRange(LineColliders.Select(c => (ICollider)c));
				list.AddRange(LineZColliders.Select(c => (ICollider)c));
				list.AddRange(PlungerColliders.Select(c => (ICollider)c));
				list.AddRange(PointColliders.Select(c => (ICollider)c));
				list.AddRange(SpinnerColliders.Select(c => (ICollider)c));
				list.AddRange(TriangleColliders.Select(c => (ICollider)c));
				list.AddRange(PlaneColliders.Select(c => (ICollider)c));
				return list;
			}
		}

		internal void Add(CircleCollider collider) => CircleColliders.Add(collider);
		internal void Add(FlipperCollider collider) => FlipperColliders.Add(collider);
		internal void Add(GateCollider collider) => GateColliders.Add(collider);
		internal void Add(Line3DCollider collider) => Line3DColliders.Add(collider);
		internal void Add(LineSlingshotCollider collider) => LineSlingshotColliders.Add(collider);
		internal void Add(LineCollider collider) => LineColliders.Add(collider);
		internal void Add(LineZCollider collider) => LineZColliders.Add(collider);
		internal void Add(PlungerCollider collider) => PlungerColliders.Add(collider);
		internal void Add(PointCollider collider) => PointColliders.Add(collider);
		internal void Add(SpinnerCollider collider) => SpinnerColliders.Add(collider);
		internal void Add(TriangleCollider collider) => TriangleColliders.Add(collider);
		internal void Add(PlaneCollider collider) => PlaneColliders.Add(collider);
	}
}
