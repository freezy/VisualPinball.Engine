namespace VisualPinball.Engine.VPT
{
	public class Bitmap
	{
		public const int RGBA = 0;
		public const int RGB_FP = 1;

		private int _width;
		private int _height;
		public int Format = RGBA;
		private byte[] _data;

		public Bitmap(int width, int height)
		{
			_width = width;
			_height = height;
		}
	}
}
