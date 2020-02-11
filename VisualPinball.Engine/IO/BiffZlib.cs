using System.IO;
using MiniZ;

namespace VisualPinball.Engine.IO
{
	/// <summary>
	/// Handles the ZLib compression sometimes used in BIFF records.
	/// </summary>
	public static class BiffZlib
	{
		public static byte[] Decompress(byte[] bytes)
		{
			using (var outStream = new MemoryStream())
			using (var inStream = new MemoryStream(bytes)) {
				Functions.Decompress(inStream, outStream);
				return outStream.ToArray();
			}
		}

		public static byte[] Compress(byte[] inData)
		{
			using (var outStream = new MemoryStream())
			using (var inStream = new MemoryStream(inData)) {
				Functions.Compress(inStream, outStream, 9);
				return outStream.ToArray();
			}
		}
	}
}
