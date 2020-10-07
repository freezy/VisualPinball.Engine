using UnityEngine;
using VisualPinball.Engine.Game;
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
	/// <typeparam name="TItem">Type of the item</typeparam>
	/// <typeparam name="TData">Data type of the item</typeparam>
	/// <typeparam name="TMainAuthoring">Type of the main component, where the data is.</typeparam>
	public abstract class ItemSubAuthoring<TItem, TData, TMainAuthoring> : ItemAuthoring<TItem, TData>
		where TItem : Item<TData>, IRenderable
		where TData : ItemData
		where TMainAuthoring : ItemMainAuthoring<TItem, TData>
	{
		/// <summary>
		/// We're in a sub component here, so in order to retrieve the data,
		/// this will:
		/// 1. Check if <see cref="ItemAuthoring{TItem,TData}._data"/> is set (e.g. it's a serialized item)
		/// 2. Find the main component in the hierarchy and return its data.
		/// </summary>
		///
		/// <remarks>
		/// We deliberately don't cache this, because if we do we need to find
		/// a way to invalidate the cache in case the game object gets
		/// re-attached to another parent.
		/// </remarks>
		public override TData Data => FindData();

		/// <summary>
		/// Since we're in a sub component, we don't instantiate the item, but
		/// look for the main component and retrieve the item from there (which
		/// will instantiate it itself if necessary).
		/// </summary>
		///
		/// <remarks>
		/// If no main component found, this yields to `null`, and in this case
		/// the component is somewhere in the hierarchy where it doesn't make
		/// sense, and a warning should be printed.
		/// </remarks>
		public override TItem Item => FindItem();

		/// <summary>
		/// Finds the main authoring component in the parent.
		/// </summary>
		protected TMainAuthoring MainAuthoring => FindMainAuthoring();

		// public IItemAuthoring SetItem(TItem item, RenderObjectGroup rog)
		// {
		// 	_item = item;
		// 	_data = item.Data;
		// 	_isSubComponent = false;
		// 	name = rog.ComponentName + " (collider)";
		// 	return this;
		// }

		// public void SetMainItem(TItem item)
		// {
		// 	_item = item;
		// 	_isSubComponent = true;
		// }

		private TData FindData()
		{
			var ac = FindMainAuthoring();
			return ac != null ? ac.Data : null;
		}

		private TItem FindItem()
		{
			// otherwise retrieve from parent
			var ac = FindMainAuthoring();
			return ac != null ? ac.Item : null;
		}

		private TMainAuthoring FindMainAuthoring()
		{
			var go = gameObject;

			// search on current game object
			var ac = go.GetComponent<TMainAuthoring>();
			if (ac != null) {
				return ac;
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

			if (ac == null) {
				Debug.LogWarning("No same- or parent authoring component found.");
			}

			return ac;
		}
	}
}
