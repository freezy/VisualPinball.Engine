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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NLog;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	public abstract class ItemMeshAuthoring<TItem, TData, TAuthoring> : ItemSubAuthoring<TItem, TData, TAuthoring>, IItemMeshAuthoring
		where TData : ItemData
		where TItem : Item<TData>, IHittable, IRenderable
		where TAuthoring : ItemMainAuthoring<TItem, TData>
	{
		protected virtual string MeshId => null;

		#region Creation and destruction

		[HideInInspector]
		[SerializeField]
		private bool _meshCreated;

		private void Awake()
		{
			if (!_meshCreated && gameObject.GetComponent<MeshFilter>() == null) {
				var ta = GetComponentInParent<TableAuthoring>();
				var ro = Item.GetRenderObject(ta.Table, MeshId, asRightHanded: false);
				var mesh = ro.Mesh.ToUnityMesh($"{gameObject.name}_Mesh");

				// apply mesh to game object
				var mf = gameObject.AddComponent<MeshFilter>();
				mf.sharedMesh = mesh;

				// apply material
				if (ro.Mesh.AnimationFrames.Count > 0) {
					var smr = gameObject.AddComponent<SkinnedMeshRenderer>();
					smr.sharedMaterial = ro.Material.ToUnityMaterial(ta);
					smr.sharedMesh = mesh;
					smr.enabled = ro.IsVisible;
				} else {
					var mr = gameObject.AddComponent<MeshRenderer>();
					mr.sharedMaterial = ro.Material.ToUnityMaterial(ta);
					mr.enabled = ro.IsVisible;
				}

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

		private void OnDestroy()
		{
			var mr = gameObject.GetComponent<MeshRenderer>();
			if (mr != null) {
				DestroyImmediate(mr);
			}

			var mf = gameObject.GetComponent<MeshFilter>();
			if (mf != null) {
				DestroyImmediate(mf);
			}
		}

		#endregion















		public List<MemberInfo> MaterialRefs => _materialRefs ?? (_materialRefs = GetMembersWithAttribute<MaterialReferenceAttribute>());
		public List<MemberInfo> TextureRefs => _textureRefs ?? (_textureRefs = GetMembersWithAttribute<TextureReferenceAttribute>());

		private List<MemberInfo> _materialRefs;
		private List<MemberInfo> _textureRefs;

		private readonly Logger _logger = LogManager.GetCurrentClassLogger();

		// for tracking if we need to rebuild the meshes (handled by the editor scripts) during undo/redo flows
		[HideInInspector]
		[SerializeField]
		private bool _meshDirty;
		public bool MeshDirty { get => _meshDirty; set => _meshDirty = value; }

		public void RebuildMeshes()
		{
			if (Data == null) {
				_logger.Warn("Cannot retrieve data component for a {0}.", typeof(TItem).Name);
				return;
			}
			var table = transform.GetComponentInParent<TableAuthoring>();
			if (table == null) {
				_logger.Warn("Cannot retrieve table component from {0}, not updating meshes.", Data.GetName());
				return;
			}

			var rog = Item.GetRenderObjects(table.Table, Origin.Original, false);

			// todo can probably ditch this, because components now update themselves
			// var children = Children;
			// if (children == null) {
			// 	UpdateMesh(Item.Name, gameObject, rog, table);
			// } else {
			// 	foreach (var child in children) {
			// 		if (transform.childCount == 0) {
			// 			//Find the matching  renderObject  and Update it based on base gameObject
			// 			var ro = rog.RenderObjects.FirstOrDefault(r => r.Name == child);
			// 			if (ro != null)
			// 			{
			// 				UpdateMesh(child, gameObject, rog, table);
			// 				break;
			// 			}
			// 		} else {
			// 			Transform childTransform = transform.Find(child);
			// 			if (childTransform != null) {
			// 				UpdateMesh(child, childTransform.gameObject, rog, table);
			// 			} else {
			// 				// child hasn't been created yet (i.e. ramp might have changed type)
			// 				var ro = rog.RenderObjects.FirstOrDefault(r => r.Name == child);
			// 				if (ro != null) {
			// 					var subObj = new GameObject(ro.Name);
			// 					subObj.transform.SetParent(transform, false);
			// 					subObj.layer = VpxConverter.ChildObjectsLayer;
			// 				}
			// 			}
			// 		}
			// 	}
			// }

			// update transform based on item data, but not for "Table" since its the effective "root" and the user might want to move it on their own
			if (table != this) {
				transform.SetFromMatrix(rog.TransformationMatrix.ToUnityMatrix());
			}

			ItemDataChanged();
			_meshDirty = false;
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

		private static List<MemberInfo> GetMembersWithAttribute<TAttr>() where TAttr: Attribute
		{
			return typeof(TData)
				.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
				.Where(member => member.GetCustomAttribute<TAttr>() != null)
				.ToList();
		}
	}
}
