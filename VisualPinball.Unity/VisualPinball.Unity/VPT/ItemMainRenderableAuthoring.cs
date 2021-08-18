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

		public virtual void UpdateTransforms()
		{
		}

		public virtual void UpdateVisibility()
		{
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

		protected float SurfaceHeight(ISurfaceAuthoring surface, Vector2 position)
		{
			return surface?.Height(position) ?? TableHeight;
		}

		protected static void CopyMaterialName(MeshRenderer mr, string[] materialNames, string[] textureNames,
			ref string materialName)
		{
			string _ = null;
			CopyMaterialName(mr, materialNames, textureNames, ref materialName, ref _, ref _, ref _);
		}

		protected static void CopyMaterialName(MeshRenderer mr, string[] materialNames, string[] textureNames,
			ref string materialName, ref string mapName, ref string normalMapName)
		{
			string _ = null;
			CopyMaterialName(mr, materialNames, textureNames, ref materialName, ref mapName, ref normalMapName, ref _);
		}


		private static void CopyMaterialName(MeshRenderer mr, string[] materialNames, string[] textureNames,
			ref string materialName, ref string mapName, ref string normalMapName, ref string envMapName)
		{
			if (!mr || materialNames == null || textureNames == null) {
				return;
			}
			var result = PbrMaterial.ParseId(mr.sharedMaterial.name, materialNames, textureNames);
			if (materialName != null && !string.IsNullOrEmpty(result[0])) {
				materialName = result[0];
			}
			if (mapName != null) {
				var tex = mr.sharedMaterial.mainTexture;
				if (tex != null) {
					mapName = tex.name;

				} else if (!string.IsNullOrEmpty(result[1])) {
					mapName = result[1];
				}
			}
			if (normalMapName != null) {
				var tex = mr.sharedMaterial.GetTexture(RenderPipeline.Current.MaterialConverter.NormalMapProperty);
				if (tex != null) {
					normalMapName = tex.name;

				} else if (!string.IsNullOrEmpty(result[2])) {
					normalMapName = result[2];
				}
			}
			if (envMapName != null && !string.IsNullOrEmpty(result[3])) {
				envMapName = result[3];
			}
		}

		#region Tools

		public virtual ItemDataTransformType EditorPositionType => ItemDataTransformType.None;
		public virtual Vector3 GetEditorPosition() => transform.localPosition;
		public virtual void SetEditorPosition(Vector3 pos) { }

		public virtual ItemDataTransformType EditorRotationType => ItemDataTransformType.None;
		public virtual Vector3 GetEditorRotation() => Vector3.zero;
		public virtual void SetEditorRotation(Vector3 rot) { }

		public virtual ItemDataTransformType EditorScaleType => ItemDataTransformType.None;
		public virtual Vector3 GetEditorScale() => Vector3.zero;
		public virtual void SetEditorScale(Vector3 rot) { }

		#endregion
	}
}
