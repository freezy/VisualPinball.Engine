using System.IO;

namespace VisualPinball.Engine.VPT.Collection
{
	public class Collection : Item<CollectionData>
	{
		public Collection(CollectionData data) : base(data)
		{
		}

		public Collection(BinaryReader reader, string itemName) : this(new CollectionData(reader, itemName))
		{
		}
	}
}
