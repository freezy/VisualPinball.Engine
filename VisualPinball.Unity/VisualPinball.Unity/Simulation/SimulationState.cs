// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
// https://github.com/freezy/VisualPinball.Engine
//
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace VisualPinball.Unity.Simulation
{
	/// <summary>
	/// Shared simulation state between simulation thread and Unity main thread.
	/// Uses double-buffering for lock-free reads.
	/// </summary>
	public class SimulationState : IDisposable
	{
		/// <summary>
		/// Maximum number of coils/solenoids supported
		/// </summary>
		private const int MaxCoils = 64;

		/// <summary>
		/// Maximum number of lamps supported
		/// </summary>
		private const int MaxLamps = 256;

		/// <summary>
		/// Maximum number of GI strings supported
		/// </summary>
		private const int MaxGIStrings = 8;

		#region State Structures

		/// <summary>
		/// Coil state (solenoid)
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		public struct CoilState
		{
			public int Id;
			public byte IsActive;  // 0 = off, 1 = on
			public byte _padding1;
			public short _padding2;
		}

		/// <summary>
		/// Lamp state
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		public struct LampState
		{
			public int Id;
			public float Value;  // 0.0 - 1.0 brightness
		}

		/// <summary>
		/// GI (General Illumination) state
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		public struct GIState
		{
			public int Id;
			public float Value;  // 0.0 - 1.0 brightness
		}

		/// <summary>
		/// Complete simulation state snapshot
		/// </summary>
		public struct Snapshot
		{
			// Timing
			public long SimulationTimeUsec;
			public long RealTimeUsec;

			// PinMAME state
			public NativeArray<CoilState> CoilStates;
			public NativeArray<LampState> LampStates;
			public NativeArray<GIState> GIStates;

			// Physics state references (not copied, just references)
			// The actual PhysicsState is too large to copy every tick
			// Instead, we'll use versioning and the main thread will read directly
			public int PhysicsStateVersion;

			public void Allocate()
			{
				CoilStates = new NativeArray<CoilState>(MaxCoils, Allocator.Persistent);
				LampStates = new NativeArray<LampState>(MaxLamps, Allocator.Persistent);
				GIStates = new NativeArray<GIState>(MaxGIStrings, Allocator.Persistent);
			}

			public void Dispose()
			{
				if (CoilStates.IsCreated) CoilStates.Dispose();
				if (LampStates.IsCreated) LampStates.Dispose();
				if (GIStates.IsCreated) GIStates.Dispose();
			}
		}

		#endregion

		#region Fields

		// Double-buffered snapshots
		private Snapshot _backBuffer;
		private Snapshot _frontBuffer;

		// Atomic pointer swap for lock-free reading
		private volatile int _currentFrontBuffer = 0; // 0 = _frontBuffer, 1 = _backBuffer

		private bool _disposed = false;

		#endregion

		#region Constructor / Dispose

		public SimulationState()
		{
			_frontBuffer.Allocate();
			_backBuffer.Allocate();
		}

		public void Dispose()
		{
			if (_disposed) return;
			_disposed = true;

			_frontBuffer.Dispose();
			_backBuffer.Dispose();
		}

		#endregion

		#region Write (Simulation Thread)

		/// <summary>
		/// Get the back buffer for writing.
		/// Called by simulation thread only.
		/// </summary>
		public ref Snapshot GetBackBuffer()
		{
			return ref (_currentFrontBuffer == 0 ? ref _backBuffer : ref _frontBuffer);
		}

		/// <summary>
		/// Swap buffers atomically.
		/// Called by simulation thread after writing to back buffer.
		/// </summary>
		public void SwapBuffers()
		{
			// Atomic swap
			_currentFrontBuffer = 1 - _currentFrontBuffer;
		}

		#endregion

		#region Read (Main Thread)

		/// <summary>
		/// Get the front buffer for reading.
		/// Called by Unity main thread only.
		/// Lock-free read.
		/// </summary>
		public ref readonly Snapshot GetFrontBuffer()
		{
			return ref (_currentFrontBuffer == 0 ? ref _frontBuffer : ref _backBuffer);
		}

		#endregion
	}
}
