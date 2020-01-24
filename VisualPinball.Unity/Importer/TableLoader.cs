using System;
using System.Linq;
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
				var job = new GameItemJob(table.Data.NumGameItems);
				for (var i = 0; i < table.Data.NumGameItems; i++) {
					var itemName = $"GameItem{i}";
					var itemStream = storage.GetStream(itemName);
					var bytes = itemStream.GetData();

					job.Data[i] = MemHelper.FromByteArray(bytes);
					job.DataLength[i] = bytes.Length;
				}

				// parse threaded
				var handle = job.Schedule(table.Data.NumGameItems, 64);
				handle.Complete();

				// update table with results
				for (var i = 0; i < table.Data.NumGameItems; i++) {
					if (job.ItemObj[i].ToInt32() > 0) {
						var objHandle = (GCHandle) job.ItemObj[i];
						switch (job.ItemType[i]) {
							case ItemType.Primitive: {
								var item = objHandle.Target as Primitive;
								table.Primitives[item.Name] = item;
								break;
							}
						}
					}
				}
				job.Dispose();

			} finally {
				cf.Close();
				Profiler.Stop("LoadGameItems via Job");
			}

			return table;
		}
	}

	internal struct GameItemJob : IJobParallelFor, IDisposable
	{
		[ReadOnly]
		public NativeArray<IntPtr> Data;

		[ReadOnly]
		public NativeArray<int> DataLength;

		[WriteOnly]
		public NativeArray<int> ItemType;

		[WriteOnly]
		public NativeArray<IntPtr> ItemObj;

		public GameItemJob(int size) : this()
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
			foreach (var ptr in ItemObj.Where(ptr => ptr.ToInt32() > 0)) {
				((GCHandle)ptr).Free();
			}
			Data.Dispose();
			DataLength.Dispose();
			ItemType.Dispose();
			ItemObj.Dispose();
		}
	}
}
