using System.IO;

namespace VisualPinball.Engine.VPT.Flasher
{
	public class Flasher : Item<FlasherData>
	{
		public Flasher(FlasherData data) : base(data)
		{
		}

		public Flasher(BinaryReader reader, string itemName) : this(new FlasherData(reader, itemName))
		{
		}
	}
}
