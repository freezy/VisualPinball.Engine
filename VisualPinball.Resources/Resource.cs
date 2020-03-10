using System.Reflection;

namespace VisualPinball.Engine.Resources
{
	public class Resource
	{
		public static readonly Resource BallDebug = new Resource("__BallDebug", GetTexture("VisualPinball.Engine.Resources.Textures.BallDebug.png"));
		public static readonly Resource BumperBase = new Resource("__BumperBase", GetTexture("VisualPinball.Engine.Resources.Textures.BumperBase.png"));
		public static readonly Resource BumperCap = new Resource("__BumperCap", GetTexture("VisualPinball.Engine.Resources.Textures.BumperCap.png"));
		public static readonly Resource BumperRing = new Resource("__BumperRing", GetTexture("VisualPinball.Engine.Resources.Textures.BumperRing.png"));
		public static readonly Resource BumperSocket = new Resource("__BumperSocket", GetTexture("VisualPinball.Engine.Resources.Textures.BumperSkirt.png"));

		public readonly string Name;
		public readonly byte[] Data;

		private Resource(string name, byte[] data)
		{
			Name = name;
			Data = data;
		}

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
