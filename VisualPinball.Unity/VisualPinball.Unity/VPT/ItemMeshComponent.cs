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
using UnityEngine;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity
{
	public abstract class ItemMeshComponent : MonoBehaviour
	{
		protected abstract Type ItemType { get; }

		protected virtual string MeshId => null;

		public bool MeshDirty;

		public void CreateMesh(IRenderable item, ITextureProvider texProvider, IMaterialProvider matProvider)
		{
			var ta = GetComponentInParent<TableAuthoring>();
			var ro = item.GetRenderObject(ta.Table, MeshId, Origin.Original, false);
			if (ro?.Mesh == null) {
				return;
			}
			var mesh = ro.Mesh.ToUnityMesh($"{gameObject.name}_Mesh");
			enabled = ro.IsVisible;

			// apply mesh to game object
			var mf = gameObject.AddComponent<MeshFilter>();
			mf.sharedMesh = mesh;

			// apply material
			if (ro.Mesh.AnimationFrames.Count > 1) { // if number of animations frames are 1, the blend vertices are in the uvs are handle by the lerp shader.
				var smr = gameObject.AddComponent<SkinnedMeshRenderer>();
				smr.sharedMaterial = ro.Material.ToUnityMaterial(matProvider, texProvider, ItemType);
				smr.sharedMesh = mesh;
				smr.SetBlendShapeWeight(0, ro.Mesh.AnimationDefaultPosition);
				smr.enabled = ro.IsVisible;

			} else {
				var mr = gameObject.AddComponent<MeshRenderer>();
				mr.sharedMaterial = ro.Material.ToUnityMaterial(matProvider, texProvider, ItemType);
				mr.enabled = ro.IsVisible;
			}
		}

		public void RebuildMeshes()
		{
			// UpdateMesh();
			// ItemDataChanged();
			MeshDirty = false;
		}

		private void UpdateMesh()
		{
			// var ta = GetComponentInParent<TableAuthoring>();
			// var ro = Item.GetRenderObject(ta.Table, MeshId, Origin.Original, false);
			//
			// // mesh generator can return null - but in this case the main component
			// // will take care of removing the mesh component.
			// if (ro == null) {
			// 	return;
			// }
			// var mr = GetComponent<MeshRenderer>();
			// var mf = GetComponent<MeshFilter>();
			//
			// if (mf != null) {
			// 	var unityMesh = mf.sharedMesh;
			// 	if (ro.Mesh != null) {
			// 		ro.Mesh.ApplyToUnityMesh(unityMesh);
			// 	}
			// }

			// if (mr != null) {
			// 	if (ta != null) {
			// 		mr.sharedMaterial = ro.Material.ToUnityMaterial(ta, MainAuthoring.Item.GetType());
			// 	}
			// 	mr.enabled = true;
			// }
		}
	}
}
