using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Editor.Utils.TreeViewWithTreeModel;
using VisualPinball.Unity.VPT;
using VisualPinball.Unity.VPT.Table;

namespace VisualPinball.Unity.Editor.Layers
{
	/// <summary>
	/// Enum for different Item Types in the LayerTreeView
	/// </summary>
	/// <remarks>
	/// The Item Type will change the IsVisible behavior.
	/// It will be used also to display a specific icon per item in the treeview
	/// </remarks>
	enum LayerTreeViewElementType
	{
		Root,
		Table,
		Layer,
		Item
	}

	/// <summary>
	/// Element type used in TreeModel for the LayerTreeView
	/// </summary>
	/// <remarks>
	/// Could be initialized in different ways to fit all possible LayerTreeView item types
	/// </remarks>
	class LayerTreeElement : TreeElement
	{
		public Table Table { get; private set; }
		public ILayerableItemBehavior Item { get; private set; }
		public string LayerName { get; private set; }
		public LayerTreeViewElementType Type { get { return Table != null ? LayerTreeViewElementType.Table : Item != null ? LayerTreeViewElementType.Item : LayerName != null ? LayerTreeViewElementType.Layer : LayerTreeViewElementType.Root; } }

		[SerializeField]
		private bool _isVisible = true;
		public bool IsVisible 
		{
			get { 
				if (Type == LayerTreeViewElementType.Item) {
					if (Item is Behaviour behavior) {
						_isVisible = !SceneVisibilityManager.instance.IsHidden(behavior.gameObject);
					}
					else {
						_isVisible = false;
					}
				}
				return _isVisible;
			}

			set {
				_isVisible = value;
				if (Type == LayerTreeViewElementType.Item) {
					Item.EditorLayerVisibility = _isVisible;
					if (Item is Behaviour behavior) {
						if (_isVisible) {
							SceneVisibilityManager.instance.Show(behavior.gameObject, true);
						}
						else {
							SceneVisibilityManager.instance.Hide(behavior.gameObject, true);
						}
					}
				}
				else if (Type != LayerTreeViewElementType.Root) {
					if (HasChildren) {
						foreach (LayerTreeElement child in Children) {
							child.IsVisible = _isVisible;
						}
					}
				}
			} 
		}

		public override string Name
		{
			get {
				if (Table != null) {
					return Table.Name;
				}

				if (Item is IIdentifiableItemBehavior identifiable) {
					return identifiable.Name;
				}

				if (LayerName != null) {
					return LayerName;
				}

				return "<Root>";
			}
		}

		/// <summary>
		/// Default CTor is for Root
		/// </summary>
		public LayerTreeElement(){}
		public LayerTreeElement(Table table)
		{
			Table = table;
		}
		public LayerTreeElement(ILayerableItemBehavior item)
		{
			Item = item;
			IsVisible = Item.EditorLayerVisibility;
		}
		public LayerTreeElement(string layerName)
		{
			LayerName = layerName;
		}
	}
}
