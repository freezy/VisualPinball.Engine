using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.Game
{
	public interface IRenderable
	{
		string Name { get; }

		RenderObjectGroup GetRenderObjects(Table table, Origin origin = Origin.Global, bool asRightHanded = true);
	}
}
