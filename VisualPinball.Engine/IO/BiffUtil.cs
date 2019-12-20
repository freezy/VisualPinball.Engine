using System;
using System.IO;
using System.Linq;
using System.Text;

namespace VisualPinball.Engine.IO
{
	public static class BiffUtil
	{
		/// <summary>
		/// Reads a number of bytes but stops converting to ASCII after 0x0.
		/// </summary>
		/// <param name="reader">Binary data from the VPX file</param>
		/// <param name="length">Data to read</param>
		/// <returns></returns>
		public static string ReadNullTerminatedString(BinaryReader reader, int length)
		{
			var bytes = reader.ReadBytes(length);
			var nullPos = Array.IndexOf(bytes, (byte)0x0);
			return Encoding.ASCII.GetString(nullPos > -1 ? bytes.Take(nullPos).ToArray() : bytes);
		}

		/// <summary>
		/// Converts a BGR color to a RGB color.
		/// </summary>
		/// <param name="bgr">BGR color</param>
		/// <returns>RGB color</returns>
		public static int BgrToRgb(int bgr) {
			var r = (bgr & 0xff) << 16;
			var g = bgr & 0xff00;
			var b = (bgr & 0xff0000) >> 16;
			return r + g + b;
		}
	}
}
