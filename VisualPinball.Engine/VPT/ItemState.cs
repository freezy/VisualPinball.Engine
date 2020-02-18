namespace VisualPinball.Engine.VPT
{
	public abstract class ItemState
	{
		public string Name;
		public bool IsVisible;

		protected ItemState(string name, bool isVisible)
		{
			Name = name;
			IsVisible = isVisible;
		}
	}
}
