using System.IO;
using System.Xml.Schema;

namespace VisualPinball.Engine.VPT
{
	public class Texture : Item<TextureData>
	{
		public int Width => Data.Width;
		public int Height => Data.Width;

		public bool IsHdr => Data.Path.ToLower().EndsWith(".hdr") || Data.Path.ToLower().EndsWith(".exr");

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

		public Texture(BinaryReader reader, string itemName) : base(new TextureData(reader, itemName))
		{
		}

		private IBinaryData GetBinaryData()
		{
			return Data.Binary as IBinaryData ?? Data.Bitmap;
		}
	}
}
