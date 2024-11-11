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

// ReSharper disable InconsistentNaming

using System.Diagnostics;
using System;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Profiling;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity
{
	[NativeContainer]
	[NativeContainerSupportsMinMaxWriteRestriction]
	[DebuggerDisplay("Length = {Length}")]
	[DebuggerTypeProxy(typeof(NativeCollidersDebugView))]
	public unsafe struct NativeColliders : IDisposable
	{
		#region Members

		private static readonly ProfilerMarker PerfMarker = new("NativeColliders Allocation");

		public int Length => m_Length;
		public bool KinematicColliders => m_KinematicColliders;

		/// <summary>
		/// An array that links the collider IDs (the key) to the position in the respective collider buffer.
		/// </summary>
		[NativeDisableUnsafePtrRestriction] private void* m_LookupBuffer;

		[NativeDisableUnsafePtrRestriction] private void* m_CircleColliderBuffer;
		[NativeDisableUnsafePtrRestriction] private void* m_FlipperColliderBuffer;
		[NativeDisableUnsafePtrRestriction] private void* m_GateColliderBuffer;
		[NativeDisableUnsafePtrRestriction] private void* m_Line3DColliderBuffer;
		[NativeDisableUnsafePtrRestriction] private void* m_LineSlingshotColliderBuffer;
		[NativeDisableUnsafePtrRestriction] private void* m_LineColliderBuffer;
		[NativeDisableUnsafePtrRestriction] private void* m_LineZColliderBuffer;
		[NativeDisableUnsafePtrRestriction] private void* m_PlungerColliderBuffer;
		[NativeDisableUnsafePtrRestriction] private void* m_PointColliderBuffer;
		[NativeDisableUnsafePtrRestriction] private void* m_SpinnerColliderBuffer;
		[NativeDisableUnsafePtrRestriction] private void* m_TriangleColliderBuffer;
		[NativeDisableUnsafePtrRestriction] private void* m_PlaneColliderBuffer;

		private readonly Allocator m_AllocatorLabel;
		private readonly bool m_KinematicColliders;

		private int m_Length; // must be here, and called like that.

#if ENABLE_UNITY_COLLECTIONS_CHECKS
		internal int m_MinIndex;
		internal int m_MaxIndex;
		internal AtomicSafetyHandle m_Safety;
		internal static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<NativeColliders>();
#endif

		#endregion

		#region Constructor / Allocation

		public NativeColliders(ref ColliderReference colRef, Allocator allocator)
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			// Native allocation is only valid for Temp, Job and Persistent
			if (allocator <= Allocator.None) {
				throw new ArgumentException("Allocator must be Temp, TempJob or Persistent", nameof(allocator));
			}
#endif
			PerfMarker.Begin();

			m_Length = colRef.Lookups.Length;
			m_KinematicColliders = colRef.KinematicColliders;

			long size = UnsafeUtility.SizeOf<ColliderLookup>() * colRef.Lookups.Length;
			m_LookupBuffer = UnsafeUtility.Malloc(size, UnsafeUtility.AlignOf<ColliderLookup>(), allocator);
			UnsafeUtility.MemCpy(m_LookupBuffer, colRef.Lookups.GetUnsafePtr(), size);

			size = UnsafeUtility.SizeOf<FlipperCollider>() * colRef.FlipperColliders.Length;
			m_FlipperColliderBuffer = UnsafeUtility.Malloc(size, UnsafeUtility.AlignOf<FlipperCollider>(), allocator);
			UnsafeUtility.MemCpy(m_FlipperColliderBuffer, colRef.FlipperColliders.GetUnsafePtr(), size);

			size = UnsafeUtility.SizeOf<CircleCollider>() * colRef.CircleColliders.Length;
			m_CircleColliderBuffer = UnsafeUtility.Malloc(size, UnsafeUtility.AlignOf<CircleCollider>(), allocator);
			UnsafeUtility.MemCpy(m_CircleColliderBuffer, colRef.CircleColliders.GetUnsafePtr(), size);

			size = UnsafeUtility.SizeOf<GateCollider>() * colRef.GateColliders.Length;
			m_GateColliderBuffer = UnsafeUtility.Malloc(size, UnsafeUtility.AlignOf<GateCollider>(), allocator);
			UnsafeUtility.MemCpy(m_GateColliderBuffer, colRef.GateColliders.GetUnsafePtr(), size);

			size = UnsafeUtility.SizeOf<Line3DCollider>() * colRef.Line3DColliders.Length;
			m_Line3DColliderBuffer = UnsafeUtility.Malloc(size, UnsafeUtility.AlignOf<Line3DCollider>(), allocator);
			UnsafeUtility.MemCpy(m_Line3DColliderBuffer, colRef.Line3DColliders.GetUnsafePtr(), size);

			size = UnsafeUtility.SizeOf<LineSlingshotCollider>() * colRef.LineSlingshotColliders.Length;
			m_LineSlingshotColliderBuffer = UnsafeUtility.Malloc(size, UnsafeUtility.AlignOf<LineSlingshotCollider>(), allocator);
			UnsafeUtility.MemCpy(m_LineSlingshotColliderBuffer, colRef.LineSlingshotColliders.GetUnsafePtr(), size);

			size = UnsafeUtility.SizeOf<LineCollider>() * colRef.LineColliders.Length;
			m_LineColliderBuffer = UnsafeUtility.Malloc(size, UnsafeUtility.AlignOf<LineCollider>(), allocator);
			UnsafeUtility.MemCpy(m_LineColliderBuffer, colRef.LineColliders.GetUnsafePtr(), size);

			size = UnsafeUtility.SizeOf<LineZCollider>() * colRef.LineZColliders.Length;
			m_LineZColliderBuffer = UnsafeUtility.Malloc(size, UnsafeUtility.AlignOf<LineZCollider>(), allocator);
			UnsafeUtility.MemCpy(m_LineZColliderBuffer, colRef.LineZColliders.GetUnsafePtr(), size);

			size = UnsafeUtility.SizeOf<PlungerCollider>() * colRef.PlungerColliders.Length;
			m_PlungerColliderBuffer = UnsafeUtility.Malloc(size, UnsafeUtility.AlignOf<PlungerCollider>(), allocator);
			UnsafeUtility.MemCpy(m_PlungerColliderBuffer, colRef.PlungerColliders.GetUnsafePtr(), size);

			size = UnsafeUtility.SizeOf<PointCollider>() * colRef.PointColliders.Length;
			m_PointColliderBuffer = UnsafeUtility.Malloc(size, UnsafeUtility.AlignOf<PointCollider>(), allocator);
			UnsafeUtility.MemCpy(m_PointColliderBuffer, colRef.PointColliders.GetUnsafePtr(), size);

			size = UnsafeUtility.SizeOf<SpinnerCollider>() * colRef.SpinnerColliders.Length;
			m_SpinnerColliderBuffer = UnsafeUtility.Malloc(size, UnsafeUtility.AlignOf<SpinnerCollider>(), allocator);
			UnsafeUtility.MemCpy(m_SpinnerColliderBuffer, colRef.SpinnerColliders.GetUnsafePtr(), size);

			size = UnsafeUtility.SizeOf<TriangleCollider>() * colRef.TriangleColliders.Length;
			m_TriangleColliderBuffer = UnsafeUtility.Malloc(size, UnsafeUtility.AlignOf<TriangleCollider>(), allocator);
			UnsafeUtility.MemCpy(m_TriangleColliderBuffer, colRef.TriangleColliders.GetUnsafePtr(), size);

			size = UnsafeUtility.SizeOf<PlaneCollider>() * colRef.PlaneColliders.Length;
			m_PlaneColliderBuffer = UnsafeUtility.Malloc(size, UnsafeUtility.AlignOf<PlaneCollider>(), allocator);
			UnsafeUtility.MemCpy(m_PlaneColliderBuffer, colRef.PlaneColliders.GetUnsafePtr(), size);

			m_AllocatorLabel = allocator;

			PerfMarker.End();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
			m_MinIndex = 0;
			m_MaxIndex = m_Length - 1;
			m_Safety = CollectionHelper.CreateSafetyHandle(allocator);
			CollectionHelper.SetStaticSafetyId<NativeColliders>(ref m_Safety, ref s_staticSafetyId.Data);
#endif
		}

		#endregion

		#region Collider Access

		internal ref CircleCollider Circle(int colliderId)
		{
			ref var lookup = ref UnsafeUtility.ArrayElementAsRef<ColliderLookup>(m_LookupBuffer, colliderId);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if (lookup.Type != ColliderType.Circle) {
				throw new ArgumentException($"Invalid collider type {lookup.Type} when looking up circle collider.");
			}
#endif
			return ref UnsafeUtility.ArrayElementAsRef<CircleCollider>(m_CircleColliderBuffer, lookup.Index);
		}

		internal ref FlipperCollider Flipper(int colliderId)
		{
			ref var lookup = ref UnsafeUtility.ArrayElementAsRef<ColliderLookup>(m_LookupBuffer, colliderId);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if (lookup.Type != ColliderType.Flipper) {
				throw new ArgumentException($"Invalid collider type {lookup.Type} when looking up flipper collider.");
			}
#endif
			return ref UnsafeUtility.ArrayElementAsRef<FlipperCollider>(m_FlipperColliderBuffer, lookup.Index);
		}

		internal ref GateCollider Gate(int colliderId)
		{
			ref var lookup = ref UnsafeUtility.ArrayElementAsRef<ColliderLookup>(m_LookupBuffer, colliderId);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if (lookup.Type != ColliderType.Gate) {
				throw new ArgumentException($"Invalid collider type {lookup.Type} when looking up gate collider.");
			}
#endif
			return ref UnsafeUtility.ArrayElementAsRef<GateCollider>(m_GateColliderBuffer, lookup.Index);
		}

		internal ref Line3DCollider Line3D(int colliderId)
		{
			ref var lookup = ref UnsafeUtility.ArrayElementAsRef<ColliderLookup>(m_LookupBuffer, colliderId);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if (lookup.Type != ColliderType.Line3D) {
				throw new ArgumentException($"Invalid collider type {lookup.Type} when looking up line3d collider.");
			}
#endif
			return ref UnsafeUtility.ArrayElementAsRef<Line3DCollider>(m_Line3DColliderBuffer, lookup.Index);
		}

		internal ref LineCollider Line(int colliderId)
		{
			ref var lookup = ref UnsafeUtility.ArrayElementAsRef<ColliderLookup>(m_LookupBuffer, colliderId);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if (lookup.Type != ColliderType.Line) {
				throw new ArgumentException($"Invalid collider type {lookup.Type} when looking up line collider.");
			}
#endif
			return ref UnsafeUtility.ArrayElementAsRef<LineCollider>(m_LineColliderBuffer, lookup.Index);
		}

		internal ref LineSlingshotCollider LineSlingShot(int colliderId)
		{
			ref var lookup = ref UnsafeUtility.ArrayElementAsRef<ColliderLookup>(m_LookupBuffer, colliderId);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if (lookup.Type != ColliderType.LineSlingShot) {
				throw new ArgumentException($"Invalid collider type {lookup.Type} when looking up line slingshot collider.");
			}
#endif
			return ref UnsafeUtility.ArrayElementAsRef<LineSlingshotCollider>(m_LineSlingshotColliderBuffer, lookup.Index);
		}

		internal ref LineZCollider LineZ(int colliderId)
		{
			ref var lookup = ref UnsafeUtility.ArrayElementAsRef<ColliderLookup>(m_LookupBuffer, colliderId);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if (lookup.Type != ColliderType.LineZ) {
				throw new ArgumentException($"Invalid collider type {lookup.Type} when looking up line-z collider.");
			}
#endif
			return ref UnsafeUtility.ArrayElementAsRef<LineZCollider>(m_LineZColliderBuffer, lookup.Index);
		}

		internal ref PlaneCollider Plane(int colliderId)
		{
			ref var lookup = ref UnsafeUtility.ArrayElementAsRef<ColliderLookup>(m_LookupBuffer, colliderId);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if (lookup.Type != ColliderType.Plane) {
				throw new ArgumentException($"Invalid collider type {lookup.Type} when looking up plane collider.");
			}
#endif
			return ref UnsafeUtility.ArrayElementAsRef<PlaneCollider>(m_PlaneColliderBuffer, lookup.Index);
		}

		internal ref PlungerCollider Plunger(int colliderId)
		{
			ref var lookup = ref UnsafeUtility.ArrayElementAsRef<ColliderLookup>(m_LookupBuffer, colliderId);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if (lookup.Type != ColliderType.Plunger) {
				throw new ArgumentException($"Invalid collider type {lookup.Type} when looking up plunger collider.");
			}
#endif
			return ref UnsafeUtility.ArrayElementAsRef<PlungerCollider>(m_PlungerColliderBuffer, lookup.Index);
		}

		internal ref PointCollider Point(int colliderId)
		{
			ref var lookup = ref UnsafeUtility.ArrayElementAsRef<ColliderLookup>(m_LookupBuffer, colliderId);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if (lookup.Type != ColliderType.Point) {
				throw new ArgumentException($"Invalid collider type {lookup.Type} when looking up point collider.");
			}
#endif
			return ref UnsafeUtility.ArrayElementAsRef<PointCollider>(m_PointColliderBuffer, lookup.Index);
		}

		internal ref SpinnerCollider Spinner(int colliderId)
		{
			ref var lookup = ref UnsafeUtility.ArrayElementAsRef<ColliderLookup>(m_LookupBuffer, colliderId);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if (lookup.Type != ColliderType.Spinner) {
				throw new ArgumentException($"Invalid collider type {lookup.Type} when looking up spinner collider.");
			}
#endif
			return ref UnsafeUtility.ArrayElementAsRef<SpinnerCollider>(m_SpinnerColliderBuffer, lookup.Index);
		}

		internal ref TriangleCollider Triangle(int colliderId)
		{
			ref var lookup = ref UnsafeUtility.ArrayElementAsRef<ColliderLookup>(m_LookupBuffer, colliderId);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if (lookup.Type != ColliderType.Triangle) {
				throw new ArgumentException($"Invalid collider type {lookup.Type} when looking up triangle collider.");
			}
#endif
			return ref UnsafeUtility.ArrayElementAsRef<TriangleCollider>(m_TriangleColliderBuffer, lookup.Index);
		}

		#endregion

		#region Collection Access

		public ICollider this[int index]
		{
			get
			{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				// If the container is currently not allowed to read from the buffer
				// then this will throw an exception.
				// This handles all cases, from already disposed containers
				// to safe multithreaded access.
				AtomicSafetyHandle.CheckReadAndThrow(m_Safety);

				// Perform out of range checks based on
				// the NativeContainerSupportsMinMaxWriteRestriction policy
				if (index < m_MinIndex || index > m_MaxIndex)
					FailOutOfRangeError(index);
#endif
				if (index < 0 || index >= m_Length) {
					throw new IndexOutOfRangeException($"Invalid index {index} when looking up collider.");
				}

				var lookup = UnsafeUtility.ReadArrayElement<ColliderLookup>(m_LookupBuffer, index);
				switch (lookup.Type) {
					case ColliderType.Circle: return UnsafeUtility.ReadArrayElement<CircleCollider>(m_CircleColliderBuffer, lookup.Index);
					case ColliderType.Flipper: return UnsafeUtility.ReadArrayElement<FlipperCollider>(m_FlipperColliderBuffer, lookup.Index);
					case ColliderType.Gate: return UnsafeUtility.ReadArrayElement<GateCollider>(m_GateColliderBuffer, lookup.Index);
					case ColliderType.Line3D: return UnsafeUtility.ReadArrayElement<Line3DCollider>(m_Line3DColliderBuffer, lookup.Index);
					case ColliderType.LineSlingShot: return UnsafeUtility.ReadArrayElement<LineSlingshotCollider>(m_LineSlingshotColliderBuffer, lookup.Index);
					case ColliderType.Line: return UnsafeUtility.ReadArrayElement<LineCollider>(m_LineColliderBuffer, lookup.Index);
					case ColliderType.LineZ: return UnsafeUtility.ReadArrayElement<LineZCollider>(m_LineZColliderBuffer, lookup.Index);
					case ColliderType.Plunger: return UnsafeUtility.ReadArrayElement<PlungerCollider>(m_PlungerColliderBuffer, lookup.Index);
					case ColliderType.Point: return UnsafeUtility.ReadArrayElement<PointCollider>(m_PointColliderBuffer, lookup.Index);
					case ColliderType.Spinner: return UnsafeUtility.ReadArrayElement<SpinnerCollider>(m_SpinnerColliderBuffer, lookup.Index);
					case ColliderType.Triangle: return UnsafeUtility.ReadArrayElement<TriangleCollider>(m_TriangleColliderBuffer, lookup.Index);
					case ColliderType.Plane: return UnsafeUtility.ReadArrayElement<PlaneCollider>(m_PlaneColliderBuffer, lookup.Index);
				}
				throw new ArgumentException($"Unknown lookup type.");
			}

			set
			{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				// If the container is currently not allowed to write to the buffer
				// then this will throw an exception.
				// This handles all cases, from already disposed containers
				// to safe multithreaded access.
				AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);

				// Perform out of range checks based on
				// the NativeContainerSupportsMinMaxWriteRestriction policy
				if (index < m_MinIndex || index > m_MaxIndex)
					FailOutOfRangeError(index);
#endif

				if (index < 0 || index >= m_Length) {
					throw new IndexOutOfRangeException($"Invalid index {index} when looking up collider.");
				}

				var lookup = UnsafeUtility.ReadArrayElement<ColliderLookup>(m_LookupBuffer, index);
				switch (lookup.Type) {
					case ColliderType.Circle:
						UnsafeUtility.WriteArrayElement(m_CircleColliderBuffer, lookup.Index, (CircleCollider)value);
						break;
					case ColliderType.Flipper:
						UnsafeUtility.WriteArrayElement(m_FlipperColliderBuffer, lookup.Index, (FlipperCollider)value);
						break;
					case ColliderType.Gate:
						UnsafeUtility.WriteArrayElement(m_GateColliderBuffer, lookup.Index, (GateCollider)value);
						break;
					case ColliderType.Line:
						UnsafeUtility.WriteArrayElement(m_LineColliderBuffer, lookup.Index, (LineCollider)value);
						break;
					case ColliderType.Line3D:
						UnsafeUtility.WriteArrayElement(m_Line3DColliderBuffer, lookup.Index, (Line3DCollider)value);
						break;
					case ColliderType.LineSlingShot:
						UnsafeUtility.WriteArrayElement(m_LineSlingshotColliderBuffer, lookup.Index, (LineSlingshotCollider)value);
						break;
					case ColliderType.LineZ:
						UnsafeUtility.WriteArrayElement(m_LineZColliderBuffer, lookup.Index, (LineZCollider)value);
						break;
					case ColliderType.Plane:
						UnsafeUtility.WriteArrayElement(m_PlaneColliderBuffer, lookup.Index, (PlaneCollider)value);
						break;
					case ColliderType.Plunger:
						UnsafeUtility.WriteArrayElement(m_PlungerColliderBuffer, lookup.Index, (PlungerCollider)value);
						break;
					case ColliderType.Point:
						UnsafeUtility.WriteArrayElement(m_PointColliderBuffer, lookup.Index, (PointCollider)value);
						break;
					case ColliderType.Spinner:
						UnsafeUtility.WriteArrayElement(m_SpinnerColliderBuffer, lookup.Index, (SpinnerCollider)value);
						break;
					case ColliderType.Triangle:
						UnsafeUtility.WriteArrayElement(m_TriangleColliderBuffer, lookup.Index, (TriangleCollider)value);
						break;
					default:
						throw new ArgumentOutOfRangeException();
				}
			}
		}

		#endregion

		#region Disposition

		public void Dispose()
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			CollectionHelper.DisposeSafetyHandle(ref m_Safety);
#endif

			UnsafeUtility.Free(m_LookupBuffer, m_AllocatorLabel);
			UnsafeUtility.Free(m_CircleColliderBuffer, m_AllocatorLabel);
			UnsafeUtility.Free(m_FlipperColliderBuffer, m_AllocatorLabel);
			UnsafeUtility.Free(m_GateColliderBuffer, m_AllocatorLabel);
			UnsafeUtility.Free(m_Line3DColliderBuffer, m_AllocatorLabel);
			UnsafeUtility.Free(m_LineSlingshotColliderBuffer, m_AllocatorLabel);
			UnsafeUtility.Free(m_LineColliderBuffer, m_AllocatorLabel);
			UnsafeUtility.Free(m_LineZColliderBuffer, m_AllocatorLabel);
			UnsafeUtility.Free(m_PlungerColliderBuffer, m_AllocatorLabel);
			UnsafeUtility.Free(m_PointColliderBuffer, m_AllocatorLabel);
			UnsafeUtility.Free(m_SpinnerColliderBuffer, m_AllocatorLabel);
			UnsafeUtility.Free(m_TriangleColliderBuffer, m_AllocatorLabel);
			UnsafeUtility.Free(m_PlaneColliderBuffer, m_AllocatorLabel);

			m_LookupBuffer = null;
			m_CircleColliderBuffer = null;
			m_FlipperColliderBuffer = null;
			m_GateColliderBuffer = null;
			m_Line3DColliderBuffer = null;
			m_LineSlingshotColliderBuffer = null;
			m_LineColliderBuffer = null;
			m_LineZColliderBuffer = null;
			m_PlungerColliderBuffer = null;
			m_PointColliderBuffer = null;
			m_SpinnerColliderBuffer = null;
			m_TriangleColliderBuffer = null;
			m_PlaneColliderBuffer = null;
			m_Length = 0;
		}

