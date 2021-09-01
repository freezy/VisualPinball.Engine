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
	/// <typeparam name="TMainAuthoring">Type of the main component, where the data is.</typeparam>
	public abstract class ItemSubAuthoring<TData, TMainAuthoring> : ItemAuthoring
		where TData : ItemData
		where TMainAuthoring : ItemMainAuthoring<TData>
	{
		/// <summary>
		/// Finds the main authoring component in the parent.
		/// </summary>
		public TMainAuthoring MainComponent => FindMainAuthoring();

		public bool HasMainComponent => FindMainAuthoring() == null;

		public override string ItemName => MainComponent.ItemName;

		public IItemMainRenderableAuthoring ParentAuthoring => MainComponent.ParentAuthoring;

		public abstract IEnumerable<Type> ValidParents { get; }

		public bool IsCorrectlyParented {
			get {
				var parentAuthoring = ParentAuthoring;
				return parentAuthoring == null || ValidParents.Any(validParent => parentAuthoring.GetType() == validParent);
			}
		}

		private TMainAuthoring FindMainAuthoring()
		{
			var go = gameObject;

			// search on current game object
			var ac = go.GetComponent<TMainAuthoring>();
			if (ac != null) {
				return ac;
			}
			if (this is IItemColliderAuthoring) {
				// collider must be on the same game object
				return null;
			}

			// search on parent
			if (go.transform.parent != null) {
				ac = go.transform.parent.GetComponent<TMainAuthoring>();
			}
			if (ac != null) {
				return ac;
			}

			// search on grand parent
			if (go.transform.parent.transform.parent != null) {
				ac = go.transform.parent.transform.parent.GetComponent<TMainAuthoring>();
			}
			if (ac != null) {
				return ac;
			}

			// search on great grand parent
			if (go.transform.parent.transform.parent.transform.parent != null) {
				ac = go.transform.parent.transform.parent.transform.parent.GetComponent<TMainAuthoring>();
			}

			if (ac == null) {
				Debug.LogWarning("No same- or parent authoring component found.");
			}

			return ac;
		}
	}
}
