using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity
{
	public abstract class ItemMainAuthoring<TItem, TData> : ItemAuthoring<TItem, TData>,
		IItemMainAuthoring, ILayerableItemAuthoring, IIdentifiableItemAuthoring
		where TItem : Item<TData>, IRenderable
		where TData : ItemData
	{
		/// <summary>
		/// Returns the serialized data.
		/// </summary>
		public override TData Data => _data;

		/// <summary>
		/// Instantiates a new item based on the serialized data, and caches it
		/// for the next access.
		/// </summary>
		public override TItem Item => _item ?? (_item = InstantiateItem(_data));

		public virtual bool CanBeTransformed => true;

		/// <summary>
		/// List of types for parenting. Empty list if only to own parent.
		/// </summary>
		public abstract IEnumerable<Type> ValidParents { get; }

		/// <summary>
		/// Authoring type of the child class.
		/// </summary>
		protected abstract Type MeshAuthoringType { get; }

		protected abstract Type ColliderAuthoringType { get; }

		/// <summary>
		/// Instantiates a new item based on the item data.
		/// </summary>
		/// <param name="data">Item data</param>
		/// <returns>New item instance</returns>
		protected abstract TItem InstantiateItem(TData data);

		/// <summary>
		/// Applies the GameObject data to the item data. typically name and visibility.
		/// </summary>
		public abstract void Restore();

		/// <summary>
		/// The serialized data, as written to the .vpx file.
		/// </summary>
		///
		/// <remarks>
		/// This might be "empty" (since Unity can't serialize it as `null`), so
		/// the component authoring classes keep a flag whether to read the data
		/// from this field or retrieve it from the parent in the hierarchy.
		/// </remarks>
		[SerializeField]
		private TData _data;

		/// <summary>
		/// The game item object. This is not serialized and gets re-instantiated
		/// and cached here.
		/// </summary>
		[NonSerialized]
		private TItem _item;

		/// <summary>
		/// Returns all child mesh components linked to this data.
		/// </summary>
		protected IEnumerable<IItemMeshAuthoring> MeshComponents => MeshAuthoringType != null ?
			GetComponentsInChildren(MeshAuthoringType, true)
				.Select(c => (IItemMeshAuthoring) c)
				.Where(ma => ma.ItemData == _data) : new IItemMeshAuthoring[0];

		protected IEnumerable<IItemColliderAuthoring> ColliderComponents => ColliderAuthoringType != null ?
			GetComponentsInChildren(ColliderAuthoringType, true)
				.Select(c => (IItemColliderAuthoring) c)
				.Where(ca => ca.ItemData == _data) : new IItemColliderAuthoring[0];

		public IItemMainAuthoring ParentAuthoring => FindParentAuthoring();

		public bool IsCorrectlyParented {
			get {
				var parentAuthoring = ParentAuthoring;
				return parentAuthoring == null || ValidParents.Any(validParent => parentAuthoring.GetType() == validParent);
			}
		}

		public IItemMainAuthoring SetItem(TItem item, string gameObjectName = null)
		{
			_item = item;
			_data = item.Data;
			name = gameObjectName ?? _data.GetName();
			ItemDataChanged();
			return this;
		}

		public void SetMeshDirty()
		{
			foreach (var meshComponent in MeshComponents) {
				meshComponent.MeshDirty = true;
			}
		}

		public void RebuildMeshIfDirty()
		{
			foreach (var meshComponent in MeshComponents) {
				if (meshComponent.MeshDirty) {
					meshComponent.RebuildMeshes();
				}
			}

			// update transform based on item data, but not for "Table" since its the effective "root" and the user might want to move it on their own
			var ta = GetComponentInParent<TableAuthoring>();
			if (ta != this) {
				transform.SetFromMatrix(Item.TransformationMatrix(Table, Origin.Original).ToUnityMatrix());
			}
		}

		public void Destroy()
		{
			DestroyImmediate(gameObject);
		}

		public void DestroyMeshComponent()
		{
			foreach (var component in MeshComponents) {
				var mb = component as MonoBehaviour;

				// if game object is the same, remove component
				if (mb.gameObject == gameObject) {
					DestroyImmediate(mb);

				} else {
					// otherwise, destroy entire game object
					DestroyImmediate(mb.gameObject);
				}
			}
		}

		public void DestroyColliderComponent()
		{
			foreach (var component in ColliderComponents) {
				var mb = component as MonoBehaviour;

				// if game object is the same, remove component
				if (mb.gameObject == gameObject) {
					DestroyImmediate(mb);

				} else {
					// otherwise, destroy entire game object
					DestroyImmediate(mb.gameObject);
				}
			}
		}

		protected void Convert(Entity entity, EntityManager dstManager)
		{
			Item.Index = entity.Index;
			Item.Version = entity.Version;

			var parentAuthoring = ParentAuthoring;
			if (parentAuthoring != null && !(parentAuthoring is TableAuthoring)) {
				Item.ParentIndex = parentAuthoring.IItem.Index;
				Item.ParentVersion = parentAuthoring.IItem.Version;
			}
		}

		protected virtual void OnDrawGizmos()
		{
			// handle dirty whenever scene view draws just in case a field or dependant changed and our
			// custom inspector window isn't up to process it
			RebuildMeshIfDirty();

			// Draw invisible gizmos over top of the sub meshes of this item so clicking in the scene view
			// selects the item itself first, which is most likely what the user would want
			var mfs = GetComponentsInChildren<MeshFilter>();
			Gizmos.color = Color.clear;
			Gizmos.matrix = Matrix4x4.identity;
			foreach (var mf in mfs) {
				var t = mf.transform;
				Gizmos.DrawMesh(mf.sharedMesh, t.position, t.rotation, t.lossyScale);
			}
		}

		private IItemMainAuthoring FindParentAuthoring()
		{
			IItemMainAuthoring ma = null;
			var go = gameObject;

			// search on parent
			if (go.transform.parent != null) {
				ma = go.transform.parent.GetComponent<IItemMainAuthoring>();
			}

			if (ma is MonoBehaviour mb && mb.GetComponent<TableAuthoring>() != null) {
				return null;
			}
			if (ma != null) {
				return ma;
			}

			// search on grand parent
			if (go.transform.parent.transform.parent != null) {
				ma = go.transform.parent.transform.parent.GetComponent<IItemMainAuthoring>();
			}

			if (ma is MonoBehaviour mb2 && mb2.GetComponent<TableAuthoring>() != null) {
				return null;
			}

			return ma;
		}

		#region Tools

		public virtual ItemDataTransformType EditorPositionType => ItemDataTransformType.None;
		public virtual Vector3 GetEditorPosition() => Vector3.zero;
		public virtual void SetEditorPosition(Vector3 pos) { }

		public virtual ItemDataTransformType EditorRotationType => ItemDataTransformType.None;
		public virtual Vector3 GetEditorRotation() => Vector3.zero;
		public virtual void SetEditorRotation(Vector3 rot) { }

		public virtual ItemDataTransformType EditorScaleType => ItemDataTransformType.None;
		public virtual Vector3 GetEditorScale() => Vector3.zero;
		public virtual void SetEditorScale(Vector3 rot) { }

		#endregion

		public int EditorLayer { get => Data.EditorLayer; set => Data.EditorLayer = value; }
		public string EditorLayerName { get => Data.EditorLayerName; set => Data.EditorLayerName = value; }
		public bool EditorLayerVisibility { get => Data.EditorLayerVisibility; set => Data.EditorLayerVisibility = value; }
	}
}
