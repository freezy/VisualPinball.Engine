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

using System;

namespace VisualPinball.Engine.Math
{
	[Serializable]
	public class Color
	{
		public int Red;
		public int Green;
		public int Blue;
		public int Alpha = 0xff;

		public float R => Red / 255f;
		public float G => Green / 255f;
		public float B => Blue / 255f;
		public float A => Alpha / 255f;

		public uint Bgr => (uint)Alpha * 16777216 + (uint)Blue * 65536 + (uint)Green * 256 + (uint)Red;

		public Color(int red, int green, int blue, int alpha)
		{
			Red = red;
			Green = green;
			Blue = blue;
			Alpha = alpha;
		}

		public Color(uint color, ColorFormat format)
		{
			switch (format) {
				case ColorFormat.Bgr:
					Red = (int)(color & 0x000000ff);
					Green = (int)((color & 0x0000ff00) >> 8);
					Blue = (int)((color & 0x00ff0000) >> 16);
					Alpha = (int)(color >> 24);
					break;
				case ColorFormat.Argb:
					Red = (int)((color & 0x00ff0000) >> 16);
					Green = (int)((color & 0x0000ff00) >> 8);
					Blue = (int)(color & 0x000000ff);
					Alpha = (int)((color & 0xff000000) >> 24);
					break;
			}
		}

		public Color Clone()
		{
			return new Color(Red, Green, Blue, Alpha);
		}

		public Color WithAlpha(int alpha)
		{
			return new Color(Red, Green, Blue, alpha);
		}

		public bool IsGray()
		{
			return Red == Green && Green == Blue;
		}

		public override string ToString()
		{
			return $"rgba({System.Math.Round(R, 3)}, {System.Math.Round(G, 3)}, {System.Math.Round(B, 3)}, {System.Math.Round(A, 3)})";
		}

		public uint ToInt(ColorFormat format)
		{
			switch (format) {
				case ColorFormat.Bgr:
					return (uint)Red + ((uint)Green << 8) + ((uint)Blue << 16) + ((uint)Alpha << 24);
				case ColorFormat.Argb:
					return ((uint)Red << 16) + ((uint)Green << 8) + (uint)Blue + ((uint)Alpha << 24);
			}
			return 0;
		}
	}

	public enum ColorFormat
	{
		Bgr, Argb
	}

	public enum ColorChannel
	{
		/// <summary>
		/// The red channel for RGB lamps.
		/// </summary>
		Red,

		/// <summary>
		/// The green channel for RGB lamps.
		/// </summary>
		Green,

		/// <summary>
		/// The blue channel for RBG lamps.
		/// </summary>
		Blue,

		/// <summary>
		/// The intensity for white lamps.
		/// </summary>
		Alpha,
	}

	public static class Colors
	{
		public static readonly Color Black = new Color(0, 0, 0, 255);
		public static readonly Color White = new Color(255, 255, 255, 255);
		public static readonly Color Red = new Color(255, 0, 0, 255);
	}
}
