using UnityEditor.IMGUI.Controls;

namespace VisualPinball.Unity.Editor
{
	internal class CollectionTreeView : TreeView<CollectionTreeElement>
	{
		public CollectionTreeView() : base(new TreeViewState(), new CollectionTreeElement(string.Empty))
		{
			showBorder = true;
			showAlternatingRowBackgrounds = true;
		}
	}
}
