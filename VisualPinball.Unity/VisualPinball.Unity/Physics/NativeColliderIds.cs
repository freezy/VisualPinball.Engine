using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace VisualPinball.Unity
{
	public unsafe struct NativeColliderIds : IDisposable
	{
		public readonly int Length;
		private void* _buffer;

		private readonly Allocator _allocator;

		public NativeColliderIds(NativeList<int> colliderIds, Allocator allocator)
		{
			_allocator = allocator;
			Length = colliderIds.Length;
			long size = UnsafeUtility.SizeOf<int>() * Length;
			_buffer = UnsafeUtility.Malloc(size, UnsafeUtility.AlignOf<int>(), allocator);
			UnsafeUtility.MemCpy(_buffer, colliderIds.GetUnsafePtr(), size);
		}

		public int this[int index]
		{
			get {
				if (index < 0 || index >= Length) {
					throw new IndexOutOfRangeException();
				}
				return UnsafeUtility.ReadArrayElement<int>(_buffer, index);
			}
		}

		public void Dispose()
		{
			UnsafeUtility.Free(_buffer, _allocator);
			_buffer = null;
		}
	}
}
