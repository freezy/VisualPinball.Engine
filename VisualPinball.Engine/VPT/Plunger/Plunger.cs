using System.IO;

namespace VisualPinball.Engine.VPT.Plunger
{
	public class Plunger : Item<PlungerData>
	{
		public Plunger(BinaryReader reader, string itemName) : base(new PlungerData(reader, itemName))
		{
		}
	}
}
