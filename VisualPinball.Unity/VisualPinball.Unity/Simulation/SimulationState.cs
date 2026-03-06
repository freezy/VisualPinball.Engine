// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
// https://github.com/freezy/VisualPinball.Engine
//
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Collections;
using Unity.Mathematics;

namespace VisualPinball.Unity.Simulation
{
	/// <summary>
	/// Shared simulation state between simulation thread and Unity main thread.
	/// Uses triple-buffering for truly lock-free reads: the sim thread always
	/// writes to its own buffer, publishes via atomic exchange, and the main
	/// thread acquires the latest published buffer — neither thread ever
	/// touches the other's active buffer.
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

		/// <summary>
		/// Maximum number of balls tracked per snapshot
		/// </summary>
		internal const int MaxBalls = 32;

		/// <summary>
		/// Maximum number of float-animated items (flippers, gates, spinners,
		/// plungers, drop targets, hit targets, triggers, bumper rings)
		/// </summary>
		internal const int MaxFloatAnimations = 128;

		/// <summary>
		/// Maximum number of float2-animated items (bumper skirts)
		/// </summary>
		internal const int MaxFloat2Animations = 16;

		#region Animation Snapshot Structures

		/// <summary>
		/// Per-ball snapshot for lock-free rendering.
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		public struct BallSnapshot
		{
			public int Id;
			public float3 Position;
			public float Radius;
			public byte IsFrozen; // 0 = no, 1 = yes
			public float3x3 Orientation; // BallOrientationForUnity
		}

		/// <summary>
		/// Per-item float animation value (flipper angle, gate angle, etc.)
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		public struct FloatAnimation
		{
			public int ItemId;
			public float Value;
		}

		/// <summary>
		/// Per-item float2 animation value (bumper skirt rotation)
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		public struct Float2Animation
		{
			public int ItemId;
			public float2 Value;
		}

		#endregion

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
		/// Complete simulation state snapshot including animation data for
		/// lock-free visual updates.
		/// </summary>
		public struct Snapshot
		{
			// Timing
			public long SimulationTimeUsec;
			public long RealTimeUsec;

			// PinMAME state
			public NativeArray<CoilState> CoilStates;
			public int CoilCount;
			public NativeArray<LampState> LampStates;
			public int LampCount;
			public NativeArray<GIState> GIStates;
			public int GICount;

			// Physics state references (not copied, just references)
			// The actual PhysicsState is too large to copy every tick
			// Instead, we'll use versioning and the main thread will read directly
			public int PhysicsStateVersion;

			// --- Animation snapshot data (filled by sim thread) ---

			public NativeArray<BallSnapshot> BallSnapshots;
			public int BallCount;

			public NativeArray<FloatAnimation> FloatAnimations;
			public int FloatAnimationCount;

			public NativeArray<Float2Animation> Float2Animations;
			public int Float2AnimationCount;

			public void Allocate()
			{
				CoilStates = new NativeArray<CoilState>(MaxCoils, Allocator.Persistent);
				LampStates = new NativeArray<LampState>(MaxLamps, Allocator.Persistent);
				GIStates = new NativeArray<GIState>(MaxGIStrings, Allocator.Persistent);
				CoilCount = 0;
				LampCount = 0;
				GICount = 0;
				BallSnapshots = new NativeArray<BallSnapshot>(MaxBalls, Allocator.Persistent);
				FloatAnimations = new NativeArray<FloatAnimation>(MaxFloatAnimations, Allocator.Persistent);
				Float2Animations = new NativeArray<Float2Animation>(MaxFloat2Animations, Allocator.Persistent);
				BallCount = 0;
				FloatAnimationCount = 0;
				Float2AnimationCount = 0;
			}

