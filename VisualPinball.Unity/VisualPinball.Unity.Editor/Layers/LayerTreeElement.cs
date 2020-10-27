// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// Enum for LayerTreeElement visibility status (including children)
	/// </summary>
	internal enum LayerTreeElementVisibility
	{
		Hidden,			// Element is hidden and children also
		HiddenMixed,	// Element is hidden but some children are visible
		VisibleMixed,	// Element is visible but some children are hidden
		Visible			// Element is visible and children also
	}

	/// <summary>
	/// Element type used in tree model for the <see cref="LayerTreeView"/>. <p/>
	///
	/// It represents one of multiple types of elements, see <see cref="LayerTreeViewElementType"/>
	/// </summary>
	internal class LayerTreeElement : TreeElement
	{
		public string LayerName;
		public ILayerableItemAuthoring Item { get; }

		private readonly Table _table;

		/// <summary>
		/// Returns the type based on which data is set.
		/// </summary>
		public LayerTreeViewElementType Type =>
			_table != null
				? LayerTreeViewElementType.Table
				: Item != null
					? LayerTreeViewElementType.Item
					: LayerName != null ? LayerTreeViewElementType.Layer : LayerTreeViewElementType.Root;


		/// <summary>
		/// Dispatches the visibility flag to the game item data.
		/// </summary>
		public bool IsVisible
		{
			get {
				if (Type == LayerTreeViewElementType.Item) {
					if (Item != null && Item is MonoBehaviour behaviour) {
						var obj = behaviour.gameObject;
						_isVisible = obj != null && !SceneVisibilityManager.instance.IsHidden(obj);

					} else {
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
					if (behaviour != null && behaviour.gameObject != null) {
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
		private bool _isVisible = true;

		/// <summary>
		/// The name of the game item if it's a game item.
		/// </summary>
		public override string Name {
			get {
				switch (Type) {
					case LayerTreeViewElementType.Table: {
						return _table?.Name;
					}

					case LayerTreeViewElementType.Layer: {
						return LayerName ?? string.Empty;
					}

					case LayerTreeViewElementType.Item: {
						if (Item is IIdentifiableItemAuthoring identifiable) {
							return identifiable.Name;
						}
						return string.Empty;
					}

					default: {
						return "<Root>";
					}
				}
			}
		}

		/// <summary>
		///
		/// </summary>
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

		private static readonly Dictionary<LayerTreeViewElementType, Texture> TypeToIcon = new Dictionary<LayerTreeViewElementType, Texture> {
			{ LayerTreeViewElementType.Root, null},
			{ LayerTreeViewElementType.Table, Icons.Table(IconSize.Small)},
			{ LayerTreeViewElementType.Layer, EditorGUIUtility.IconContent("ToggleUVOverlay").image},
			{ LayerTreeViewElementType.Item, EditorGUIUtility.IconContent("GameObject Icon").image},
		};

		public override Texture2D Icon => Type == LayerTreeViewElementType.Item
			? Icons.ByComponent(Item, IconSize.Small)
			: TypeToIcon[Type] as Texture2D;

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
							return IsVisible ? LayerTreeElementVisibility.VisibleMixed : LayerTreeElementVisibility.HiddenMixed;
						}

						return IsVisible ? LayerTreeElementVisibility.Visible : LayerTreeElementVisibility.Hidden;
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

		#region Constructors

		/// <summary>
		/// Default results in a <see cref="LayerTreeViewElementType.Root"/>.
		/// </summary>
		public LayerTreeElement(){}

		/// <summary>
		/// Construct as <see cref="LayerTreeViewElementType.Table"/>.
		/// </summary>
		/// <param name="table">Table object</param>
		public LayerTreeElement(Table table)
		{
			_table = table;
		}

		/// <summary>
		/// Construct as <see cref="LayerTreeViewElementType.Item"/>.
		/// </summary>
		/// <param name="item">Game item behavior</param>
		public LayerTreeElement(ILayerableItemAuthoring item)
		{
			Item = item;
			IsVisible = Item.EditorLayerVisibility;
		}

		/// <summary>
		/// Construct as <see cref="LayerTreeViewElementType.Layer"/>.
		/// </summary>
		/// <param name="layerName">Name of the layer</param>
		public LayerTreeElement(string layerName)
		{
			LayerName = layerName;
		}

		#endregion

		public override TreeElement ReParent(TreeElement newParent)
		{
			var oldParent = base.ReParent(newParent);
			// Update Layer BiffData when reparenting an Item on a Layer
			if (Type == LayerTreeViewElementType.Item &&
					newParent is LayerTreeElement layerParent &&
					layerParent.Type == LayerTreeViewElementType.Layer) {
				if (Item is MonoBehaviour behaviour) {
					Undo.RecordObject(behaviour, $"{behaviour.name}: Change layer from {Item.EditorLayerName} to {layerParent.LayerName}.");
				}
				Item.EditorLayerName = layerParent.LayerName;
			}
			return oldParent;
		}
	}

	/// <summary>
	/// Enum for different Item Types in the LayerTreeView. This allows us to
	/// re-use the same element class for the tree view.
	/// </summary>
	///
	/// <remarks>
	/// This type will change the <code>IsVisible</code> of the game item data.
	/// It will be used also to display a specific icon per item in the tree view.
	/// </remarks>
	internal enum LayerTreeViewElementType
	{
		/// <summary>
		/// The first obligatory node of the tree view, whose only purpose is
		/// to be the root.
		/// </summary>
		Root,

		/// <summary>
		/// The table node is the first node displayed in the layer window.
		/// </summary>
		Table,

		/// <summary>
		/// The layer node groups items per layer
		/// </summary>
		Layer,

		/// <summary>
		/// A game item.
		/// </summary>
		Item
	}
}
