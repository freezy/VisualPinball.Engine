using System;
using Unity.Collections;
using VisualPinball.Unity.Collections;
using System.Collections.Generic;
using UnityEngine;

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
			if (!_bitLookup.ContainsKey(ballId)) {
				return;
			}
			var bitIndex = _bitLookup[ballId];
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
			return _insideOfs.ContainsKey(itemId) && _insideOfs[itemId].IsSet(GetBitIndex(ballId));
		}

		internal bool IsOutsideOf(int itemId, int ballId) => !IsInsideOf(itemId, ballId);

		internal int GetInsideCount(int itemId)
		{
			if (!_insideOfs.ContainsKey(itemId)) {
				return 0;
			}

			return _insideOfs[itemId].CountBits();
		}

		internal bool IsEmpty(int itemId)
		{
			if (!_insideOfs.ContainsKey(itemId)) {
				return true;
			}

			return !_insideOfs[itemId].TestAny(0, 64);
		}

		internal List<int> GetIdsOfBallsInsideItem(int itemId)
		{
			var ballIds = new List<int>();
			if (!_insideOfs.ContainsKey(itemId)) {
				return ballIds;
			}

			ref var bits = ref _insideOfs.GetValueByRef(itemId);
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
			if (_bitLookup.ContainsKey(ballId)) {
				return _bitLookup[ballId];
			}

			var bitArrayIndices = _bitLookup.GetValueArray(Allocator.Temp); // todo don't copy but ref
			for (var i = 0; i < 64; i++) {
				if (bitArrayIndices.Contains(i)) {
					continue;
				}
				_bitLookup[ballId] = i;
				bitArrayIndices.Dispose();
				return i;
			}
			bitArrayIndices.Dispose();
			using var ballIds = _bitLookup.GetKeyArray(Allocator.Temp);
			throw new IndexOutOfRangeException($"Bit index in InsideOfs is full, currently stored ball IDs: {string.Join(", ", ballIds)}");
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
