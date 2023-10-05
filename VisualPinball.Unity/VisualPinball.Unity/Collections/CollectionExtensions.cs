// MIT License
//
// Copyright (c) 2022 Timothy Raines
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace VisualPinball.Unity.Collections
{
	/// <summary>
	/// Part of Tertle's excellent Core extensions.
	///
	/// See https://gitlab.com/tertle/com.bovinelabs.core
	/// </summary>
	public static unsafe class CollectionExtensions
	{
		/// <summary>
		/// Returns a reference to the value associated with the specified key. Does not copy the value.
		/// </summary>
		/// <param name="map">The hashmap to retrieve the reference to the value from</param>
		/// <param name="key">The key of the hashmap pointing to the value</param>
		/// <typeparam name="TKey">Type of the hashmap's key</typeparam>
		/// <typeparam name="TValue">Type of the hashmap's value</typeparam>
		/// <returns>Reference to the value</returns>
		/// <see href="https://gitlab.com/tertle/com.bovinelabs.core/-/blob/master/BovineLabs.Core/Extensions/NativeParallelHashMapExtensions.cs?ref_type=heads#L383">Source</see>
		public static ref TValue GetValueByRef<TKey, TValue>(this NativeParallelHashMap<TKey, TValue> map, TKey key)
			where TKey : unmanaged, IEquatable<TKey>
			where TValue : unmanaged
		{
			return ref map.m_HashMapData.m_Buffer->GetValueByRef<TKey, TValue>(key);
		}

		/// <see href="https://gitlab.com/tertle/com.bovinelabs.core/-/blob/master/BovineLabs.Core/Extensions/UnsafeParallelHashMapDataExtensions.cs?ref_type=heads#L83">Source</see>
		private static ref TValue GetValueByRef<TKey, TValue>(this ref UnsafeParallelHashMapData data, TKey key)
			where TKey : struct, IEquatable<TKey>
			where TValue : struct
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if (data.allocatedIndexLength <= 0)
			{
				throw new KeyNotFoundException();
			}
#endif

			// First find the slot based on the hash
			var buckets = (int*)data.buckets;
			var bucket = key.GetHashCode() & data.bucketCapacityMask;
			var entryIdx = buckets[bucket];

			var nextPtrs = (int*)data.next;
			while (!UnsafeUtility.ReadArrayElement<TKey>(data.keys, entryIdx).Equals(key))
			{
				entryIdx = nextPtrs[entryIdx];
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				if ((entryIdx < 0) || (entryIdx >= data.keyCapacity))
				{
					throw new KeyNotFoundException("Cannot find key " + key);
				}
#endif
			}

			// Read the value
			return ref UnsafeUtility.ArrayElementAsRef<TValue>(data.values, entryIdx);
		}

		#region Own stuff

		public static ref T GetElementAsRef<T>(this NativeArray<T> array, int index) where T : unmanaged
		{
			return ref UnsafeUtility.ArrayElementAsRef<T>(array.GetUnsafePtr(), index);
		}

		public static ref T GetElementAsRef<T>(this NativeList<T> list, int index) where T : unmanaged
		{
			return ref UnsafeUtility.ArrayElementAsRef<T>(list.GetUnsafePtr(), index);
		}

		#endregion
	}
}
