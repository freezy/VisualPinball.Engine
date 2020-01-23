using System.IO;

namespace VisualPinball.Engine.VPT.Flasher
{
	public class Flasher : Item<FlasherData>
	{
		public Flasher(BinaryReader reader, string itemName) : base(new FlasherData(reader, itemName))
		{
		}
	}
}
