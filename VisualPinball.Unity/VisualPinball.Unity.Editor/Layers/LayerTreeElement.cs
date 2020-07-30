using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Editor.Utils.TreeView;
using VisualPinball.Unity.VPT;

namespace VisualPinball.Unity.Editor.Layers
{
	/// <summary>
	/// Enum for LayerTreeElement visibility status (including children)
	/// </summary>
	internal enum LayerTreeElementVisibility
	{
		Hidden,			// Element is hidden and children also
		Hidden_Mixed,	// Element is hidden but some children are visible
		Visible_Mixed,	// Element is visible but some children are hidden
		Visible			// Element is visible and children also
	}

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

		public override string DisplayName
		{
			get {
				switch (Type) {
					case LayerTreeViewElementType.Table: {
						return $"{Name} [{Children.Count} layers]";
					}

					case LayerTreeViewElementType.Layer: {
						return $"{Name} [{Children.Count} items]";
					}

					default: {
						return base.DisplayName;
					}
				}
			}
		}

		public override Texture2D Icon
		{
			get {
				switch (Type) {
					case LayerTreeViewElementType.Table: {
						return EditorGUIUtility.FindTexture("d_winbtn_graph");
					}

					case LayerTreeViewElementType.Layer: {
						return EditorGUIUtility.IconContent("ToggleUVOverlay").image as Texture2D;
					}

					default: {
						return EditorGUIUtility.IconContent("GameObject Icon").image as Texture2D;
					}
				}
			}
		}

		/// <summary>
		/// Expose the Visibility of a LayerTreeElement, telling if all its children have the same visibility status than its own.
		/// </summary>
		public LayerTreeElementVisibility Visibility
		{
			get {
				switch (Type) {
					case LayerTreeViewElementType.Table:
					case LayerTreeViewElementType.Layer: {
						bool mixed = false;
						foreach(var subItem in Children) {
							if (((LayerTreeElement)subItem).IsVisible != IsVisible) {
								mixed = true;
								break;
							}
						}
						if (mixed) {
							return IsVisible ? LayerTreeElementVisibility.Visible_Mixed : LayerTreeElementVisibility.Hidden_Mixed;
						} else {
							return IsVisible ? LayerTreeElementVisibility.Visible : LayerTreeElementVisibility.Hidden;
						}
					}

					case LayerTreeViewElementType.Item: {
						return IsVisible ? LayerTreeElementVisibility.Visible : LayerTreeElementVisibility.Hidden;
					}
					
					default: {
						return LayerTreeElementVisibility.Hidden;
					}
				}
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
