using System;
using System.Runtime.InteropServices;
using NLog;
using Unity.Collections;
using Unity.Jobs;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity.Importer
{
	internal struct GameItemImportJob : IJobParallelFor, IDisposable
	{
		[ReadOnly]
		public NativeArray<IntPtr> Data;

		[ReadOnly]
		public NativeArray<int> DataLength;

		[WriteOnly]
		public NativeArray<int> ItemType;

		[WriteOnly]
		public NativeArray<IntPtr> ItemObj;

		public void Execute(int index)
		{
			var data = new byte[DataLength[index]];
			Marshal.Copy(Data[index], data, 0, DataLength[index]);

			ItemObj[index] = TableLoader.LoadGameItem(data, index, out var itemType);
			ItemType[index] = itemType;

			Marshal.FreeHGlobal(Data[index]);
		}

		public void Dispose()
		{
		}
	}
}
