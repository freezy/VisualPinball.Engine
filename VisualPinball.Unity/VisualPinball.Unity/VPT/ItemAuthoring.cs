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

// ReSharper disable InconsistentNaming

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NLog;
using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Table;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	/// <summary>
	/// The base class for all authoring components on the playfield.<p/>
	/// </summary>
	/// <typeparam name="TItem"></typeparam>
	/// <typeparam name="TData"></typeparam>
	public abstract class ItemAuthoring<TItem, TData> : MonoBehaviour, IItemAuthoring, IEditableItemAuthoring, IIdentifiableItemAuthoring,
		ILayerableItemAuthoring where TData : ItemData where TItem : Item<TData>, IRenderable
	{
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
		protected TData _data;

		/// <summary>
		/// The game item object. This is not serialized and gets re-instantiated
		/// and cached here.
		/// </summary>
		[NonSerialized]
		protected TItem _item;

		/// <summary>
		/// Returns the data object relevant for this component. If this
		/// returns `null`, then it's wrongly attached to a game object
		/// where it can't find its main component.
		/// </summary>
		///
		/// <remarks>
		/// The default implementation here represents the one of a main
		/// component. It's overridden for the sub components (<see cref="ItemColliderAuthoring{TItem,TData,TAuthoring}"/>, etc)
		/// </remarks>
		public virtual TData Data => _data;

		/// <summary>
		/// Returns the item object for this component. If this
		/// returns `null`, then it's wrongly attached to a game object
		/// where it can't find its main component.
		/// </summary>
		///
		/// <remarks>
		/// For any game item, we only serialize its data and re-instantiate
		/// the actual item on the fly (and cache it). So in <see cref="SetItem"/>,
		/// which initializes the component after creation, <see cref="_item"/>
		/// is only set to avoid a cache miss.
		/// </remarks>
		public virtual TItem Item => _item ?? (_item = InstantiateItem(_data));

		/// <summary>
		/// A non-typed version of the item.
		/// </summary>
		public IItem IItem => _item;

		/// <summary>
		/// The data-oriented version of the item.
		/// </summary>
		public ItemData ItemData => _data;


		public string ItemType => Item.ItemType;

		public bool IsLocked { get => _data.IsLocked; set => _data.IsLocked = value; }

		public List<MemberInfo> MaterialRefs => _materialRefs ?? (_materialRefs = GetMembersWithAttribute<MaterialReferenceAttribute>());
		public List<MemberInfo> TextureRefs => _textureRefs ?? (_textureRefs = GetMembersWithAttribute<TextureReferenceAttribute>());

		private Table _table;

		protected Table Table => _table ?? (_table = gameObject.transform.GetComponentInParent<TableAuthoring>()?.Item);

		private List<MemberInfo> _materialRefs;
		private List<MemberInfo> _textureRefs;

		private readonly Logger _logger = LogManager.GetCurrentClassLogger();

		// for tracking if we need to rebuild the meshes (handled by the editor scripts) during undo/redo flows
		[HideInInspector]
		[SerializeField]
		private bool _meshDirty;
		public bool MeshDirty { get => _meshDirty; set => _meshDirty = value; }

		public ItemAuthoring<TItem, TData> SetItem(TItem item, string gameObjectName = null)
		{
			_item = item;
			_data = item.Data;
			name = gameObjectName ?? _data.GetName();
			ItemDataChanged();
			return this;
		}

		public void RebuildMeshes()
		{
			if (_data == null) {
				_logger.Warn("Cannot retrieve data component for a {0}.", typeof(TItem).Name);
				return;
			}
			var table = transform.GetComponentInParent<TableAuthoring>();
			if (table == null) {
				_logger.Warn("Cannot retrieve table component from {0}, not updating meshes.", _data.GetName());
				return;
			}

			var rog = Item.GetRenderObjects(table.Table, Origin.Original, false);
			var children = Children;
			if (children == null) {
				UpdateMesh(Item.Name, gameObject, rog, table);
			} else {
				foreach (var child in children) {
					if (transform.childCount == 0) {
						//Find the matching  renderObject  and Update it based on base gameObject
						var ro = rog.RenderObjects.FirstOrDefault(r => r.Name == child);
						if (ro != null)
						{
							UpdateMesh(child, gameObject, rog, table);
							break;
						}
					} else {
						Transform childTransform = transform.Find(child);
						if (childTransform != null) {
							UpdateMesh(child, childTransform.gameObject, rog, table);
						} else {
							// child hasn't been created yet (i.e. ramp might have changed type)
							var ro = rog.RenderObjects.FirstOrDefault(r => r.Name == child);
							if (ro != null) {
								var subObj = new GameObject(ro.Name);
								subObj.transform.SetParent(transform, false);
								subObj.layer = VpxConverter.ChildObjectsLayer;
							}
						}
					}
				}
			}
			// update transform based on item data, but not for "Table" since its the effective "root" and the user might want to move it on their own
			if (table != this) {
				transform.SetFromMatrix(rog.TransformationMatrix.ToUnityMatrix());
			}
			ItemDataChanged();
			_meshDirty = false;
		}

		protected virtual void ItemDataChanged() {}

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
			Item.Version = entity.Version;
		}


		public void LinkChild(IItemAuthoring childItem)
		{
			if (childItem is IItemColliderAuthoring) {
				Table.AddColliderOverride(Item, childItem.IItem as IHittable);
			}
		}

		protected virtual void OnDrawGizmos()
		{
			// handle dirty whenever scene view draws just in case a field or dependant changed and our
			// custom inspector window isn't up to process it
			if (_meshDirty) {
				RebuildMeshes();
			}

			// Draw invisible gizmos over top of the sub meshes of this item so clicking in the scene view
			// selects the item itself first, which is most likely what the user would want
			var mfs = GetComponentsInChildren<MeshFilter>();
			Gizmos.color = Color.clear;
			Gizmos.matrix = Matrix4x4.identity;
			foreach (var mf in mfs) {
				Gizmos.DrawMesh(mf.sharedMesh, mf.transform.position, mf.transform.rotation, mf.transform.lossyScale);
			}
		}

		private static void UpdateMesh(string childName, GameObject go, RenderObjectGroup rog, TableAuthoring table)
		{
			var mr = go.GetComponent<MeshRenderer>();
			var ro = rog.RenderObjects.FirstOrDefault(r => r.Name == childName);
			if (ro == null || !ro.IsVisible) {
				if (mr != null) {
					mr.enabled = false;
				}
				return;
			}
			var mf = go.GetComponent<MeshFilter>();
			if (mf != null) {
				var unityMesh = mf.sharedMesh;
				ro.Mesh.ApplyToUnityMesh(unityMesh);
			}

			if (mr != null) {
				if (table != null) {
					mr.sharedMaterial = ro.Material.ToUnityMaterial(table);
				}
				mr.enabled = true;
			}
		}

		private List<MemberInfo> GetMembersWithAttribute<TAttr>() where TAttr: Attribute
		{
			List<MemberInfo> members = new List<MemberInfo>();
			foreach (var member in typeof(TData).GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) {
				if (member.GetCustomAttribute<TAttr>() != null) {
					members.Add(member);
				}
			}
			return members;
		}

		protected abstract string[] Children { get; }

		protected abstract TItem InstantiateItem(TData data);

		public string Name { get => Item.Name; set => Item.Name = value; }

		public int EditorLayer { get => _data.EditorLayer; set => _data.EditorLayer = value; }
		public string EditorLayerName { get => _data.EditorLayerName; set => _data.EditorLayerName = value; }
		public bool EditorLayerVisibility { get => _data.EditorLayerVisibility; set => _data.EditorLayerVisibility = value; }
	}
}
