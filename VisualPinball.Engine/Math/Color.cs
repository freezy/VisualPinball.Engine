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

		public int Bgr => Blue * 65536 + Green * 256 + Red;

		public Color(int red, int green, int blue, int alpha)
		{
			Red = red;
			Green = green;
			Blue = blue;
			Alpha = alpha;
		}

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

		public Color Clone()
		{
			return new Color(Red, Green, Blue, Alpha);
		}

		public bool IsGray()
		{
			return Red == Green && Green == Blue;
		}

		public override string ToString()
		{
			return $"rgba({System.Math.Round(R, 3)}, {System.Math.Round(G, 3)}, {System.Math.Round(B, 3)}, {System.Math.Round(A, 3)})";
		}

		public int ToInt(ColorFormat format)
		{
			switch (format) {
				case ColorFormat.Bgr:
					return Red + (Green << 8) + (Blue << 16);
				case ColorFormat.Argb:
					return (Red << 16) + (Green << 8) + Blue + (Alpha << 24);
			}
			return 0;
		}
	}

	public enum ColorFormat
	{
		Bgr, Argb
	}
}
