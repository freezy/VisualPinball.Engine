using System;
using System.Collections.Generic;
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

		public static string ParseWideString(IEnumerable<byte> data)
		{
			return Encoding.ASCII.GetString(data.Where((x, i) => i % 2 == 0).ToArray());
		}
	}
}
