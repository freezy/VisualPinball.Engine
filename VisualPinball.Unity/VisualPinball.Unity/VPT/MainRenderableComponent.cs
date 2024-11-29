// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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
using UnityEngine;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using Mesh = VisualPinball.Engine.VPT.Mesh;

namespace VisualPinball.Unity
{
	public abstract class MainRenderableComponent<TData> : MainComponent<TData>, IMainRenderableComponent
		where TData : ItemData
	{
		public virtual bool CanBeTransformed => true;

		public virtual bool OverrideTransform => true;

		//public abstract void SetTransform(Vector3 position, Vector3 scale, Quaternion rotation);
		public abstract void CopyFromObject(GameObject go);
		//public virtual void SetTransform(Vector3 position, Vector3 scale, Quaternion rotation) { }

		/// <summary>
		/// Component type of the child class.
		/// </summary>
		protected abstract Type MeshComponentType { get; }

		protected abstract Type ColliderComponentType { get; }

		[NonSerialized]
		public Player Player;

		[NonSerialized]
		private PlayfieldComponent _playfield;
		protected PlayfieldComponent Playfield => _playfield ? _playfield : _playfield = GetComponentInParent<PlayfieldComponent>();

		/// <summary>
		/// Returns all child mesh components linked to this data.
		/// </summary>
		private IEnumerable<IMeshComponent> MeshComponents => MeshComponentType != null ?
			GetComponentsInChildren(MeshComponentType, true)
				.Select(c => (IMeshComponent) c)
				/*.Where(ma => ma.ItemData == _data)*/ : Array.Empty<IMeshComponent>();

		private IEnumerable<IColliderComponent> ColliderComponents => ColliderComponentType != null ?
			GetComponentsInChildren(ColliderComponentType, true)
				.Select(c => (IColliderComponent) c)
				/*.Where(ca => ca.ItemData == _data)*/ : Array.Empty<IColliderComponent>();

		public void RebuildMeshes()
		{
			Debug.Log("Rebuilding meshes...");
			foreach (var meshComponent in MeshComponents) {
				meshComponent.RebuildMeshes();
			}
			foreach (var colliderComponent in ColliderComponents) {
				colliderComponent.CollidersDirty = true;
			}
		}

		protected Mesh GetDefaultMesh()
		{
			var mf = GetComponent<MeshFilter>();
			if (mf && mf.sharedMesh) {
				return mf.sharedMesh.ToVpMesh();
			}

			return null;
		}

		public UnityEngine.Mesh GetUnityMesh()
		{
			var mf = GetComponent<MeshFilter>();
			if (mf && mf.sharedMesh) {
				return mf.sharedMesh;
			}
			return null;
		}

		public virtual void OnPlayfieldHeightUpdated() => UpdateTransforms();

		public virtual void UpdateTransforms()
		{
			foreach (var colliderComponent in ColliderComponents) {
				colliderComponent.CollidersDirty = true;
			}
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

		protected float ClampDegrees(float deg)
		{
			deg %= 360;
			return deg > 180 ? deg - 360 : deg;
		}

		private static void CopyMaterialName(MeshRenderer mr, string[] materialNames, string[] textureNames,
			ref string materialName, ref string mapName, ref string normalMapName, ref string envMapName)
		{
			if (!mr || materialNames == null || textureNames == null || mr.sharedMaterial == null) {
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

		protected static void MoveDragPointsTo(DragPointData[] dragPoints, Vector3 destination)
		{
			var sum = Vertex3D.Zero;
			foreach (var t in dragPoints) {
				sum += t.Center;
			}
			var srcPos = sum / dragPoints.Length;
			
			var delta = (destination.ToVertex3D() - srcPos);
			foreach (var dragPointData in dragPoints) {
				dragPointData.Center += delta;
			}
		}

		#endregion
	}
}
