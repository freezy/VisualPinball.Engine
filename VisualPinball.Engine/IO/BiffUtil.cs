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
			return Encoding.Default.GetString(nullPos > -1 ? bytes.Take(nullPos).ToArray() : bytes);
		}

		public static byte[] GetNullTerminatedString(string value, int length)
		{
			var bytes = Encoding.Default.GetBytes(value);
			if (bytes.Length == length) {
				return bytes;
			}

			if (bytes.Length > length) {
				return bytes.Take(length).ToArray();

			}

			var newArray = new byte[length];
			Array.Copy(bytes, 0, newArray, 0, bytes.Length);
			return newArray;
		}

		public static string ParseWideString(IEnumerable<byte> data)
		{
			return Encoding.Default.GetString(data.Where((x, i) => i % 2 == 0).ToArray());
		}

		public static byte[] GetWideString(string value)
		{
			return Encoding.Default.GetBytes(value).SelectMany(b => new byte[] {b, 0x0}).ToArray();
		}
	}
}
