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
		public const int ProtocolVersion = 2;
		public const int DeviceIdSize = 260;
		public const int DeviceNameSize = 128;
		public const int AxisNameSize = 32;

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

		public enum InputEventType
		{
			Action = 0,
			Axis = 1,
			DevicesChanged = 2,
		}

		public enum AxisKind
		{
			Position = 0,
			Velocity = 1,
			Acceleration = 2,
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
			Z = 0x5A,
			Numpad1 = 0x61,

			A = 0x41,
			B = 0x42,
			S = 0x53,
			D = 0x44,
			W = 0x57,

			Minus = 0xBD,  // VK_OEM_MINUS
			Slash = 0xBF,  // VK_OEM_2
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
			public int EventType; // InputEventType
			public int Action;  // InputAction
			public int DeviceIndex;
			public int AxisId;
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

		// The native side fills the string fields with UTF-8; decode them manually,
		// since ByValTStr would decode with the system ANSI codepage and garble
		// non-ASCII device names.
		[StructLayout(LayoutKind.Sequential, Pack = 4)]
		public struct InputDeviceInfo
		{
			public int DeviceIndex;
			public int AxisCount;
			public int IsConnected;
			private int _padding;

			[MarshalAs(UnmanagedType.ByValArray, SizeConst = DeviceIdSize)]
			private byte[] _stableId;

			[MarshalAs(UnmanagedType.ByValArray, SizeConst = DeviceNameSize)]
			private byte[] _displayName;

			public string StableId => DecodeUtf8(_stableId);
			public string DisplayName => DecodeUtf8(_displayName);
		}

		[StructLayout(LayoutKind.Sequential, Pack = 4)]
		public struct InputAxisInfo
		{
			public int AxisId;
			public int UsagePage;
			public int Usage;
			public int Kind;
			public float RawValue;
			private int _padding;
			public long TimestampUsec;

			[MarshalAs(UnmanagedType.ByValArray, SizeConst = AxisNameSize)]
			private byte[] _name;

			public string Name => DecodeUtf8(_name);
		}

		private static string DecodeUtf8(byte[] bytes)
		{
			if (bytes == null) {
				return string.Empty;
			}
			var length = Array.IndexOf(bytes, (byte)0);
			if (length < 0) {
				length = bytes.Length;
			}
			return length == 0 ? string.Empty : System.Text.Encoding.UTF8.GetString(bytes, 0, length);
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
		public static extern int VpeInputGetProtocolVersion();

		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void VpeInputShutdown();

		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void VpeInputSetBindings(InputBinding[] bindings, int count);

		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		public static extern int VpeInputStartPolling(InputEventCallback callback, IntPtr userData, int pollIntervalUs);

		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void VpeInputStopPolling();

		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		public static extern int VpeInputListDevices([Out] InputDeviceInfo[] devices, int maxDevices);

		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		public static extern int VpeInputListDeviceAxes(int deviceIndex, [Out] InputAxisInfo[] axes, int maxAxes);

		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		public static extern long VpeGetTimestampUsec();

		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void VpeSetThreadPriority();

		#endregion
	}
}
