using System.IO;

namespace VisualPinball.Engine.VPT
{
	public class Texture : Item<TextureData>
	{
		public Texture(BinaryReader reader, string itemName) : base(new TextureData(reader, itemName))
		{
		}
	}
}
