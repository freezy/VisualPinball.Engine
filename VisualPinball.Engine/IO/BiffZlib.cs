using System.IO;
using zlib;

namespace VisualPinball.Engine.IO
{
	/// <summary>
	/// Handles the ZLib compression sometimes used in BIFF records.
	/// </summary>
	public static class BiffZlib
	{
		public static byte[] Decompress(byte[] bytes)
		{
			using (var outMemoryStream = new MemoryStream())
			using (var outZStream = new ZOutputStream(outMemoryStream))
			using (Stream inMemoryStream = new MemoryStream(bytes))
			{
				CopyStream(inMemoryStream, outZStream);
				outZStream.finish();
				return outMemoryStream.ToArray();
			}
		}

		private static void CopyStream(Stream input, Stream output)
		{
			var buffer = new byte[2000];
			int len;
			while ((len = input.Read(buffer, 0, 2000)) > 0) {
				output.Write(buffer, 0, len);
			}
			output.Flush();
		}
	}
}
