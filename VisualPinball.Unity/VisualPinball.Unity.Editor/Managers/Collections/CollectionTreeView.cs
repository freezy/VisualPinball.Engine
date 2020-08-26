using UnityEditor.IMGUI.Controls;

namespace VisualPinball.Unity.Editor
{
	internal class CollectionTreeView : TreeView<CollectionTreeElement>
	{
		public CollectionTreeView() : base(new TreeViewState(), new CollectionTreeElement(string.Empty) { Id = -1 })
		{
			showBorder = true;
			showAlternatingRowBackgrounds = true;
		}
	}
}
