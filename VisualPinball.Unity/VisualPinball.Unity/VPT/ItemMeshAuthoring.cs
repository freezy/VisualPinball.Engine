﻿// Visual Pinball Engine
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
using System.Reflection;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	public abstract class ItemMeshAuthoring<TItem, TData, TAuthoring> : ItemSubAuthoring<TItem, TData, TAuthoring>,
		IItemMeshAuthoring
		where TData : ItemData
		where TItem : Item<TData>, IRenderable
		where TAuthoring : ItemMainRenderableAuthoring<TItem, TData>
	{
		public List<MemberInfo> MaterialRefs => _materialRefs ??= GetMembersWithAttribute<MaterialReferenceAttribute>();

		public IItemMainRenderableAuthoring IMainAuthoring => MainAuthoring;

		protected virtual string MeshId => null;

		private List<MemberInfo> _materialRefs;
		private List<MemberInfo> _textureRefs;

		#region Creation and destruction

		[HideInInspector]
		[SerializeField]
		private bool _meshCreated = true;

		private void OnEnable()
		{
			var mr = gameObject.GetComponent<MeshRenderer>();
			if (mr != null) {
				mr.enabled = true;
			}
		}

		private void OnDisable()
		{
			var mr = gameObject.GetComponent<MeshRenderer>();
			if (mr != null) {
				mr.enabled = false;
			}
		}

		#endregion

		public void OnVisibilityChanged(bool before, bool after)
		{
			if (before == after) {
				return;
			}
			enabled = after;
		}

		public void RebuildMeshes()
		{
			UpdateMesh();
			ItemDataChanged();
		}

		public void ClearMeshVertices()
		{
			var mf = GetComponent<MeshFilter>();
			if (mf && mf.sharedMesh) {
				var mesh = mf.sharedMesh;
				mesh.triangles = null;
				mesh.vertices = Array.Empty<Vector3>();
				mesh.normals = Array.Empty<Vector3>();
				mesh.uv = Array.Empty<Vector2>();
			}
		}

		protected abstract RenderObject GetRenderObject(TData data, Table table);

		public void CreateMesh(TData data, ITextureProvider texProvider, IMaterialProvider matProvider)
		{
			var ta = GetComponentInParent<TableAuthoring>();
			var ro = GetRenderObject(data, ta.Table);
			if (ro?.Mesh == null) {
				return;
			}
			var mesh = ro.Mesh.ToUnityMesh($"{data.GetName()} Mesh ({gameObject.name})");

			// apply mesh to game object
			var mf = gameObject.GetComponent<MeshFilter>();
			mf.sharedMesh = mesh;

			// apply renderer and material
			if (ro.Mesh.AnimationFrames.Count > 0) { // if number of animations frames are 1, the blend vertices are in the uvs are handle by the lerp shader.
				var smr = gameObject.AddComponent<SkinnedMeshRenderer>();
				smr.sharedMaterial = ro.Material.ToUnityMaterial(matProvider, texProvider, MainAuthoring.Item.GetType());
				smr.sharedMesh = mesh;
				smr.SetBlendShapeWeight(0, ro.Mesh.AnimationDefaultPosition);
			} else {
				var mr = gameObject.AddComponent<MeshRenderer>();
				mr.sharedMaterial = ro.Material.ToUnityMaterial(matProvider, texProvider, MainAuthoring.Item.GetType());
			}
		}

		private void UpdateMesh()
		{
			var ta = GetComponentInParent<TableAuthoring>();
			MainAuthoring.CopyDataTo(Item.Data, null, null);
			var ro = Item.GetRenderObject(ta.Table, MeshId, Origin.Original, false);

			// mesh generator can return null - but in this case the main component
			// will take care of removing the mesh component.
			if (ro == null) {
				return;
			}
			var mf = GetComponent<MeshFilter>();

			if (mf != null) {
				var unityMesh = mf.sharedMesh;
				if (unityMesh) {
					ro.Mesh?.ApplyToUnityMesh(unityMesh);
				}
			}

			// if (mr != null) {
			// 	if (ta != null) {
			// 		mr.sharedMaterial = ro.Material.ToUnityMaterial(ta, MainAuthoring.Item.GetType());
			// 	}
			// 	mr.enabled = true;
			// }
		}

		private static List<MemberInfo> GetMembersWithAttribute<TAttr>() where TAttr: Attribute
		{
			return typeof(TData)
				.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
				.Where(member => member.GetCustomAttribute<TAttr>() != null)
				.ToList();
		}
	}
}
