using System.IO;

namespace VisualPinball.Engine.VPT.Collection
{
	public class Collection : Item<CollectionData>
	{
		public Collection(BinaryReader reader, string itemName) : base(new CollectionData(reader, itemName))
		{
		}
	}
}
