namespace VisualPinball.Engine.Common
{
	public readonly struct ItemId
	{
		public readonly int Index;
		public readonly int Version;

		public ItemId(int index, int version)
		{
			Index = index;
			Version = version;
		}
	}
}
