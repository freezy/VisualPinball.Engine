using System;
using Unity.Collections;
using VisualPinball.Unity.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VisualPinball.Unity
{
	internal struct InsideOfs : IDisposable
	{
		private NativeParallelHashMap<int, int> _bitLookup;
		private NativeParallelHashMap<int, BitField64> _insideOfs;

		public InsideOfs(Allocator allocator)
		{
			_bitLookup = new NativeParallelHashMap<int, int>(64, allocator);
			_insideOfs = new NativeParallelHashMap<int, BitField64>(64, allocator);
		}

		internal void SetInsideOf(int itemId, int ballId)
		{
			if (!_insideOfs.ContainsKey(itemId)) {
				_insideOfs.Add(itemId, new BitField64());
			}

			ref var bits = ref _insideOfs.GetValueByRef(itemId);
			bits.SetBits(GetBitIndex(ballId), true);
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
			var maps = _insideOfs.GetValueArray(Allocator.Temp);
			var index = GetBitIndex(ballId);
			foreach (var ballIndices in maps) {
				if (!ballIndices.IsSet(index)) {
					continue;
				}
				maps.Dispose();
				return;
			}
			_bitLookup.Remove(ballId);
		}

		private int GetBitIndex(int ballId)
		{
			if (_bitLookup.ContainsKey(ballId)) {
				return _bitLookup[ballId];
			}

			var indices = _bitLookup.GetValueArray(Allocator.Temp);
			for (var i = 0; i < 64; i++) {
				if (indices.Contains(i)) {
					continue;
				}
				_bitLookup[ballId] = i;
				indices.Dispose();
				return i;
			}
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
