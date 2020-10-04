using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity
{
	public abstract class ItemMainAuthoring<TItem, TData> : ItemAuthoring<TItem, TData>
		where TItem : Item<TData>, IRenderable
		where TData : ItemData
	{

	}
}
