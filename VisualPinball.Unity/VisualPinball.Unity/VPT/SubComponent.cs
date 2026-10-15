using System.Linq;
using NLog;
using UnityEngine;
using VisualPinball.Engine.VPT;
using Logger = NLog.Logger;

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
	public abstract class SubComponent<TData, TMainComponent> : ItemComponent
		where TData : ItemData
		where TMainComponent : MainComponent<TData>
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private static readonly LogOncePerKey<int> NoMainComponentLog = new();

		/// <summary>
		/// Finds the main component in the parent.
		/// </summary>
		public TMainComponent MainComponent => FindMainComponent();

		public bool HasMainComponent => FindMainComponent() == null;

		public override string ItemName => MainComponent.ItemName;

		private TMainComponent FindMainComponent()
		{
			var go = gameObject;

			// search on current game object
			var ac = go.GetComponent<TMainComponent>();
			if (ac != null) {
				return ac;
			}
			if (this is ICollidableComponent) {
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
				if (NoMainComponentLog.ShouldLog(UnityObjectId.Get(this))) {
					Logger.Warn("{Component:l} on {Name} has no {MainComponent:l} on itself or its parents.", GetType().Name, name, typeof(TMainComponent).Name);
				}
			}

			return ac;
		}
	}
}
