// Visual Pinball Engine
// Copyright (C) 2022 freezy and VPE Team
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
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Table;
using Mesh = VisualPinball.Engine.VPT.Mesh;

namespace VisualPinball.Unity
{
	public abstract class MeshComponent<TData, TComponent> : SubComponent<TData, TComponent>,
		IMeshComponent
		where TData : ItemData
		where TComponent : MainRenderableComponent<TData>
	{
		public IMainRenderableComponent MainRenderableComponent => MainComponent;

		#region Creation and destruction

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

		public virtual void RebuildMeshes()
		{
			UpdateMesh();
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

		protected abstract Mesh GetMesh(TData data);

		protected virtual bool IsProcedural => true;

		protected abstract PbrMaterial GetMaterial(TData data, Table table);

		public void CreateMesh(TData data, Table table, ITextureProvider texProvider, IMaterialProvider matProvider)
		{
			CreateMesh(gameObject, GetMesh(data), GetMaterial(data, table), data.GetName(), texProvider, matProvider);
		}

		public static void CreateMesh(GameObject gameObject, Mesh m, PbrMaterial material, string name, ITextureProvider texProvider, IMaterialProvider matProvider)
		{
			if (m == null) {
				return;
			}
			var mesh = m.ToUnityMesh($"{name} (Generated)");

			// apply mesh to game object
			var mf = gameObject.GetComponent<MeshFilter>();
			mf.sharedMesh = mesh;

			// apply renderer and material
			if (m.AnimationFrames.Count > 0) { // if number of animations frames are 1, the blend vertices are in the uvs are handle by the lerp shader.
				var smr = gameObject.AddComponent<SkinnedMeshRenderer>();
				smr.sharedMaterial = material.ToUnityMaterial(matProvider, texProvider);
				smr.sharedMesh = mesh;
				smr.SetBlendShapeWeight(0, m.AnimationDefaultPosition);
			} else {
				var mr = gameObject.AddComponent<MeshRenderer>();
				mr.sharedMaterial = material.ToUnityMaterial(matProvider, texProvider);
			}
		}

		[Obsolete("Drag-point meshes should be auto-bound. So, use Renderer.ResetBounds() and Renderer.ResetLocalBounds().")]
		protected static Bounds CalculateBounds(IEnumerable<DragPointData> dragPoints, float margin = 0, float sizeZ = 0, float posZ = 0)
		{
			var min = new float3(float.MaxValue, float.MaxValue, float.MaxValue);
			var max = new float3(float.MinValue, float.MinValue, float.MinValue);
			foreach (var t in dragPoints) {
				var p = t.Center.ToUnityVector3().TranslateToWorld();
				min = math.min(min, p);
				max = math.max(max, p);
			}
			var middle = min + (max - min) / 2;
			var size = max - min;
			if (sizeZ > 0) {
				var sizeY = Physics.ScaleToWorld(sizeZ);
				var posY = Physics.ScaleToWorld(posZ);
				middle.y = posY + sizeY / 2;
				size.y = sizeY;
			}

			return new Bounds(middle, size + margin * new float3(1f, 1f, 1f));
		}

		private void UpdateMesh()
		{
			var data = MainComponent.InstantiateData();
			MainComponent.CopyDataTo(data, null, null, false);
			var mesh = GetMesh(data);

			// mesh generator can return null - but in this case the main component
			// will take care of removing the mesh component.
			if (mesh == null || !mesh.IsSet) {
				return;
			}
			var mf = GetComponent<MeshFilter>();

			if (mf != null) {
				var unityMesh = mf.sharedMesh;
				if (!unityMesh) {
					mf.sharedMesh = new UnityEngine.Mesh { name = $"{name} (Updated)" };
					unityMesh = mf.sharedMesh;
				}
				mesh.ApplyToUnityMesh(unityMesh);
				unityMesh.RecalculateBounds();
			}
		}

#if UNITY_EDITOR
		[SerializeField] private int _instanceID;
		void Awake()
		{
			if (Application.isPlaying) {
				return;
			}

			if (_instanceID == 0 || !IsProcedural) {
				SetInstanceID();
				return;
			}

			if (_instanceID != GetInstanceID()) {
				SetInstanceID();

				var mf = GetComponent<MeshFilter>();
				if (mf == null) {
					return;
				}

				if (mf.sharedMesh != null) {
					mf.sharedMesh = null;
					RebuildMeshes();
					Debug.Log($"[{name}] Mesh regenerated.");
				}
			}
		}

		private void SetInstanceID()
		{
			_instanceID = GetInstanceID();
			var obj = new UnityEditor.SerializedObject(this);
			obj.FindProperty(nameof(_instanceID)).intValue = _instanceID;
			obj.ApplyModifiedPropertiesWithoutUndo();
		}
#endif

	}
}
