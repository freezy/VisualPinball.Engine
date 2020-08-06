using System.Collections.Generic;
using System.Linq;
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
					if (Item is Behaviour behaviour) {
						_isVisible = !SceneVisibilityManager.instance.IsHidden(behaviour.gameObject);
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
					var behaviour = Item as MonoBehaviour;
					if (behaviour != null) {
						Undo.RecordObject(behaviour, $"{behaviour.name} : Switch visibility to {_isVisible}.");
					}
					Item.EditorLayerVisibility = _isVisible;
					if (behaviour != null) {
						if (_isVisible) {
							SceneVisibilityManager.instance.Show(behaviour.gameObject, true);
						}
						else {
							SceneVisibilityManager.instance.Hide(behaviour.gameObject, true);
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

		internal static Dictionary<LayerTreeViewElementType, Texture> _typeToIcon = new Dictionary<LayerTreeViewElementType, Texture>() {
			{ LayerTreeViewElementType.Root, null},
			{ LayerTreeViewElementType.Table, EditorGUIUtility.IconContent("d_winbtn_graph").image},
			{ LayerTreeViewElementType.Layer, EditorGUIUtility.IconContent("ToggleUVOverlay").image},
			{ LayerTreeViewElementType.Item, EditorGUIUtility.IconContent("GameObject Icon").image},
		};
		public override Texture2D Icon => _typeToIcon[Type] as Texture2D;

		/// <summary>
		/// Expose the Visibility of a LayerTreeElement, telling if all its children have the same visibility status than its own.
		/// </summary>
		public LayerTreeElementVisibility Visibility
		{
			get {
				switch (Type) {
					case LayerTreeViewElementType.Table:
					case LayerTreeViewElementType.Layer: {
						if (Children.Any(e => ((LayerTreeElement)e).IsVisible != IsVisible)) {
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

		#region CTors
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
		#endregion

		public override void ReParent(TreeElement newParent)
		{
			base.ReParent(newParent);
			//Update Layer BiffData when reparenting an Item on a Layer
			if (Type == LayerTreeViewElementType.Item && 
					newParent is LayerTreeElement layerParent && 
					layerParent.Type == LayerTreeViewElementType.Layer) {
				if (Item is MonoBehaviour behaviour) {
					Undo.RecordObject(behaviour, $"{behaviour.name} : Change layer from {Item.EditorLayerName} to {layerParent.LayerName}.");
				}
				Item.EditorLayerName = layerParent.LayerName;
			}
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
