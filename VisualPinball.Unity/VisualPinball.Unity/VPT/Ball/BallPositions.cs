// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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
using Unity.Mathematics;

namespace VisualPinball.Unity
{
	/// <summary>
	/// We can't have lists of lists in jobs, so...
	/// </summary>
	public struct BallPositions
	{
		public static int Count => 10;

		private float3 _pos00;
		private float3 _pos01;
		private float3 _pos02;
		private float3 _pos03;
		private float3 _pos04;
		private float3 _pos05;
		private float3 _pos06;
		private float3 _pos07;
		private float3 _pos08;
		private float3 _pos09;

		public BallPositions(float3 initialPositions)
		{
			_pos00 = initialPositions;
			_pos01 = initialPositions;
			_pos02 = initialPositions;
			_pos03 = initialPositions;
			_pos04 = initialPositions;
			_pos05 = initialPositions;
			_pos06 = initialPositions;
			_pos07 = initialPositions;
			_pos08 = initialPositions;
			_pos09 = initialPositions;
		}

		public float3 this[int index] {
			get {
				return index switch {
					0 => _pos00,
					1 => _pos01,
					2 => _pos02,
					3 => _pos03,
					4 => _pos04,
					5 => _pos05,
					6 => _pos06,
					7 => _pos07,
					8 => _pos08,
					9 => _pos09,
					_ => throw new ArgumentOutOfRangeException("Only " + Count + " positions available.")
				};
			}
			set {
				switch (index) {
					case 0: _pos00 = value; break;
					case 1: _pos01 = value; break;
					case 2: _pos02 = value; break;
					case 3: _pos03 = value; break;
					case 4: _pos04 = value; break;
					case 5: _pos05 = value; break;
					case 6: _pos06 = value; break;
					case 7: _pos07 = value; break;
					case 8: _pos08 = value; break;
					case 9: _pos09 = value; break;
				}
			}
		}
	}
}
