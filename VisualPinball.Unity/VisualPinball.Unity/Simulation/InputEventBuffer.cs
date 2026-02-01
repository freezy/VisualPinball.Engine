// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
// https://github.com/freezy/VisualPinball.Engine
//
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Threading;

namespace VisualPinball.Unity.Simulation
{
	/// <summary>
	/// Lock-free SPSC (Single Producer, Single Consumer) ring buffer for input events.
	/// Producer: Input polling thread
	/// Consumer: Simulation thread
	///
	/// Implementation uses a circular buffer with atomic head/tail indices.
	/// Thread-safe for single producer and single consumer without locks.
	/// </summary>
	public class InputEventBuffer : IDisposable
	{
		private readonly NativeInputApi.InputEvent[] _buffer;
		private readonly int _capacity;
		private readonly int _mask; // For power-of-2 wraparound

		// Use separate cache lines to avoid false sharing
		private volatile int _head; // Consumer reads from head
		private volatile int _tail; // Producer writes to tail

		/// <summary>
		/// Creates a new input event buffer.
		/// </summary>
		/// <param name="capacity">Maximum number of events to buffer (default 1024, must be power of 2)</param>
		public InputEventBuffer(int capacity = 1024)
		{
			// Ensure capacity is power of 2 for efficient modulo via bitwise AND
			if (capacity <= 0 || (capacity & (capacity - 1)) != 0)
			{
				throw new ArgumentException("Capacity must be a power of 2", nameof(capacity));
			}

			_capacity = capacity;
			_mask = capacity - 1;
			_buffer = new NativeInputApi.InputEvent[capacity];
			_head = 0;
			_tail = 0;
		}

		/// <summary>
		/// Try to enqueue an input event (non-blocking).
		/// Called by input polling thread (producer).
		/// </summary>
		public bool TryEnqueue(NativeInputApi.InputEvent evt)
		{
			// Read head (consumer position) with volatile semantics
			int currentTail = _tail;
			int nextTail = (currentTail + 1) & _mask;

			// Check if buffer is full
			if (nextTail == Volatile.Read(ref _head))
			{
				// Buffer full - drop event (oldest event stays)
				return false;
			}

			// Write event to buffer
			_buffer[currentTail] = evt;

			// Advance tail (make event visible to consumer)
			Volatile.Write(ref _tail, nextTail);

			return true;
		}

		/// <summary>
		/// Try to dequeue an input event (non-blocking).
		/// Called by simulation thread (consumer).
		/// </summary>
		public bool TryDequeue(out NativeInputApi.InputEvent evt)
		{
			// Read head (our position)
			int currentHead = _head;

			// Check if buffer is empty
			if (currentHead == Volatile.Read(ref _tail))
			{
				evt = default;
				return false;
			}

			// Read event from buffer
			evt = _buffer[currentHead];

			// Advance head (free slot for producer)
			int nextHead = (currentHead + 1) & _mask;
			Volatile.Write(ref _head, nextHead);

			return true;
		}

		/// <summary>
		/// Get the approximate number of events currently in the buffer.
		/// Note: This is an estimate and may not be exact due to concurrent access.
		/// </summary>
		public int Count
		{
			get
			{
				int head = Volatile.Read(ref _head);
				int tail = Volatile.Read(ref _tail);
				int count = (tail - head) & _mask;
				return count;
			}
		}

		public void Dispose()
		{
			// Nothing to dispose for array-based buffer
		}
	}
}
