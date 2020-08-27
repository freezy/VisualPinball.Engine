using System.IO;

namespace VisualPinball.Engine.VPT.Collection
{
	public class Collection : Item<CollectionData>
	{
		public Collection(string name) : this(new CollectionData(name))
		{
			Name = name;
		}

		public Collection(string name, CollectionData data) : base(data)
		{
			Name = name;
		}

		public Collection(CollectionData data) : base(data)
		{
		}

		public Collection(BinaryReader reader, string itemName) : this(new CollectionData(reader, itemName))
		{
		}

	}
}
