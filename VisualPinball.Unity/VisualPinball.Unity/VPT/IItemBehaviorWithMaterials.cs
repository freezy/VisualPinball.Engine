using UnityEngine;

namespace VisualPinball.Unity.VPT
{
	// to be implemented by ItemBehaviors that reference vpx materials, so the
	// editor window can find them via unity heirarchy searches
	public interface IItemBehaviorWithMaterials
    {
		string[] UsedMaterials { get; }
	}
}
