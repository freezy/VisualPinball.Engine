namespace VisualPinball.Engine.VPT
{
	/// <summary>
	/// Non-generic abstraction for <see cref="Item{TData}"/>
	/// </summary>
	public interface IItem
	{
		string Name { get; }

		int StorageIndex { get; set; }
	}
}
