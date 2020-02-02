using UnityEngine;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity.Editor
{
	public abstract class ItemComponent<TItem, TData> : MonoBehaviour where TData : ItemData where TItem : Item<TData>
	{
		protected TItem Item => _item ?? (_item = GetItem(_data));

		private TItem _item;

		[SerializeField]
		[HideInInspector]
		protected TData _data;

		public void SetData(TData data)
		{
			_data = data;
			_item = GetItem(data);
			OnDataSet(_data);
		}

		protected abstract TItem GetItem(TData data);

		protected abstract void OnDataSet(TData data);
	}
}
