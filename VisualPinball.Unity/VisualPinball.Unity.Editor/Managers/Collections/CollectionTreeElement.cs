namespace VisualPinball.Unity.Editor
{
	public class CollectionTreeElement : TreeElement
	{
		private string _itemName;

		public override string Name => _itemName ?? "";

		public CollectionTreeElement(string itemName) : base()
		{
			_itemName = itemName;
		}
	}
}
