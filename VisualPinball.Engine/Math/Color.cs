using System;

namespace VisualPinball.Engine.Math
{
	[Serializable]
	public class Color
	{
		public int Red = 0x0;
		public int Green = 0x0;
		public int Blue = 0x0;
		public int Alpha = 0xff;

		public float R => Red / 255f;
		public float G => Green / 255f;
		public float B => Blue / 255f;
		public float A => Alpha / 255f;

		public Color(long color, ColorFormat format)
		{
			switch (format) {
				case ColorFormat.Bgr:
					Red = (int)(color & 0x000000ff);
					Green = (int)(color & 0x0000ff00) >> 8;
					Blue = (int)(color & 0x00ff0000) >> 16;
					break;
				case ColorFormat.Argb:
					Red = (int)(color & 0x00ff0000) >> 16;
					Green = (int)(color & 0x0000ff00) >> 8;
					Blue = (int)(color & 0x000000ff);
					Alpha = (int)(color & 0xff000000) >> 24;
					break;
			}
		}

		public bool IsGray()
		{
			return Red == Green && Green == Blue;
		}

		public override string ToString()
		{
			return $"rgba({System.Math.Round(R, 3)}, {System.Math.Round(G, 3)}, {System.Math.Round(B, 3)}, {System.Math.Round(A, 3)})";
		}
	}

	public enum ColorFormat
	{
		Bgr, Argb
	}
}
