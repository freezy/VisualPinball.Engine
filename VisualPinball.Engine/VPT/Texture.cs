using System.IO;
using System.Xml.Schema;

namespace VisualPinball.Engine.VPT
{
	public class Texture : Item<TextureData>
	{
		public byte[] Content => (Data.Binary as IBinaryData ?? Data.Bitmap).Bytes;

		public Texture(BinaryReader reader, string itemName) : base(new TextureData(reader, itemName))
		{
		}
	}
}
