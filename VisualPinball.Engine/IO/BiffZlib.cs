using System.IO;
using NetMiniZ;

namespace VisualPinball.Engine.IO
{
	/// <summary>
	/// Handles the ZLib compression sometimes used in BIFF records.
	/// </summary>
	public static class BiffZlib
	{
		private static NetMiniZUtils utils = null;
		
		static BiffZlib()
		{
		   utils = new NetMiniZUtils();
		}
		
		public static byte[] Decompress(byte[] bytes)
		{
			using (var outStream = new MemoryStream())
			using (var inStream = new MemoryStream(bytes)) {
				utils.Decompress(inStream, outStream);
				return outStream.ToArray();
			}
		}

		public static byte[] Compress(byte[] inData)
		{
			using (var outStream = new MemoryStream())
			using (var inStream = new MemoryStream(inData)) {
				utils.Compress(inStream, outStream, 9);
				return outStream.ToArray();
			}
		}
	}
}
