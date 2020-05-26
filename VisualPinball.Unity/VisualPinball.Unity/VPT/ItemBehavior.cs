using System.Linq;
using NLog;
using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Extensions;
using VisualPinball.Unity.VPT.Table;
using Logger = NLog.Logger;

namespace VisualPinball.Unity.VPT
{
	public abstract class ItemBehavior<TItem, TData> : MonoBehaviour, IItemDataTransformable where TData : ItemData where TItem : Item<TData>, IRenderable
	{
		[SerializeField]
		public TData data;

		public TItem Item => _item ?? (_item = GetItem());

		protected TableData _tableData;
		private TItem _item;

		private readonly Logger _logger = LogManager.GetCurrentClassLogger();

		// for tracking if we need to rebuild the meshes (handled by the editor scripts) during undo/redo flows
		[SerializeField]
		private bool _meshDirty;
		public bool MeshDirty { get { return _meshDirty; } set { _meshDirty = value; } }
		public virtual bool RebuildMeshOnMove => false;
		public virtual bool RebuildMeshOnScale => false;

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
			var table = transform.GetComponentInParent<TableBehavior>().Table;
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
					Transform childTransform = transform.Find(child);
					if (childTransform != null) {
						UpdateMesh(child, childTransform.gameObject, rog);
					}
				}
			}
			_meshDirty = false;
		}

		public virtual ItemDataTransformType EditorPositionType => ItemDataTransformType.None;
		public virtual Vector3 GetEditorPosition() { return Vector3.zero; }
		public virtual void SetEditorPosition(Vector3 pos) { }

		public virtual ItemDataTransformType EditorRotationType => ItemDataTransformType.None;
		public virtual Vector3 GetEditorRotation() { return Vector3.zero; }
		public virtual void SetEditorRotation(Vector3 rot) { }

		public virtual ItemDataTransformType EditorScaleType => ItemDataTransformType.None;
		public virtual Vector3 GetEditorScale() { return Vector3.zero; }
		public virtual void SetEditorScale(Vector3 rot) { }

		protected void Convert(Entity entity, EntityManager dstManager)
		{
			Item.Index = entity.Index;
		}

		protected virtual void Awake()
		{
			var rootObj = gameObject.transform.GetComponentInParent<TableBehavior>();
			// can be null in editor, shouldn't be at runtime.
			if (rootObj != null) {
				_tableData = rootObj.data;
			}
		}

		protected virtual void OnDrawGizmos()
		{
			// Draw invisible gizmos over top of the sub meshes of this item so clicking in the scene view
			// selects the item itself first, which is most likely what the user would want
			var mfs = this.GetComponentsInChildren<MeshFilter>();
			Gizmos.color = Color.clear;
			Gizmos.matrix = Matrix4x4.identity;
			foreach (var mf in mfs) {
				Gizmos.DrawMesh(mf.sharedMesh, mf.transform.position, mf.transform.rotation, mf.transform.lossyScale);
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
