namespace VisualPinball.Engine.VPT
{
	/// <summary>
	/// The base class for all playfield items (including the table itself)
	/// </summary>
	/// <typeparam name="TData">Data class type this item is using</typeparam>
	public class Item<TData> where TData : ItemData
	{
		public readonly TData Data;

		public string Name => Data.Name;

		public Item(TData data)
		{
			Data = data;
		}
	}
}
