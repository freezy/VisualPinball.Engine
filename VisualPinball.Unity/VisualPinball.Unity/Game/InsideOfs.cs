using System;
using Unity.Collections;

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
			
			_insideOfs[itemId].SetBits(GetBitIndex(ballId), true);
		}
		
		internal void SetOutsideOf(int itemId, int ballId)
		{
			if (!_insideOfs.ContainsKey(itemId)) {
				return;
			}
			
			_insideOfs[itemId].SetBits(GetBitIndex(ballId), false);
			ClearBitIndex(ballId);
			ClearItems(itemId);
		}
		
		internal bool IsInsideOf(int itemId, int ballId)
		{
			return _insideOfs.ContainsKey(itemId) && _insideOfs[itemId].IsSet(GetBitIndex(ballId));
		}
		
		internal bool IsOutsideOf(int itemId, int ballId) => !IsInsideOf(itemId, ballId);

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
			throw new IndexOutOfRangeException();
		}


		public void Dispose()
		{
			_bitLookup.Dispose();
			_insideOfs.Dispose();
		}
	}
}
