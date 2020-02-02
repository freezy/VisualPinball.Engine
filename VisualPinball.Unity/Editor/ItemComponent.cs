using System.Linq;
using NLog;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT;
using VisualPinball.Unity.Extensions;
using Logger = NLog.Logger;

namespace VisualPinball.Unity.Editor
{
	public abstract class ItemComponent<TItem, TData> : MonoBehaviour where TData : ItemData where TItem : Item<TData>, IRenderable
	{
		[SerializeField]
		[HideInInspector]
		protected TData data;

		protected TItem Item => _item ?? (_item = GetItem(data));
		private TItem _item;

		private readonly Logger _logger = LogManager.GetCurrentClassLogger();

		public void SetData(TData d)
		{
			data = d;
			_item = GetItem(d);
			OnDataSet();
		}

		protected void UpdateMeshes()
		{
			if (data == null) {
				_logger.Warn("Cannot retrieve data component for a {0}.", typeof(TItem).Name);
				return;
			}
			var table = transform.root.GetComponent<TableComponent>().Table;
			if (table == null) {
				_logger.Warn("Cannot retrieve table component from {0}, not updating meshes.", data.Name);
				return;
			}

			var rog = Item.GetRenderObjects(table, Origin.Original, false);
			var children = GetChildren();
			if (children == null) {
				UpdateMesh(Item.Name, gameObject, rog);
			} else {
				foreach (var child in children) {
					UpdateMesh(child, transform.Find(child).gameObject, rog);
				}
			}
		}

		private void UpdateMesh(string childName, GameObject go, RenderObjectGroup rog)
		{
			var ro = rog.RenderObjects.FirstOrDefault(r => r.Name == childName);
			if (ro == null) {
				_logger.Warn("Cannot find mesh {0} in {1} {2}.", childName, typeof(TItem).Name, data.Name);
				return;
			}
			var unityMesh = go.GetComponent<MeshFilter>().sharedMesh;
			ro.Mesh.ApplyToUnityMesh(unityMesh);
		}

		protected abstract TItem GetItem(TData data);

		protected abstract void OnDataSet();

		protected abstract string[] GetChildren();
	}
}
