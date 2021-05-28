// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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
using VisualPinball.Engine.VPT.Trough;

namespace VisualPinball.Unity
{
	public static class TableLoader
	{
		public static TableHolder LoadTable(string path)
		{
			var th = TableHolder.Load(path, false);

			var job = new GameItemJob(th.Table.Data.NumGameItems);
			var gameItems = Engine.VPT.Table.TableLoader.ReadGameItems(path, th.Table.Data.NumGameItems);
			for (var i = 0; i < th.Table.Data.NumGameItems; i++) {
				job.Data[i] = MemHelper.FromByteArray(gameItems[i]);
				job.DataLength[i] = gameItems[i].Length;
			}

			// parse threaded
			var handle = job.Schedule(th.Table.Data.NumGameItems, 64);
			handle.Complete();

			// update table with results
			for (var i = 0; i < th.Table.Data.NumGameItems; i++) {
				if (job.ItemObj[i].ToInt32() > 0) {
					var objHandle = (GCHandle) job.ItemObj[i];
					switch ((ItemType)job.ItemType[i]) {
						case ItemType.Bumper: {
							th.Add(objHandle.Target as Bumper);
							break;
						}
						case ItemType.Decal: {
							th.Add(objHandle.Target as Decal);
							break;
						}
						case ItemType.DispReel: {
							th.Add(objHandle.Target as DispReel);
							break;
						}
						case ItemType.Flasher: {
							th.Add(objHandle.Target as Flasher);
							break;
						}
						case ItemType.Flipper: {
							th.Add(objHandle.Target as Flipper);
							break;
						}
						case ItemType.Gate: {
							th.Add(objHandle.Target as Gate);
							break;
						}
						case ItemType.HitTarget: {
							th.Add(objHandle.Target as HitTarget);
							break;
						}
						case ItemType.Kicker: {
							th.Add(objHandle.Target as Kicker);
							break;
						}
						case ItemType.Light: {
							th.Add(objHandle.Target as Light);
							break;
						}
						case ItemType.LightSeq: {
							th.Add(objHandle.Target as LightSeq);
							break;
						}
						case ItemType.Plunger: {
							th.Add(objHandle.Target as Plunger);
							break;
						}
						case ItemType.Primitive: {
							th.Add(objHandle.Target as Primitive);
							break;
						}
						case ItemType.Ramp: {
							th.Add(objHandle.Target as Ramp);
							break;
						}
						case ItemType.Rubber: {
							th.Add(objHandle.Target as Rubber);
							break;
						}
						case ItemType.Spinner: {
							th.Add(objHandle.Target as Spinner);
							break;
						}
						case ItemType.Surface: {
							th.Add(objHandle.Target as Surface);
							break;
						}
						case ItemType.TextBox: {
							th.Add(objHandle.Target as TextBox);
							break;
						}
						case ItemType.Timer: {
							th.Add(objHandle.Target as Timer);
							break;
						}
						case ItemType.Trigger: {
							th.Add(objHandle.Target as Trigger);
							break;
						}
						case ItemType.Trough: {
							th.Add(objHandle.Target as Trough);
							break;
						}
						default:
							throw new ArgumentException("Unknown item type " + (ItemType)job.ItemType[i] + ".");
					}
				}
			}
			th.Table.SetupPlayfieldMesh();
			job.Dispose();

			return th;
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