#if ENABLE_UNITY_COLLECTIONS_CHECKS
		private void FailOutOfRangeError(int index)
		{
			if (index < Length && (m_MinIndex != 0 || m_MaxIndex != Length - 1))
				throw new IndexOutOfRangeException(string.Format(
					"Index {0} is out of restricted IJobParallelFor range [{1}...{2}] in ReadWriteBuffer.\n" +
					"ReadWriteBuffers are restricted to only read & write the element at the job index. " +
					"You can use double buffering strategies to avoid race conditions due to " +
					"reading & writing in parallel to the same elements from a job.",
					index, m_MinIndex, m_MaxIndex));

			throw new IndexOutOfRangeException($"Index {index} is out of range of '{Length}' Length.");
		}
#endif

		#endregion

		#region Collider Data

		public Aabb GetAabb(int index)
		{
			if (index < 0 || index >= m_Length) {
				throw new IndexOutOfRangeException($"Invalid index {index} when looking up collider.");
			}
			var lookup = UnsafeUtility.ReadArrayElement<ColliderLookup>(m_LookupBuffer, index);
			switch (lookup.Type) {
				case ColliderType.Circle: return UnsafeUtility.ArrayElementAsRef<CircleCollider>(m_CircleColliderBuffer, lookup.Index).Bounds.Aabb;
				case ColliderType.Flipper: return UnsafeUtility.ArrayElementAsRef<FlipperCollider>(m_FlipperColliderBuffer, lookup.Index).Bounds.Aabb;
				case ColliderType.Gate: return UnsafeUtility.ArrayElementAsRef<GateCollider>(m_GateColliderBuffer, lookup.Index).Bounds.Aabb;
				case ColliderType.Line3D: return UnsafeUtility.ArrayElementAsRef<Line3DCollider>(m_Line3DColliderBuffer, lookup.Index).Bounds.Aabb;
				case ColliderType.LineSlingShot: return UnsafeUtility.ArrayElementAsRef<LineSlingshotCollider>(m_LineSlingshotColliderBuffer, lookup.Index).Bounds.Aabb;
				case ColliderType.Line: return UnsafeUtility.ArrayElementAsRef<LineCollider>(m_LineColliderBuffer, lookup.Index).Bounds.Aabb;
				case ColliderType.LineZ: return UnsafeUtility.ArrayElementAsRef<LineZCollider>(m_LineZColliderBuffer, lookup.Index).Bounds.Aabb;
				case ColliderType.Plunger: return UnsafeUtility.ArrayElementAsRef<PlungerCollider>(m_PlungerColliderBuffer, lookup.Index).Bounds.Aabb;
				case ColliderType.Point: return UnsafeUtility.ArrayElementAsRef<PointCollider>(m_PointColliderBuffer, lookup.Index).Bounds.Aabb;
				case ColliderType.Spinner: return UnsafeUtility.ArrayElementAsRef<SpinnerCollider>(m_SpinnerColliderBuffer, lookup.Index).Bounds.Aabb;
				case ColliderType.Triangle: return UnsafeUtility.ArrayElementAsRef<TriangleCollider>(m_TriangleColliderBuffer, lookup.Index).Bounds.Aabb;
				case ColliderType.Plane: return UnsafeUtility.ArrayElementAsRef<PlaneCollider>(m_PlaneColliderBuffer, lookup.Index).Bounds.Aabb;
			}
			throw new ArgumentException($"Unknown lookup type.");
		}

		public bool IsTransformed(int index) => GetHeader(index).IsTransformed;
		public int GetItemId(int index) => GetHeader(index).ItemId;
		public ItemType GetItemType(int index) => GetHeader(index).ItemType;

		public ref ColliderHeader GetHeader(int index)
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if (index < 0 || index >= m_Length) {
				throw new IndexOutOfRangeException($"Invalid index {index} when looking up collider.");
			}
