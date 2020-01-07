using System.IO;

namespace VisualPinball.Engine.VPT.Light
{
	public class Light : Item<LightData>
	{
		public Light(BinaryReader reader, string itemName) : base(new LightData(reader, itemName))
		{
		}
	}
}
