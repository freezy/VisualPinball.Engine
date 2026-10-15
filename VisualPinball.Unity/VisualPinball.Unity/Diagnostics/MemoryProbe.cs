// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
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
using System.IO;
using System.Runtime.InteropServices;
using NLog;
using Unity.Profiling;
using UnityEngine;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Reads how much memory the process and the GPU use. Called every few seconds, never
	/// per frame.
	/// </summary>
	///
	/// <remarks>
	/// RAM comes from the OS: working set and private bytes on Windows, the resident set from
	/// <c>/proc/self/statm</c> on Linux. Where that's not available (macOS, or when reading
	/// fails), Unity's "System Used Memory" counter, which is what the OS reports for the
	/// process, is used instead. VRAM usage and budget come from DXGI on Windows, read through
	/// the COM vtable directly, so no native plugin is needed. Elsewhere, and when DXGI can't
	/// be read, VRAM usage is unavailable (-1) and only the capacity from
	/// <see cref="SystemInfo"/> is known.
	/// </remarks>
	internal sealed class MemoryProbe : IDisposable
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private IntPtr _adapter;
		private QueryVideoMemoryInfoFn _queryVideoMemory;
		private bool _vramTried;

		private bool _osRamFailed;
		private FileStream _statm;
		private readonly byte[] _statmBuffer = new byte[128];
		private ProfilerRecorder _systemUsed;

		/// <summary>
		/// Process memory in bytes: the working (resident) set, or -1 if unknown, and private
		/// bytes where the OS tells (else -1).
		/// </summary>
		public void ReadProcess(out long workingSet, out long privateBytes)
		{
			privateBytes = -1;
			workingSet = -1;
			if (!_osRamFailed) {
				if (IsWindows) {
					try {
						var counters = new ProcessMemoryCounters { cb = (uint)Marshal.SizeOf<ProcessMemoryCounters>() };
						if (K32GetProcessMemoryInfo(GetCurrentProcess(), ref counters, counters.cb)) {
							workingSet = (long)(ulong)counters.WorkingSetSize;
							privateBytes = (long)(ulong)counters.PagefileUsage;
							return;
						}
					} catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException) {
						Logger.Debug(e, "Cannot read the process memory from Windows, using Unity's counter.");
						_osRamFailed = true;
					}

				} else if (IsLinux) {
					workingSet = ReadStatm();
					if (workingSet >= 0) {
						return;
					}
				}
			}

			// macOS, or the OS read failed: what Unity got from the OS
			if (!_systemUsed.Valid) {
				_systemUsed = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
			}
			var used = _systemUsed.Valid ? _systemUsed.LastValue : 0;
			workingSet = used > 0 ? used : -1;
		}

		/// <summary>
		/// The resident set from <c>/proc/self/statm</c> (second field, in pages), or -1. The file
		/// stays open and is read again from the start, into a buffer that's kept.
		/// </summary>
		private long ReadStatm()
		{
			try {
				if (_statm == null) {
					_statm = new FileStream("/proc/self/statm", FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1);
				}
				_statm.Seek(0, SeekOrigin.Begin);
				var length = _statm.Read(_statmBuffer, 0, _statmBuffer.Length);
				var field = 0;
				long value = 0;
				var inNumber = false;
				for (var i = 0; i < length; i++) {
					var c = _statmBuffer[i];
					if (c >= '0' && c <= '9') {
						value = value * 10 + (c - '0');
						inNumber = true;
						continue;
					}
					if (inNumber) {
						if (field == 1) {
							return value * Environment.SystemPageSize;
						}
						field++;
						value = 0;
						inNumber = false;
					}
				}
				if (inNumber && field == 1) {
					return value * Environment.SystemPageSize;
				}

			} catch (Exception e) {
				Logger.Debug(e, "Cannot read /proc/self/statm, using Unity's counter for the process memory.");
			}
			_osRamFailed = true;
			_statm?.Dispose();
			_statm = null;
			return -1;
		}

		/// <summary>
		/// GPU memory this process uses and the budget the OS gives it, in bytes, or -1 when
		/// unavailable.
		/// </summary>
		public void ReadVram(out long usage, out long budget)
		{
			usage = -1;
			budget = -1;
			if (!_vramTried) {
				_vramTried = true;
				OpenAdapter();
			}
			if (_queryVideoMemory == null) {
				return;
			}
			try {
				// node 0, local (dedicated) segment
				if (_queryVideoMemory(_adapter, 0, 0, out var info) == 0) {
					usage = (long)info.CurrentUsage;
					budget = (long)info.Budget;
				}
			} catch (Exception e) {
				Logger.Debug(e, "Reading the video memory usage failed, it won't be reported anymore.");
				_queryVideoMemory = null;
			}
		}

		public void Dispose()
		{
			_statm?.Dispose();
			_statm = null;
			if (_systemUsed.Valid) {
				_systemUsed.Dispose();
			}
			if (_adapter != IntPtr.Zero) {
				Marshal.Release(_adapter);
				_adapter = IntPtr.Zero;
			}
			_queryVideoMemory = null;
		}

		#region DXGI

		private void OpenAdapter()
		{
			if (!IsWindows) {
				return;
			}
			var type = SystemInfo.graphicsDeviceType;
			if (type != UnityEngine.Rendering.GraphicsDeviceType.Direct3D11 && type != UnityEngine.Rendering.GraphicsDeviceType.Direct3D12) {
				return;
			}
			IntPtr factory = IntPtr.Zero;
			try {
				var factoryId = new Guid("770aae78-f26f-4dba-a829-253c83d1b387"); // IDXGIFactory1
				if (CreateDXGIFactory1(ref factoryId, out factory) != 0 || factory == IntPtr.Zero) {
					return;
				}
				var enumAdapters1 = Method<EnumAdapters1Fn>(factory, 12);
				var vendor = (uint)SystemInfo.graphicsDeviceVendorID;
				var device = (uint)SystemInfo.graphicsDeviceID;

				// Unity doesn't tell which adapter it renders on, only its PCI ids. With two cards
				// of the same model, there's no telling which one it is, so rather report nothing
				// than the other card.
				var match = IntPtr.Zero;
				var matches = 0;
				for (uint i = 0; enumAdapters1(factory, i, out var adapter) == 0; i++) {
					var keep = false;
					try {
						var getDesc1 = Method<GetDesc1Fn>(adapter, 10);
						if (getDesc1(adapter, out var desc) == 0 && desc.VendorId == vendor && desc.DeviceId == device) {
							matches++;
							if (match == IntPtr.Zero) {
								match = adapter;
								keep = true;
							}
						}
					} finally {
						if (!keep) {
							Marshal.Release(adapter);
						}
					}
				}
				if (match == IntPtr.Zero) {
					Logger.Debug("No DXGI adapter matches the graphics device, video memory usage won't be reported.");
					return;
				}
				try {
					if (matches > 1) {
						Logger.Debug("{Count} identical GPUs, can't tell which one renders, video memory usage won't be reported.", matches);
						return;
					}
					var adapter3Id = new Guid("645967a4-1392-4310-a798-8053ce3e93fd"); // IDXGIAdapter3
					if (Marshal.QueryInterface(match, ref adapter3Id, out var adapter3) != 0 || adapter3 == IntPtr.Zero) {
						return;
					}
					_adapter = adapter3;
					_queryVideoMemory = Method<QueryVideoMemoryInfoFn>(adapter3, 14);
				} finally {
					Marshal.Release(match);
				}
			} catch (Exception e) {
				Logger.Debug(e, "Cannot open the DXGI adapter, video memory usage won't be reported.");
				Dispose();
			} finally {
				if (factory != IntPtr.Zero) {
					Marshal.Release(factory);
				}
			}
		}

		private static T Method<T>(IntPtr comObject, int slot) where T : Delegate
		{
			var vtable = Marshal.ReadIntPtr(comObject);
			return Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(vtable, slot * IntPtr.Size));
		}

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		private delegate int EnumAdapters1Fn(IntPtr factory, uint index, out IntPtr adapter);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		private delegate int GetDesc1Fn(IntPtr adapter, out AdapterDesc1 desc);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		private delegate int QueryVideoMemoryInfoFn(IntPtr adapter, uint nodeIndex, int memorySegmentGroup, out VideoMemoryInfo info);

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		private struct AdapterDesc1
		{
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
			public string Description;
			public uint VendorId;
			public uint DeviceId;
			public uint SubSysId;
			public uint Revision;
			public UIntPtr DedicatedVideoMemory;
			public UIntPtr DedicatedSystemMemory;
			public UIntPtr SharedSystemMemory;
			public long AdapterLuid;
			public uint Flags;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct VideoMemoryInfo
		{
			public ulong Budget;
			public ulong CurrentUsage;
			public ulong AvailableForReservation;
			public ulong CurrentReservation;
		}

		[DllImport("dxgi.dll")]
		private static extern int CreateDXGIFactory1(ref Guid riid, out IntPtr factory);

		#endregion

		#region Process memory

		[StructLayout(LayoutKind.Sequential)]
		private struct ProcessMemoryCounters
		{
			public uint cb;
			public uint PageFaultCount;
			public UIntPtr PeakWorkingSetSize;
			public UIntPtr WorkingSetSize;
			public UIntPtr QuotaPeakPagedPoolUsage;
			public UIntPtr QuotaPagedPoolUsage;
			public UIntPtr QuotaPeakNonPagedPoolUsage;
			public UIntPtr QuotaNonPagedPoolUsage;
			public UIntPtr PagefileUsage;
			public UIntPtr PeakPagefileUsage;
		}

		[DllImport("kernel32.dll")]
		private static extern IntPtr GetCurrentProcess();

		[DllImport("kernel32.dll")]
		private static extern bool K32GetProcessMemoryInfo(IntPtr process, ref ProcessMemoryCounters counters, uint size);

		#endregion

		private static bool IsWindows => Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor;
		private static bool IsLinux => Application.platform == RuntimePlatform.LinuxPlayer || Application.platform == RuntimePlatform.LinuxEditor;
	}
}
