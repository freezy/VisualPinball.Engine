namespace VisualPinball.Engine.VPT
{
	/// <summary>
	/// The base class for all playfield items (including the table itself)
	/// </summary>
	/// <typeparam name="TData">Data class type this item is using</typeparam>
	public class Item<TData> : INameable where TData : ItemData
	{
		public readonly TData Data;

		public string Name { get { return Data.GetName(); } set { Data.SetName(value); } }
		public int Index { get; set; }
		public int Version { get; set; }

		public Item(TData data)
		{
			Data = data;
		}
	}
}
