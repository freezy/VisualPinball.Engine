// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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
using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity
{
	[DisallowMultipleComponent]
	public abstract class ItemMainAuthoring<TItem, TData> : ItemAuthoring<TItem, TData>,
		IItemMainAuthoring, ILayerableItemAuthoring, IIdentifiableItemAuthoring
		where TItem : Item<TData>
		where TData : ItemData
	{
		public float PlayfieldHeight {
			get {
				var playfieldComponent = GetComponentInParent<PlayfieldAuthoring>();
				return playfieldComponent ? playfieldComponent.TableHeight : 0f;
			}
		}

		public abstract IEnumerable<MonoBehaviour> SetData(TData data);
		public abstract IEnumerable<MonoBehaviour> SetReferencedData(TData data, IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IItemMainAuthoring> components);
		public abstract TData CopyDataTo(TData data, string[] materialNames, string[] textureNames);

		public abstract ItemType ItemType { get; }

		protected T GetAuthoring<T>(Dictionary<string, IItemMainAuthoring> components, string surfaceName) where T : class, IItemMainAuthoring
		{
			return (components != null && components.ContainsKey(surfaceName.ToLower())
					? components[surfaceName.ToLower()]
					: null)
				as T;
		}

		public TData CreateData() => CopyDataTo(InstantiateData(), null, null);

		public TItem CreateItem(Dictionary<string, TData> datas, string[] materialNames, string[] textureNames)
			=> InstantiateItem(CopyDataTo(datas.ContainsKey(name) ? datas[name] : InstantiateData(), materialNames, textureNames));

		public TItem CreateItem(string[] materialNames, string[] textureNames)
			=> InstantiateItem(CopyDataTo(InstantiateData(), materialNames, textureNames));

		#region Data

		/// <summary>
		/// Returns the serialized data.
		/// </summary>
		public override TData Data => _data;

		/// <summary>
		/// Instantiates a new item based on the serialized data, and caches it
		/// for the next access.
		/// </summary>
		public override TItem Item => _item ??= InstantiateItem(_data);

		/// <summary>
		/// Instantiates a new item based on the item data.
		/// </summary>
		/// <param name="data">Item data</param>
		/// <returns>New item instance</returns>
		protected abstract TItem InstantiateItem(TData data);

		/// <summary>
		/// Instantiates a new data object with default values.
		/// </summary>
		/// <returns></returns>
		protected abstract TData InstantiateData();

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
		private TItem _item;

		public IItemMainAuthoring SetItem(TItem item, string gameObjectName = null)
		{
			_item = item;
			_data = item.Data;
			name = gameObjectName ?? _data.GetName();
			return this;
		}

		public void Destroy()
		{
			DestroyImmediate(gameObject);
		}

		#endregion

		#region Parenting

		public virtual void FillBinaryData()
		{
		}

		public void FreeBinaryData()
		{
			Item.Data.FreeBinaryData();
		}

		/// <summary>
		/// List of types for parenting. Empty list if only to own parent.
		/// </summary>
		public abstract IEnumerable<Type> ValidParents { get; }

		protected Entity ParentEntity {
			get {
				var parentAuthoring = ParentAuthoring;
				if (parentAuthoring != null && !(parentAuthoring is TableAuthoring)) {
					return new Entity {
						Index = parentAuthoring.IItem.Index,
						Version = parentAuthoring.IItem.Version,
					};
				}
				return Entity.Null;
			}
		}

		public IItemMainRenderableAuthoring ParentAuthoring => FindParentAuthoring();

		public bool IsCorrectlyParented {
			get {
				var parentAuthoring = ParentAuthoring;
				return parentAuthoring == null || ValidParents.Any(validParent => parentAuthoring.GetType() == validParent);
			}
		}
		private IItemMainRenderableAuthoring FindParentAuthoring()
		{
			IItemMainRenderableAuthoring ma = null;
			var go = gameObject;

			// search on parent
			if (go.transform.parent != null) {
				ma = go.transform.parent.GetComponent<IItemMainRenderableAuthoring>();
			}

			if (ma is MonoBehaviour mb && (mb.GetComponent<TableAuthoring>() != null || mb.GetComponent<PlayfieldAuthoring>() != null)) {
				return null;
			}

			if (ma != null) {
				return ma;
			}

			// search on grand parent
			if (go.transform.parent != null && go.transform.parent.transform.parent != null) {
				ma = go.transform.parent.transform.parent.GetComponent<IItemMainRenderableAuthoring>();
			}

			if (ma is MonoBehaviour mb2 && (mb2.GetComponent<TableAuthoring>() != null || mb2.GetComponent<PlayfieldAuthoring>() != null)) {
				return null;
			}

			return ma;
		}

		#endregion

		#region ILayerableItemAuthoring

		public int EditorLayer { get => Data.EditorLayer; set => Data.EditorLayer = value; }
		public string EditorLayerName { get => Data.EditorLayerName; set => Data.EditorLayerName = value; }
		public bool EditorLayerVisibility { get => Data.EditorLayerVisibility; set => Data.EditorLayerVisibility = value; }

		#endregion
	}
}