#endif
			ref var lookup = ref UnsafeUtility.ArrayElementAsRef<ColliderLookup>(m_LookupBuffer, index);
			switch (lookup.Type) {
				case ColliderType.Circle: return ref UnsafeUtility.ArrayElementAsRef<CircleCollider>(m_CircleColliderBuffer, lookup.Index).Header;
				case ColliderType.Flipper: return ref UnsafeUtility.ArrayElementAsRef<FlipperCollider>(m_FlipperColliderBuffer, lookup.Index).Header;
				case ColliderType.Gate: return ref UnsafeUtility.ArrayElementAsRef<GateCollider>(m_GateColliderBuffer, lookup.Index).Header;
				case ColliderType.Line3D: return ref UnsafeUtility.ArrayElementAsRef<Line3DCollider>(m_Line3DColliderBuffer, lookup.Index).Header;
				case ColliderType.LineSlingShot: return ref UnsafeUtility.ArrayElementAsRef<LineSlingshotCollider>(m_LineSlingshotColliderBuffer, lookup.Index).Header;
				case ColliderType.Line: return ref UnsafeUtility.ArrayElementAsRef<LineCollider>(m_LineColliderBuffer, lookup.Index).Header;
				case ColliderType.LineZ: return ref UnsafeUtility.ArrayElementAsRef<LineZCollider>(m_LineZColliderBuffer, lookup.Index).Header;
				case ColliderType.Plunger: return ref UnsafeUtility.ArrayElementAsRef<PlungerCollider>(m_PlungerColliderBuffer, lookup.Index).Header;
				case ColliderType.Point: return ref UnsafeUtility.ArrayElementAsRef<PointCollider>(m_PointColliderBuffer, lookup.Index).Header;
				case ColliderType.Spinner: return ref UnsafeUtility.ArrayElementAsRef<SpinnerCollider>(m_SpinnerColliderBuffer, lookup.Index).Header;
				case ColliderType.Triangle: return ref UnsafeUtility.ArrayElementAsRef<TriangleCollider>(m_TriangleColliderBuffer, lookup.Index).Header;
				case ColliderType.Plane: return ref UnsafeUtility.ArrayElementAsRef<PlaneCollider>(m_PlaneColliderBuffer, lookup.Index).Header;
			}
			throw new ArgumentException($"Unknown lookup type.");
		}

		#endregion

		#region Debug

		public ICollider[] ToArray()
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
			var array = new ICollider[Length];
			for (var i = 0; i < Length; i++) {
				ref var lookup = ref UnsafeUtility.ArrayElementAsRef<ColliderLookup>(m_LookupBuffer, i);
				switch (lookup.Type) {
					case ColliderType.Circle:
						array[i] = UnsafeUtility.ReadArrayElement<CircleCollider>(m_CircleColliderBuffer, lookup.Index);
						break;
					case ColliderType.Flipper:
						array[i] = UnsafeUtility.ReadArrayElement<FlipperCollider>(m_FlipperColliderBuffer, lookup.Index);
						break;
					case ColliderType.Gate:
						array[i] = UnsafeUtility.ReadArrayElement<GateCollider>(m_GateColliderBuffer, lookup.Index);
						break;
					case ColliderType.Line3D:
						array[i] = UnsafeUtility.ReadArrayElement<Line3DCollider>(m_Line3DColliderBuffer, lookup.Index);
						break;
					case ColliderType.LineSlingShot:
						array[i] = UnsafeUtility.ReadArrayElement<LineSlingshotCollider>(m_LineSlingshotColliderBuffer, lookup.Index);
						break;
					case ColliderType.Line:
						array[i] = UnsafeUtility.ReadArrayElement<LineCollider>(m_LineColliderBuffer, lookup.Index);
						break;
					case ColliderType.LineZ:
						array[i] = UnsafeUtility.ReadArrayElement<LineZCollider>(m_LineZColliderBuffer, lookup.Index);
						break;
					case ColliderType.Plunger:
						array[i] = UnsafeUtility.ReadArrayElement<PlungerCollider>(m_PlungerColliderBuffer, lookup.Index);
						break;
					case ColliderType.Point:
						array[i] = UnsafeUtility.ReadArrayElement<PointCollider>(m_PointColliderBuffer, lookup.Index);
						break;
					case ColliderType.Spinner:
						array[i] = UnsafeUtility.ReadArrayElement<SpinnerCollider>(m_SpinnerColliderBuffer, lookup.Index);
						break;
					case ColliderType.Triangle:
						array[i] = UnsafeUtility.ReadArrayElement<TriangleCollider>(m_TriangleColliderBuffer, lookup.Index);
						break;
					case ColliderType.Plane:
						array[i] = UnsafeUtility.ReadArrayElement<PlaneCollider>(m_PlaneColliderBuffer, lookup.Index);
						break;
				}
			}
			return array;
		}

		#endregion
	}

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

	// Visualizes the colliders in the C# debugger
	internal sealed class NativeCollidersDebugView
	{
		private NativeColliders _nativeColliders;

		public NativeCollidersDebugView(NativeColliders nativeColliders)
		{
			_nativeColliders = nativeColliders;
		}
		public ICollider[] Colliders => _nativeColliders.ToArray();
	}
}
