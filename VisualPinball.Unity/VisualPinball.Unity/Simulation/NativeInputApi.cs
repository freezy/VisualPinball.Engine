// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
// https://github.com/freezy/VisualPinball.Engine
//
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Runtime.InteropServices;
using AOT;

namespace VisualPinball.Unity.Simulation
{
	/// <summary>
	/// P/Invoke wrapper for native input polling library
	/// </summary>
	public static class NativeInputApi
	{
		private const string DllName = "VpeNativeInput";

		#region Enums

		/// <summary>
		/// Input action enum (must match native enum)
		/// </summary>
		public enum InputAction
		{
			LeftFlipper = 0,
			RightFlipper = 1,
			UpperLeftFlipper = 2,
			UpperRightFlipper = 3,
			LeftMagnasave = 4,
			RightMagnasave = 5,
			Start = 6,
			Plunge = 7,
			PlungerAnalog = 8,
			CoinInsert1 = 9,
			CoinInsert2 = 10,
			CoinInsert3 = 11,
			CoinInsert4 = 12,
			ExitGame = 13,
			SlamTilt = 14,
		}

		/// <summary>
		/// Input binding type
		/// </summary>
		public enum BindingType
		{
			Keyboard = 0,
			Gamepad = 1,
			Mouse = 2,
		}

		/// <summary>
		/// Key codes (Windows virtual key codes)
		/// </summary>
		public enum KeyCode
		{
			LShift = 0xA0,
			RShift = 0xA1,
			LControl = 0xA2,
			RControl = 0xA3,
			Space = 0x20,
			Return = 0x0D,
			D1 = 0x31,
			Num1 = 0x31, // alias for top-row '1'
			D5 = 0x35,
			Num5 = 0x35, // alias for top-row '5'
			Numpad1 = 0x61,
			A = 0x41,
			S = 0x53,
			D = 0x44,
			W = 0x57,
		}

		#endregion

		#region Structures

		/// <summary>
		/// Input event structure (matches native struct layout)
		/// </summary>
		[StructLayout(LayoutKind.Sequential, Pack = 4)]
		public struct InputEvent
		{
			public long TimestampUsec;
			public int Action;  // InputAction
			public float Value;
			private int _padding;
		}

		/// <summary>
		/// Input binding structure
		/// </summary>
		[StructLayout(LayoutKind.Sequential, Pack = 4)]
		public struct InputBinding
		{
			public int Action;      // InputAction
			public int BindingType; // BindingType
			public int KeyCode;     // KeyCode or button index
			private int _padding;
		}

		#endregion

		#region Delegates

		/// <summary>
		/// Callback for input events
		/// </summary>
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate void InputEventCallback(ref InputEvent evt, IntPtr userData);

		#endregion

		#region Native Functions

		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		public static extern int VpeInputInit();

		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void VpeInputShutdown();

		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void VpeInputSetBindings(InputBinding[] bindings, int count);

		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		public static extern int VpeInputStartPolling(InputEventCallback callback, IntPtr userData, int pollIntervalUs);

		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void VpeInputStopPolling();

		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		public static extern long VpeGetTimestampUsec();

		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void VpeSetThreadPriority();

		#endregion
	}
}
