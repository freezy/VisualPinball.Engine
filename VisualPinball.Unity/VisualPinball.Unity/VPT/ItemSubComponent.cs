using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Sub-components only deal with a specific aspect of the component and
	/// grab the data from their parent (which can sit on the same GameObject
	/// or the parent or grand parent). <p/>
	///
	/// Sub-components are collider- mesh- and movement components.
	/// </summary>
	///
	/// <remarks>
	/// Note we don't cache the data reference in order having to detect when
	/// the component gets re-parented.
	/// </remarks>
	/// <typeparam name="TData">Data type of the item</typeparam>
	/// <typeparam name="TMainComponent">Type of the main component, where the data is.</typeparam>
	public abstract class ItemSubComponent<TData, TMainComponent> : ItemComponent
		where TData : ItemData
		where TMainComponent : ItemMainComponent<TData>
	{
		/// <summary>
		/// Finds the main component in the parent.
		/// </summary>
		public TMainComponent MainComponent => FindMainComponent();

		public bool HasMainComponent => FindMainComponent() == null;

		public override string ItemName => MainComponent.ItemName;

		public IItemMainRenderableComponent ParentComponent => MainComponent.ParentComponent;

		public abstract IEnumerable<Type> ValidParents { get; }

		public bool IsCorrectlyParented {
			get {
				var parentComponent = ParentComponent;
				return parentComponent == null || ValidParents.Any(validParent => parentComponent.GetType() == validParent);
			}
		}

		private TMainComponent FindMainComponent()
		{
			var go = gameObject;

			// search on current game object
			var ac = go.GetComponent<TMainComponent>();
			if (ac != null) {
				return ac;
			}
			if (this is IItemColliderComponent) {
				// collider must be on the same game object
				return null;
			}

			// search on parent
			if (go.transform.parent != null) {
				ac = go.transform.parent.GetComponent<TMainComponent>();
			}
			if (ac != null) {
				return ac;
			}

			// search on grand parent
			if (go.transform.parent != null && go.transform.parent.transform.parent != null) {
				ac = go.transform.parent.transform.parent.GetComponent<TMainComponent>();
			}
			if (ac != null) {
				return ac;
			}

			// search on great grand parent
			if (go.transform.parent != null && go.transform.parent.transform.parent != null && go.transform.parent.transform.parent.transform.parent != null) {
				ac = go.transform.parent.transform.parent.transform.parent.GetComponent<TMainComponent>();
			}

			if (ac == null) {
				Debug.LogWarning("No same- or parent component found.");
			}

			return ac;
		}
	}
}
