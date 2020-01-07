using System.IO;
using VisualPinball.Engine.Resources;

namespace VisualPinball.Engine.VPT
{
	public class Texture : Item<TextureData>
	{
		public static readonly Texture BumperBase = new Texture(Resource.BumperBase);
		public static readonly Texture BumperCap = new Texture(Resource.BumperCap);
		public static readonly Texture BumperRing = new Texture(Resource.BumperRing);
		public static readonly Texture BumperSocket = new Texture(Resource.BumperSocket);

		public static readonly Texture[] LocalTextures = {
			BumperBase, BumperCap, BumperRing, BumperSocket
		};

		public int Width => Data.Width;
		public int Height => Data.Width;

		/// <summary>
		/// Data as read from the .vpx file. Note that for bitmaps, it doesn't
		/// contain the header.
		/// </summary>
		/// <see cref="FileContent"/>
		public byte[] Content => GetBinaryData().Bytes;

		/// <summary>
		/// Data as it would written to an image file (incl headers).
		/// </summary>
		public byte[] FileContent => GetBinaryData().FileContent;

		public Texture(BinaryReader reader, string itemName) : base(new TextureData(reader, itemName)) { }

		public Texture(Resource res) : base(new TextureData(res)) { }

		private IBinaryData GetBinaryData()
		{
			return Data.Binary as IBinaryData ?? Data.Bitmap;
		}
	}
}
