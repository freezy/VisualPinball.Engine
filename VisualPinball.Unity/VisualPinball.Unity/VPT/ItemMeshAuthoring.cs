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
using VisualPinball.Resources;

namespace VisualPinball.Unity
{
	public abstract class ItemMeshAuthoring<TItem, TData, TAuthoring> : ItemSubAuthoring<TItem, TData, TAuthoring>, IItemMeshAuthoring
		where TData : ItemData
		where TItem : Item<TData>, IRenderable
		where TAuthoring : ItemMainRenderableAuthoring<TItem, TData>
	{
		public bool MeshDirty { get => _meshDirty; set => _meshDirty = value; }
		public List<MemberInfo> MaterialRefs => _materialRefs ?? (_materialRefs = GetMembersWithAttribute<MaterialReferenceAttribute>());
		public List<MemberInfo> TextureRefs => _textureRefs ?? (_textureRefs = GetMembersWithAttribute<TextureReferenceAttribute>());

		public IItemMainRenderableAuthoring IMainAuthoring => MainAuthoring;

		protected virtual string MeshId => null;
		protected abstract bool IsVisible { get; set; }

		private List<MemberInfo> _materialRefs;
		private List<MemberInfo> _textureRefs;

		// for tracking if we need to rebuild the meshes (handled by the editor scripts) during undo/redo flows
		[HideInInspector]
		[SerializeField]
		private bool _meshDirty;

		#region Creation and destruction

		[HideInInspector]
		[SerializeField]
		private bool _meshCreated;

		private void Awake()
		{
			if (!_meshCreated && gameObject.GetComponent<MeshFilter>() == null) {
				CreateMesh();
				_meshCreated = true;
			}
		}

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
			_meshDirty = false;
		}

		private void CreateMesh()
		{
			var ta = GetComponentInParent<TableAuthoring>();
			var ro = Item.GetRenderObject(ta.Table, MeshId, Origin.Original, false);
			if (ro?.Mesh == null) {
				return;
			}
			var mesh = ro.Mesh.ToUnityMesh($"{gameObject.name}_Mesh");
			enabled = ro.IsVisible;

			// apply mesh to game object
			var mf = gameObject.AddComponent<MeshFilter>();
			mf.sharedMesh = mesh;

			// apply material
			if (ro.Mesh.AnimationFrames.Count == 1) {
				var mr = gameObject.AddComponent<MeshRenderer>();
				mr.sharedMaterial = ro.Material.ToUnityMaterial(ta);
				mr.enabled = ro.IsVisible;

				// todo find RP-specific shader
				// var lerpMat = new UnityEngine.Material();
				// mr.sharedMaterials = new[] {lerpMat};

			} else if (ro.Mesh.AnimationFrames.Count > 1) {
				var smr = gameObject.AddComponent<SkinnedMeshRenderer>();
				smr.sharedMaterial = ro.Material.ToUnityMaterial(ta, MainAuthoring.Item.GetType());
				smr.sharedMesh = mesh;
				smr.SetBlendShapeWeight(0, ro.Mesh.AnimationDefaultPosition);
				smr.enabled = ro.IsVisible;
			} else {
				var mr = gameObject.AddComponent<MeshRenderer>();
				mr.sharedMaterial = ro.Material.ToUnityMaterial(ta, MainAuthoring.Item.GetType());
				mr.enabled = ro.IsVisible;
			}
		}

		private void UpdateMesh()
		{
			var ta = GetComponentInParent<TableAuthoring>();
			var ro = Item.GetRenderObject(ta.Table, MeshId, Origin.Original, false);

			// mesh generator can return null - but in this case the main component
			// will take care of removing the mesh component.
			if (ro == null) {
				return;
			}
			var mr = GetComponent<MeshRenderer>();
			var mf = GetComponent<MeshFilter>();

			if (mf != null) {
				var unityMesh = mf.sharedMesh;
				if (ro.Mesh != null) {
					ro.Mesh.ApplyToUnityMesh(unityMesh);
				}
			}

			if (mr != null) {
				if (ta != null) {
					mr.sharedMaterial = ro.Material.ToUnityMaterial(ta, MainAuthoring.Item.GetType());
				}
				mr.enabled = true;
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
				var t = mf.transform;
				Gizmos.DrawMesh(mf.sharedMesh, t.position, t.rotation, t.lossyScale);
			}
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
