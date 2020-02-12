using System.IO;

namespace VisualPinball.Engine.VPT.Plunger
{
	public class Plunger : Item<PlungerData>
	{
		public Plunger(PlungerData data) : base(data)
		{
		}

		public Plunger(BinaryReader reader, string itemName) : this(new PlungerData(reader, itemName))
		{
		}
	}
}
