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

using System;
using System.Runtime.InteropServices;

namespace VisualPinball.Unity
{
	internal static class MemHelper
	{
		public static CopiedPtr<byte[]> ToByteArray(IntPtr ptr, int len)
		{
			var data = new byte[len];
			Marshal.Copy(ptr, data, 0, len);
			return new CopiedPtr<byte[]>(ptr, data);
		}

		public static IntPtr FromByteArray(byte[] bytes)
		{
			var dataPtr = Marshal.AllocHGlobal(bytes.Length);
			Marshal.Copy(bytes, 0, dataPtr, bytes.Length);

			return dataPtr;
		}

		public static T ToObj<T>(IntPtr ptr) where T : class
		{
			var handle = (GCHandle) ptr;
			return handle.Target as T;
		}

		public static IntPtr ToIntPtr(object item)
		{
			if (item != null) {
				var gcHandle = GCHandle.Alloc(item);
				return (IntPtr) gcHandle;
			}
			return new IntPtr();
		}
	}

	internal class CopiedPtr<T> : IDisposable
	{
		private readonly IntPtr _ptr;

		public readonly T Value;

		public CopiedPtr(IntPtr ptr, T value)
		{
			_ptr = ptr;
			Value = value;
		}

		public void Dispose()
		{
			Marshal.FreeHGlobal(_ptr);
		}
	}
}
