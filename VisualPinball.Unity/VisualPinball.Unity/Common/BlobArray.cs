// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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

using Unity.Collections.LowLevel.Unsafe;

namespace VisualPinball.Unity
{
	// Non-generic temporary stand-in for Unity BlobArray.
	// This is to work around C# wanting to treat any struct containing the generic Unity.BlobArray<T> as a managed struct.
	// Taken from Unity.Physics
	// TODO: Use Unity.Blobs instead
	internal struct BlobArray
	{
		internal int Offset;
		internal int Length;    // number of T, not number of bytes

		// Generic accessor
		public unsafe struct Accessor<T> where T : struct
		{
			private readonly int* m_OffsetPtr;
			public int Length { get; private set; }

			public Accessor(ref BlobArray blobArray)
			{
				fixed (BlobArray* ptr = &blobArray)
				{
					m_OffsetPtr = &ptr->Offset;
					Length = ptr->Length;
				}
			}

			public ref T this[int index] => ref UnsafeUtility.ArrayElementAsRef<T>((byte*)m_OffsetPtr + *m_OffsetPtr, index);

			public Enumerator GetEnumerator() => new Enumerator(m_OffsetPtr, Length);

			public struct Enumerator
			{
				private readonly int* m_OffsetPtr;
				private readonly int m_Length;
				private int m_Index;

				public T Current => UnsafeUtility.ArrayElementAsRef<T>((byte*)m_OffsetPtr + *m_OffsetPtr, m_Index);

				public Enumerator(int* offsetPtr, int length)
				{
					m_OffsetPtr = offsetPtr;
					m_Length = length;
					m_Index = -1;
				}

				public bool MoveNext()
				{
					return ++m_Index < m_Length;
				}
			}
		}
	}
}
