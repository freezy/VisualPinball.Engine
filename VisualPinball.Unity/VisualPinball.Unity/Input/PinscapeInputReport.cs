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

using System.Runtime.InteropServices;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace VisualPinball.Unity
{
	[StructLayout(LayoutKind.Explicit, Size = 14)]
	public struct PinscapeInputReport : IInputStateTypeInfo
	{
		public FourCC format => new FourCC('H', 'I', 'D');

		[FieldOffset(0)] public byte status1;
		[FieldOffset(1)] public byte status2;
		[FieldOffset(2)] public byte unused1;
		[FieldOffset(3)] public byte unused2;

		[InputControl(name = "button01", bit = 0, format = "USHT")]
		[InputControl(name = "button02", bit = 1)]
		[InputControl(name = "button03", bit = 2)]
		[InputControl(name = "button04", bit = 3)]
		[InputControl(name = "button05", bit = 4)]
		[InputControl(name = "button06", bit = 5)]
		[InputControl(name = "button07", bit = 6)]
		[InputControl(name = "button08", bit = 7)]
		[InputControl(name = "button09", bit = 8)]
		[InputControl(name = "button10", bit = 9)]
		[InputControl(name = "button11", bit = 10)]
		[InputControl(name = "button12", bit = 11)]
		[InputControl(name = "button13", bit = 12)]
		[InputControl(name = "button14", bit = 13)]
		[InputControl(name = "button15", bit = 14)]
		[InputControl(name = "button16", bit = 15)]
		[FieldOffset(4)] public byte buttonsLo1;
		[FieldOffset(5)] public byte buttonsLo2;

		[InputControl(name = "button17", bit = 0, format = "USHT")]
		[InputControl(name = "button18", bit = 1)]
		[InputControl(name = "button19", bit = 2)]
		[InputControl(name = "button20", bit = 3)]
		[InputControl(name = "button21", bit = 4)]
		[InputControl(name = "button22", bit = 5)]
		[InputControl(name = "button23", bit = 6)]
		[InputControl(name = "button24", bit = 7)]
		[InputControl(name = "button25", bit = 8)]
		[InputControl(name = "button26", bit = 9)]
		[InputControl(name = "button27", bit = 10)]
		[InputControl(name = "button28", bit = 11)]
		[InputControl(name = "button29", bit = 12)]
		[InputControl(name = "button30", bit = 13)]
		[InputControl(name = "button31", bit = 14)]
		[InputControl(name = "button32", bit = 15)]
		[FieldOffset(6)] public byte buttonsHi1;
		[FieldOffset(7)] public byte buttonsHi2;

		[InputControl(name = "acceleration", layout = "Gyroscope", format = "VC3S")]
		[InputControl(name = "acceleration/x", offset = 0, format = "SHRT", parameters = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5")]
		[InputControl(name = "acceleration/y", offset = 1, format = "SHRT", parameters = "invert,normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5")]
		[InputControl(name = "acceleration/z", offset = 2, format = "SHRT", parameters = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5")]
		[FieldOffset(8)] public byte x1;
		[FieldOffset(9)] public byte x2;
		[FieldOffset(10)] public byte y1;
		[FieldOffset(11)] public byte y2;
		[FieldOffset(12)] public byte z1;
		[FieldOffset(13)] public byte z2;

	}
}
