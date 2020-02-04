using System.IO;

namespace VisualPinball.Engine.VPT.Decal
{
	public class Decal : Item<DecalData>
	{
		public Decal(BinaryReader reader, string itemName) : base(new DecalData(reader, itemName))
		{
		}
	}
}
