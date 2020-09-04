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
	internal static class UnsafeEx
	{
		internal static unsafe int CalculateOffset<T, U>(ref T value, ref U baseValue)
			where T : struct
			where U : struct
		{
			return (int) ((byte*) UnsafeUtility.AddressOf(ref value) - (byte*) UnsafeUtility.AddressOf(ref baseValue));
		}

		internal static unsafe int CalculateOffset<T>(void* value, ref T baseValue)
			where T : struct
		{
			return (int) ((byte*) value - (byte*)UnsafeUtility.AddressOf(ref baseValue));
		}
	}
}
