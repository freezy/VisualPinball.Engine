using System;
using Unity.Collections;
using Unity.Mathematics;
using VisualPinball.Unity.Collections;

namespace VisualPinball.Unity
{
	internal struct InsideOfs : IDisposable
	{
		/**
		 * Stores at which position in the bit array(s) a ball is tracked. <br/>
		 *
		 * Key:   Ball ID
		 * Value: Index in the bit array
		 */
		private NativeParallelHashMap<int, int> _bitLookup;

		/**
		 * Stores which balls are inside of an item. <br/>
		 *
		 * Key:   Item ID
		 * Value: A bit array of ball IDs, up to 64 balls.
		 */
		private NativeParallelHashMap<int, BitField64> _insideOfs;

		public InsideOfs(Allocator allocator)
		{
			_bitLookup = new NativeParallelHashMap<int, int>(8, allocator);
			_insideOfs = new NativeParallelHashMap<int, BitField64>(16, allocator);
		}

		internal void SetInsideOf(int itemId, int ballId)
		{
			if (!_insideOfs.ContainsKey(itemId)) {
				_insideOfs.Add(itemId, new BitField64());
			}

			ref var bits = ref _insideOfs.GetValueByRef(itemId);
			bits.SetBits(GetBitIndex(ballId), true);
		}

		internal void SetOutsideOfAll(int ballId) // aka ball destroyed
		{
			if (!_bitLookup.TryGetValue(ballId, out var bitIndex)) {
				return;
			}
			using (var enumerator = _insideOfs.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					ref var ballIndices = ref enumerator.Current.Value;
					ballIndices.SetBits(bitIndex, false);
				}
			}

			_bitLookup.Remove(ballId);
		}

		internal void SetOutsideOf(int itemId, int ballId)
		{
			if (!_insideOfs.ContainsKey(itemId)) {
				return;
			}

			ref var bits = ref _insideOfs.GetValueByRef(itemId);
			bits.SetBits(GetBitIndex(ballId), false);
			ClearBitIndex(ballId);
			ClearItems(itemId);
		}

		internal bool IsInsideOf(int itemId, int ballId)
		{
			return _insideOfs.TryGetValue(itemId, out var bits) && bits.IsSet(GetBitIndex(ballId));
		}

		internal bool IsOutsideOf(int itemId, int ballId) => !IsInsideOf(itemId, ballId);

		internal int GetInsideCount(int itemId)
		{
			if (!_insideOfs.TryGetValue(itemId, out var bits)) {
				return 0;
			}

			return bits.CountBits();
		}

		internal bool IsEmpty(int itemId)
		{
			if (!_insideOfs.TryGetValue(itemId, out var bits)) {
				return true;
			}

			return !bits.TestAny(0, 64);
		}

		internal FixedList64Bytes<int> GetIdsOfBallsInsideItem(int itemId)
		{
			var ballIds = new FixedList64Bytes<int>();
			if (!_insideOfs.TryGetValue(itemId, out var bits)) {
				return ballIds;
			}

			for (int i = 0; i < 64; i++) {
				if (bits.IsSet(i)) {
					if (TryGetBallId(i, out var ballId)) {
						ballIds.Add(ballId);
					}
				}
			}

			return ballIds;
		}

		private void ClearItems(int itemId)
		{
			if (_insideOfs[itemId].GetBits(0, 64) == 0L) {
				_insideOfs.Remove(itemId);
			}
		}

		private void ClearBitIndex(int ballId)
		{
			var index = GetBitIndex(ballId);
			// for each item bitfield, check if the ball is in there. if not, remove the bit index
			using (var enumerator = _insideOfs.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					ref var ballIndices = ref enumerator.Current.Value;
					if (!ballIndices.IsSet(index)) {
						continue;
					}
					return;
				}
			}
			_bitLookup.Remove(ballId);
		}

		private int GetBitIndex(int ballId)
		{
			if (_bitLookup.TryGetValue(ballId, out var existingIndex)) {
				return existingIndex;
			}

			// Build a bitmask of occupied indices by iterating the map (no allocation).
			ulong occupied = 0;
			using (var enumerator = _bitLookup.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					occupied |= 1UL << enumerator.Current.Value;
				}
			}

			// Find the first zero bit (first free index).
			var free = ~occupied;
			if (free == 0) {
				throw new IndexOutOfRangeException("Bit index in InsideOfs is full.");
			}
			var newIndex = math.tzcnt(free);
			_bitLookup[ballId] = newIndex;
			return newIndex;
		}

		private bool TryGetBallId(int bitIndex, out int ballId)
		{
			foreach (var kvp in _bitLookup) {
				if (kvp.Value == bitIndex) {
					ballId = kvp.Key;
					return true;
				}
			}
			ballId = -1;
			return false;
		}

		public void Dispose()
		{
			_bitLookup.Dispose();
			_insideOfs.Dispose();
		}
	}
}