			public void Dispose()
			{
				if (CoilStates.IsCreated) CoilStates.Dispose();
				if (LampStates.IsCreated) LampStates.Dispose();
				if (GIStates.IsCreated) GIStates.Dispose();
				if (BallSnapshots.IsCreated) BallSnapshots.Dispose();
				if (FloatAnimations.IsCreated) FloatAnimations.Dispose();
				if (Float2Animations.IsCreated) Float2Animations.Dispose();
			}
		}

		#endregion

		#region Fields

		// Triple-buffered snapshots
		private Snapshot _buffer0;
		private Snapshot _buffer1;
		private Snapshot _buffer2;

		/// <summary>
		/// Index of the buffer the sim thread is currently writing to.
		/// Only the sim thread reads/writes this field.
		/// </summary>
		private int _writeIndex;

		/// <summary>
		/// Index of the most recently published buffer.
		/// Shared between threads — accessed only via Interlocked.Exchange.
		/// </summary>
		private int _readyIndex;

		/// <summary>
		/// Index of the buffer the main thread is currently reading from.
		/// Only the main thread reads/writes this field.
		/// </summary>
		private int _readIndex;

		private bool _disposed;

		#endregion

		#region Constructor / Dispose

		public SimulationState()
		{
			_buffer0.Allocate();
			_buffer1.Allocate();
			_buffer2.Allocate();

			// Sim thread starts writing to 0, published ("ready") starts as 1,
			// main thread starts reading from 2.
			_writeIndex = 0;
			_readyIndex = 1;
			_readIndex = 2;
		}

		public void Dispose()
		{
			if (_disposed) return;
			_disposed = true;

			_buffer0.Dispose();
			_buffer1.Dispose();
			_buffer2.Dispose();
		}

		#endregion

		#region Write (Simulation Thread)

		/// <summary>
		/// Get the current write buffer.
		/// </summary>
		/// <remarks><b>Thread:</b> Simulation thread only.</remarks>
		internal ref Snapshot GetWriteBuffer()
		{
			return ref GetBufferByIndex(_writeIndex);
		}

		/// <summary>
		/// Publish the write buffer so the main thread can pick it up, and
		/// reclaim the previously-ready buffer as the new write target.
		/// Allocation-free.
		/// </summary>
		/// <remarks><b>Thread:</b> Simulation thread only.</remarks>
		internal void PublishWriteBuffer()
		{
			// Atomically swap _readyIndex with our _writeIndex.
			// After this, the old ready buffer becomes our new write buffer,
			// and the data we just wrote is now the ready buffer.
			_writeIndex = Interlocked.Exchange(ref _readyIndex, _writeIndex);
		}

		#endregion

		#region Read (Main Thread)

		/// <summary>
		/// Acquire the latest published snapshot for reading.
		/// Returns a ref to the acquired buffer that is safe to read until the
		/// next call to <see cref="AcquireReadBuffer"/>.
		/// Allocation-free.
		/// </summary>
		/// <remarks><b>Thread:</b> Main thread only.</remarks>
		internal ref readonly Snapshot AcquireReadBuffer()
		{
			// Atomically swap _readyIndex with our _readIndex.
			// After this we own what was the ready buffer (latest data), and
			// our previous read buffer goes back into the ready slot (which
			// the sim thread may reclaim as write).
			_readIndex = Interlocked.Exchange(ref _readyIndex, _readIndex);
			return ref GetBufferByIndex(_readIndex);
		}

		/// <summary>
		/// Peek at the current read buffer without swapping.
		/// Useful when you just need to re-read the last acquired snapshot.
		/// </summary>
		/// <remarks><b>Thread:</b> Main thread only.</remarks>
		internal ref readonly Snapshot PeekReadBuffer()
		{
			return ref GetBufferByIndex(_readIndex);
		}

		#endregion

		#region Helpers

		private ref Snapshot GetBufferByIndex(int index)
		{
			switch (index) {
				case 0: return ref _buffer0;
				case 1: return ref _buffer1;
				case 2: return ref _buffer2;
				default: throw new IndexOutOfRangeException($"Invalid buffer index {index}");
			}
		}

		#endregion
	}
}
