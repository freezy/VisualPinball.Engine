using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Editor.Utils.TreeView;
using VisualPinball.Unity.VPT;
using VisualPinball.Unity.VPT.Table;

namespace VisualPinball.Unity.Editor.Layers
{
	/// <summary>
	/// Element type used in TreeModel for the LayerTreeView
	/// </summary>
	/// <remarks>
	/// Could be initialized in different ways to fit all possible LayerTreeView item types
	/// </remarks>
	internal class LayerTreeElement : TreeElement
	{
		/// <summary>
		/// Name of the layer
		/// </summary>
		public string LayerName;
		public ILayerableItemBehavior Item { get; }

		private readonly Table _table;

		public LayerTreeViewElementType Type =>
			_table != null
				? LayerTreeViewElementType.Table
				: Item != null
					? LayerTreeViewElementType.Item
					: LayerName != null ? LayerTreeViewElementType.Layer : LayerTreeViewElementType.Root;

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
				if (_table != null) {
					return _table.Name;
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
			_table = table;
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

	/// <summary>
	/// Enum for different Item Types in the LayerTreeView
	/// </summary>
	/// <remarks>
	/// The Item Type will change the IsVisible behavior.
	/// It will be used also to display a specific icon per item in the treeview
	/// </remarks>
	internal enum LayerTreeViewElementType
	{
		Root,
		Table,
		Layer,
		Item
	}
}
