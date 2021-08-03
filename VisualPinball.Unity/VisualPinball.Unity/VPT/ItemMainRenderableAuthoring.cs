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

using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity
{
	public abstract class  ItemMainRenderableAuthoring<TItem, TData> : ItemMainAuthoring<TItem, TData>,
		IItemMainRenderableAuthoring
		where TItem : Item<TData>, IRenderable
		where TData : ItemData
	{
		public virtual bool CanBeTransformed => true;

		/// <summary>
		/// Authoring type of the child class.
		/// </summary>
		protected abstract Type MeshAuthoringType { get; }

		protected abstract Type ColliderAuthoringType { get; }


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

		public void RebuildMeshes()
		{
			Debug.Log("Rebuilding meshes...");
			foreach (var meshComponent in MeshComponents) {
				meshComponent.RebuildMeshes();
			}
		}

		public void UpdateTransforms()
		{
			var ta = GetComponentInParent<TableAuthoring>();
			if (ta != this && Item != null) {
				transform.SetFromMatrix(Item.TransformationMatrix(Table, Origin.Original).ToUnityMatrix());
			}
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

		protected T GetAuthoring<T>(Dictionary<string, IItemMainAuthoring> itemMainAuthorings, string surfaceName) where T : class, IItemMainAuthoring
		{
			return (itemMainAuthorings.ContainsKey(surfaceName.ToLower())
				? itemMainAuthorings[surfaceName.ToLower()]
				: null) as T;
		}

		#region Tools

		public virtual ItemDataTransformType EditorPositionType => ItemDataTransformType.None;
		public virtual Vector3 GetEditorPosition() => transform.localPosition;
		public virtual void SetEditorPosition(Vector3 pos) => transform.localPosition = pos;

		public virtual ItemDataTransformType EditorRotationType => ItemDataTransformType.None;
		public virtual Vector3 GetEditorRotation() => Vector3.zero;
		public virtual void SetEditorRotation(Vector3 rot) { }

		public virtual ItemDataTransformType EditorScaleType => ItemDataTransformType.None;
		public virtual Vector3 GetEditorScale() => Vector3.zero;
		public virtual void SetEditorScale(Vector3 rot) { }

		#endregion

	}
}
