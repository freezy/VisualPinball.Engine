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
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	[DisallowMultipleComponent]
	public abstract class MainComponent<TData> : ItemComponent,
		IMainComponent, ILayerableItemComponent
		where TData : ItemData
	{
		public bool IsLocked { get => _isLocked; set => _isLocked = value; }

		[SerializeField] private bool _isLocked;

		public float PlayfieldHeight {
			get {
				var playfieldComponent = GetComponentInParent<PlayfieldComponent>();
				return playfieldComponent ? playfieldComponent.TableHeight : 0f;
			}
		}

		public int PlayfieldDetailLevel {
			get {
				var playfieldComponent = GetComponentInParent<PlayfieldComponent>();
				return playfieldComponent ? playfieldComponent.PlayfieldDetailLevel : 10;
			}
		}

		public abstract IEnumerable<MonoBehaviour> SetData(TData data);
		public abstract IEnumerable<MonoBehaviour> SetReferencedData(TData data, Table table, IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IMainComponent> components);
		public abstract TData CopyDataTo(TData data, string[] materialNames, string[] textureNames, bool forExport);

		public abstract ItemType ItemType { get; }

		protected T FindComponent<T>(Dictionary<string, IMainComponent> components, string surfaceName) where T : class
		{
			return (components != null && components.ContainsKey(surfaceName.ToLower())
					? components[surfaceName.ToLower()]
					: null)
				as T;
		}

		#region Data

		/// <summary>
		/// Instantiates a new data object with default values. Used for exporting back to .vpx.
		/// </summary>
		/// <returns></returns>
		public abstract TData InstantiateData();

		#endregion

		#region Parenting

		/// <summary>
		/// List of types for parenting. Empty list if only to own parent.
		/// </summary>
		public abstract IEnumerable<Type> ValidParents { get; }

		protected Entity ParentEntity {
			get {
				var parentComponent = ParentComponent;
				if (parentComponent != null && !(parentComponent is TableComponent)) {
					return parentComponent.Entity;
				}
				return Entity.Null;
			}
			set => throw new NotImplementedException();
		}

		public IMainRenderableComponent ParentComponent => FindParentComponent();

		public bool IsCorrectlyParented {
			get {
				if (!ValidParents.Any()) {
					return true;
				}
				var parentComponent = ParentComponent;
				return parentComponent == null || ValidParents.Any(validParent => parentComponent.GetType() == validParent);
			}
		}
		private IMainRenderableComponent FindParentComponent()
		{
			IMainRenderableComponent ma = null;
			var go = gameObject;

			// search on parent
			if (go.transform.parent != null) {
				ma = go.transform.parent.GetComponent<IMainRenderableComponent>();
			}

			if (ma is MonoBehaviour mb && (mb.GetComponent<TableComponent>() != null || mb.GetComponent<PlayfieldComponent>() != null)) {
				return null;
			}

			if (ma != null) {
				return ma;
			}

			// search on grand parent
			if (go.transform.parent != null && go.transform.parent.transform.parent != null) {
				ma = go.transform.parent.transform.parent.GetComponent<IMainRenderableComponent>();
			}

			if (ma is MonoBehaviour mb2 && (mb2.GetComponent<TableComponent>() != null || mb2.GetComponent<PlayfieldComponent>() != null)) {
				return null;
			}

			return ma;
		}

		#endregion

		#region ILayerableItemComponent

		public int EditorLayer { get => _editorLayer; set => _editorLayer = value; }
		public string EditorLayerName { get => _editorLayerName; set => _editorLayerName = value; }
		public bool EditorLayerVisibility { get => _editorLayerVisibility; set => _editorLayerVisibility = value; }

		[SerializeField] private int _editorLayer;
		[SerializeField] private string _editorLayerName  = string.Empty;
		[SerializeField] private bool _editorLayerVisibility = true;

		#endregion
	}
}
