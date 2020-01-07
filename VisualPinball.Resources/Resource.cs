using System.Reflection;

namespace VisualPinball.Engine.Resources
{
	public static class Resource
	{
		public static string BumperBaseFilename => @"Textures\BumperBase.png";
		public static byte[] BumperBase => GetTexture(BumperBaseFilename);
		public static string BumperCapFilename => @"Textures\BumperCap.png";
		public static byte[] BumperCap => GetTexture(BumperCapFilename);
		public static string BumperRingFilename => @"Textures\BumperRing.png";
		public static byte[] BumperRing => GetTexture(BumperRingFilename);
		public static string BumperSocketFilename => @"Textures\BumperSkirt.png";
		public static byte[] BumperSocket => GetTexture(BumperSocketFilename);

		private static byte[] GetTexture(string name)
		{
			var a = Assembly.GetExecutingAssembly();
			using (var stream = a.GetManifestResourceStream(name)) {
				if (stream == null) {
					return null;
				}
				var ba = new byte[stream.Length];
				stream.Read(ba, 0, ba.Length);
				return ba;
			}
		}
	}
}
