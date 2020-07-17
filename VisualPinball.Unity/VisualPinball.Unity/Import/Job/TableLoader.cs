// ReSharper disable PossibleNullReferenceException

using System;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Jobs;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Bumper;
using VisualPinball.Engine.VPT.Decal;
using VisualPinball.Engine.VPT.DispReel;
using VisualPinball.Engine.VPT.Flasher;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Engine.VPT.Gate;
using VisualPinball.Engine.VPT.HitTarget;
using VisualPinball.Engine.VPT.Kicker;
using VisualPinball.Engine.VPT.Light;
using VisualPinball.Engine.VPT.LightSeq;
using VisualPinball.Engine.VPT.Plunger;
using VisualPinball.Engine.VPT.Primitive;
using VisualPinball.Engine.VPT.Ramp;
using VisualPinball.Engine.VPT.Rubber;
using VisualPinball.Engine.VPT.Spinner;
using VisualPinball.Engine.VPT.Surface;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Engine.VPT.TextBox;
using VisualPinball.Engine.VPT.Timer;
using VisualPinball.Engine.VPT.Trigger;

namespace VisualPinball.Unity.Import.Job
{
	public static class TableLoader
	{
		public static Table LoadTable(string path)
		{
			var table = Table.Load(path, false);

			var job = new GameItemJob(table.Data.NumGameItems);
			var gameItems = Engine.VPT.Table.TableLoader.ReadGameItems(path, table.Data.NumGameItems);
			for (var i = 0; i < table.Data.NumGameItems; i++) {
				job.Data[i] = MemHelper.FromByteArray(gameItems[i]);
				job.DataLength[i] = gameItems[i].Length;
			}

			// parse threaded
			var handle = job.Schedule(table.Data.NumGameItems, 64);
			handle.Complete();

			// update table with results
			for (var i = 0; i < table.Data.NumGameItems; i++) {
				if (job.ItemObj[i].ToInt32() > 0) {
					var objHandle = (GCHandle) job.ItemObj[i];
					switch ((ItemType)job.ItemType[i]) {
						case ItemType.Bumper: {
							var item = objHandle.Target as Bumper;
							table.Bumpers[item.Name] = item;
							break;
						}
						case ItemType.Decal: {
							var item = objHandle.Target as Decal;
							table.Decals.Add(item);
							break;
						}
						case ItemType.DispReel: {
							var item = objHandle.Target as DispReel;
							table.DispReels[item.Name] = item;
							break;
						}
						case ItemType.Flasher: {
							var item = objHandle.Target as Flasher;
							table.Flashers[item.Name] = item;
							break;
						}
						case ItemType.Flipper: {
							var item = objHandle.Target as Flipper;
							table.Flippers[item.Name] = item;
							break;
						}
						case ItemType.Gate: {
							var item = objHandle.Target as Gate;
							table.Gates[item.Name] = item;
							break;
						}
						case ItemType.HitTarget: {
							var item = objHandle.Target as HitTarget;
							table.HitTargets[item.Name] = item;
							break;
						}
						case ItemType.Kicker: {
							var item = objHandle.Target as Kicker;
							table.Kickers[item.Name] = item;
							break;
						}
						case ItemType.Light: {
							var item = objHandle.Target as Light;
							table.Lights[item.Name] = item;
							break;
						}
						case ItemType.LightSeq: {
							var item = objHandle.Target as LightSeq;
							table.LightSeqs[item.Name] = item;
							break;
						}
						case ItemType.Plunger: {
							var item = objHandle.Target as Plunger;
							table.Plungers[item.Name] = item;
							break;
						}
						case ItemType.Primitive: {
							var item = objHandle.Target as Primitive;
							table.Primitives[item.Name] = item;
							break;
						}
						case ItemType.Ramp: {
							var item = objHandle.Target as Ramp;
							table.Ramps[item.Name] = item;
							break;
						}
						case ItemType.Rubber: {
							var item = objHandle.Target as Rubber;
							table.Rubbers[item.Name] = item;
							break;
						}
						case ItemType.Spinner: {
							var item = objHandle.Target as Spinner;
							table.Spinners[item.Name] = item;
							break;
						}
						case ItemType.Surface: {
							var item = objHandle.Target as Surface;
							table.Surfaces[item.Name] = item;
							break;
						}
						case ItemType.TextBox: {
							var item = objHandle.Target as TextBox;
							table.TextBoxes[item.Name] = item;
							break;
						}
						case ItemType.Timer: {
							var item = objHandle.Target as Timer;
							table.Timers[item.Name] = item;
							break;
						}
						case ItemType.Trigger: {
							var item = objHandle.Target as Trigger;
							table.Triggers[item.Name] = item;
							break;
						}
					}
				}
			}
			table.SetupPlayfieldMesh();
			job.Dispose();

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
				ItemType[index] = (int)itemType;
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
