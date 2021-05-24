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

using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT.Flipper;

namespace VisualPinball.Unity
{
	public class FlipperBaseMeshComponent : ItemMeshComponent
	{
		protected override string MeshId => FlipperMeshGenerator.Base;

		public override void CreateMesh(IRenderable item, ITextureProvider texProvider, IMaterialProvider matProvider)
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
				smr.sharedMaterial = ro.Material.ToUnityMaterial(matProvider, texProvider, typeof(Flipper));
				smr.sharedMesh = mesh;
				smr.SetBlendShapeWeight(0, ro.Mesh.AnimationDefaultPosition);
				smr.enabled = ro.IsVisible;
			} else {
				var mr = gameObject.AddComponent<MeshRenderer>();
				mr.sharedMaterial = ro.Material.ToUnityMaterial(matProvider, texProvider, typeof(Flipper));
				mr.enabled = ro.IsVisible;
			}
		}
	}
}
