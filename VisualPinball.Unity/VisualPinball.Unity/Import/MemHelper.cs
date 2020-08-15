using System;
using System.Runtime.InteropServices;

namespace VisualPinball.Unity
{
	public static class MemHelper
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

	public class CopiedPtr<T> : IDisposable
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
