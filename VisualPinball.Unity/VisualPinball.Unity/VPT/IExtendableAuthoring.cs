using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity
{
	/// <summary>
	/// This interface is for items that allow sub items to be linked to.
	///
	/// For example, a Flipper might have a different mesh, and hence
	/// a different item being linked to it (and translated/animated along
	/// with it).
	/// </summary>
	public interface IExtendableAuthoring
	{
		void LinkChild(IItemAuthoring item);
	}
}
