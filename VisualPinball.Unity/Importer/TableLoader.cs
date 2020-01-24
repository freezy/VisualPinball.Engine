using System;
using System.Runtime.InteropServices;
using NLog;
using OpenMcdf;
using Unity.Collections;
using Unity.Jobs;
using VisualPinball.Engine.IO;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Primitive;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity.Importer
{

	public static class TableLoader
	{
		public static Table LoadTable(string path)
		{
			var table = Table.Load(path, false);
			var cf = new CompoundFile(path);
			Profiler.Start("LoadGameItems via Job");
			try {

				// pull in data from storage
				var storage = cf.RootStorage.GetStorage("GameStg");
				var gameItemJob = new GameItemImportJob(table.Data.NumGameItems);
				for (var i = 0; i < table.Data.NumGameItems; i++) {
					var itemName = $"GameItem{i}";
					var itemStream = storage.GetStream(itemName);
					var bytes = itemStream.GetData();

					var dataPtr = Marshal.AllocHGlobal(bytes.Length);
					Marshal.Copy(bytes, 0, dataPtr, bytes.Length);

					gameItemJob.Data[i] = dataPtr;
					gameItemJob.DataLength[i] = bytes.Length;
				}

				// parse threaded
				var handle = gameItemJob.Schedule(table.Data.NumGameItems, 64);
				handle.Complete();

				for (var i = 0; i < table.Data.NumGameItems; i++) {
					if (gameItemJob.ItemObj[i].ToInt32() > 0) {
						var objHandle = (GCHandle) gameItemJob.ItemObj[i];
						switch (gameItemJob.ItemType[i]) {
							case ItemType.Primitive: {
								var item = objHandle.Target as Primitive;
								table.Primitives[item.Name] = item;
								break;
							}
						}
						objHandle.Free();
					}
				}

				gameItemJob.Dispose();

			} finally {
				cf.Close();
				Profiler.Stop("LoadGameItems via Job");
			}

			return table;
		}
	}

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

		public GameItemImportJob(int size) : this()
		{
			const Allocator allocator = Allocator.Persistent;
			Data = new NativeArray<IntPtr>(size, allocator);
			DataLength = new NativeArray<int>(size, allocator);
			ItemObj = new NativeArray<IntPtr>(size, allocator);
			ItemType = new NativeArray<int>(size, allocator);
		}

		public void Execute(int index)
		{
			// copy storage data from unmanaged to managed
			using (var data = MemHelper.ToByteArray(Data[index], DataLength[index])) {

				// do the work, managed
				Engine.VPT.Table.TableLoader.LoadGameItem(data.Value, index, out var itemType, out var item);

				// convert result back to unmanaged
				ItemObj[index] = MemHelper.ToIntPtr(item);
				ItemType[index] = itemType;
			}
		}

		public void Dispose()
		{
			Data.Dispose();
			DataLength.Dispose();
			ItemType.Dispose();
			ItemObj.Dispose();
		}
	}
}
