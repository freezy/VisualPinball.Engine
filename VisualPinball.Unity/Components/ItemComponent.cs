using System.Linq;
using NLog;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT;
using VisualPinball.Unity.Extensions;
using Logger = NLog.Logger;

namespace VisualPinball.Unity.Components
{
	public abstract class ItemComponent<TItem, TData> : MonoBehaviour where TData : ItemData where TItem : Item<TData>, IRenderable
	{
		[SerializeField]
		public TData data;

		public TItem Item => _item ?? (_item = GetItem());
		private TItem _item;

		private readonly Logger _logger = LogManager.GetCurrentClassLogger();

		public void SetData(TData d)
		{
			name = d.GetName();
			data = d;
		}

		public void RebuildMeshes()
		{
			if (data == null) {
				_logger.Warn("Cannot retrieve data component for a {0}.", typeof(TItem).Name);
				return;
			}
			var table = transform.root.GetComponent<VisualPinballTable>().Table;
			if (table == null) {
				_logger.Warn("Cannot retrieve table component from {0}, not updating meshes.", data.GetName());
				return;
			}

			var rog = Item.GetRenderObjects(table, Origin.Original, false);
			var children = Children;
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
				_logger.Warn("Cannot find mesh {0} in {1} {2}.", childName, typeof(TItem).Name, data.GetName());
				return;
			}
			var unityMesh = go.GetComponent<MeshFilter>().sharedMesh;
			ro.Mesh.ApplyToUnityMesh(unityMesh);
		}

		protected abstract string[] Children { get; }

		protected abstract TItem GetItem();
	}
}
