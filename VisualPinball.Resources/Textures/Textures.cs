using System.Reflection;

namespace VisualPinball.Engine.Resources.Textures
{
	public static class Textures
	{
		public static byte[] BumperBase => GetTexture("BumperBase.png");
		public static byte[] BumperCap => GetTexture("BumperCap.png");
		public static byte[] BumperRing => GetTexture("BumperRing.png");
		public static byte[] BumperSocket => GetTexture("BumperSkirt.png");

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
