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
		private const string DllName = "VisualPinball.NativeInput";

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
			LeftStagedFlipper = 15,
			RightStagedFlipper = 16,
			LeftNudge = 17,
			RightNudge = 18,
			CenterNudge = 19,
			Tilt = 20,
			ExtraBall = 21,
			Lockbar = 22,
			PauseGame = 23,
			CoinDoor = 24,
			Reset = 25,
			Service1 = 26,
			Service2 = 27,
			Service3 = 28,
			Service4 = 29,
			Service5 = 30,
			Service6 = 31,
			Service7 = 32,
			Service8 = 33,
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
			LAlt = 0xA4,
			RAlt = 0xA5,

			Escape = 0x1B,
			Space = 0x20,
			PageUp = 0x21,
			PageDown = 0x22,
			End = 0x23,
			Home = 0x24,
			Return = 0x0D,

			F1 = 0x70,
			F2 = 0x71,
			F3 = 0x72,
			F4 = 0x73,
			F5 = 0x74,
			F6 = 0x75,
			F7 = 0x76,
			F8 = 0x77,
			F9 = 0x78,
			F10 = 0x79,
			F11 = 0x7A,
			F12 = 0x7B,

			D0 = 0x30,
			D1 = 0x31,
			Num1 = 0x31, // alias for top-row '1'
			D2 = 0x32,
			D3 = 0x33,
			D4 = 0x34,
			D5 = 0x35,
			Num5 = 0x35, // alias for top-row '5'
			D6 = 0x36,
			D7 = 0x37,
			D8 = 0x38,
			D9 = 0x39,

			O = 0x4F,
			P = 0x50,
			T = 0x54,
			Y = 0x59,
			Numpad1 = 0x61,

			A = 0x41,
			B = 0x42,
			S = 0x53,
			D = 0x44,
			W = 0x57,

			Minus = 0xBD,  // VK_OEM_MINUS
			Quote = 0xDE,  // VK_OEM_7
			Oem3 = 0xC0,   // VK_OEM_3 (layout dependent)
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
