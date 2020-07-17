using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor.Layers
{
	/// <summary>
	/// This handler will construct a layer structure from the table loaded data.
	/// It will then be in charge of layers management (add/remove/item assignation)
	/// It will use the SceneVisibilityManager to synchronize layers/items visibility status & editor visibility
	/// </summary>
	class LayerHandler
	{
		public bool IsVisible(GameObject gameObj)
		{
			return !SceneVisibilityManager.instance.IsHidden(gameObj);
		}

		public void SetVisibility(GameObject gameObj, bool visibility, bool includeDescendants)
		{
			if (visibility) {
				SceneVisibilityManager.instance.Show(gameObj, includeDescendants);
			} else {
				SceneVisibilityManager.instance.Hide(gameObj, includeDescendants);
			}
		}
		public void SetVisibility(GameObject[] gameObjs, bool visibility, bool includeDescendants)
		{
			if (visibility) {
				SceneVisibilityManager.instance.Show(gameObjs, includeDescendants);
			} else {
				SceneVisibilityManager.instance.Hide(gameObjs, includeDescendants);
			}
		}
	}
}
