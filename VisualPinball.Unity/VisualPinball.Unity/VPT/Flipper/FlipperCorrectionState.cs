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
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace VisualPinball.Unity
{
	public unsafe struct FlipperCorrectionState : IDisposable
	{
		public readonly bool IsEnabled;
		public readonly int FlipperColliderId;
		public readonly int FlipperItemId;
		public readonly uint TimeDelayMs;

		[NativeDisableUnsafePtrRestriction] private void* _polarities;
		[NativeDisableUnsafePtrRestriction] private void* _velocities;

		private readonly int _numPolarities;
		private readonly int _numVelocities;

		public FlipperCorrectionState(bool isEnabled, int flipperItemId, int flipperColliderId, uint timeDelayMs, float2[] polarities, float2[] velocities, Allocator allocator)
		{
			IsEnabled = isEnabled;
			FlipperItemId = flipperItemId;
			FlipperColliderId = flipperColliderId;
			TimeDelayMs = timeDelayMs;
			_polarities = Allocate(polarities, allocator);
			_velocities = Allocate(velocities, allocator);
			_numPolarities = polarities.Length;
			_numVelocities = velocities.Length;
		}

		public UnmanagedArray<float2> Velocities => new(_velocities, _numVelocities);
		public UnmanagedArray<float2> Polarities => new(_polarities, _numPolarities);

		private static void* Allocate(float2[] src, Allocator allocator)
		{
			var na = new NativeArray<float2>(src, Allocator.Temp);
			var size = UnsafeUtility.SizeOf<float2>() * src.Length;
			var dest = UnsafeUtility.Malloc(size, UnsafeUtility.AlignOf<float2>(), allocator);
			UnsafeUtility.MemCpy(dest, na.GetUnsafeReadOnlyPtr(), size);
			na.Dispose();

			return dest;
		}

		public void Dispose()
		{
			UnsafeUtility.Free(_velocities, Allocator.None);
			UnsafeUtility.Free(_polarities, Allocator.None);

			_polarities = null;
			_velocities = null;
		}
	}
}
