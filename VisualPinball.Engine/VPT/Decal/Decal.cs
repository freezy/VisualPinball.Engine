using System.IO;

namespace VisualPinball.Engine.VPT.Decal
{
	public class Decal : Item<DecalData>
	{
		public Decal(DecalData data) : base(data)
		{
		}

		public Decal(BinaryReader reader, string itemName) : this(new DecalData(reader, itemName))
		{
		}
	}
}
