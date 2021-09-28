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

using Unity.Entities;
using UnityEngine;

namespace VisualPinball.Unity
{
	public class SlingshotComponent : MonoBehaviour, IMeshComponent, IMainRenderableComponent
	{
		public SurfaceColliderComponent SlingshotSurface;
		public RubberComponent RubberOn;
		public RubberComponent RubberOff;

		[SerializeField] private bool _isLocked;

		#region IMeshComponent

		public IMainRenderableComponent MainRenderableComponent => this;

		public void RebuildMeshes()
		{
			// check renderers
			var mf = GetComponent<MeshFilter>();
			var smr = GetComponent<SkinnedMeshRenderer>();
			if (!smr || !mf) {
				Debug.LogWarning("Mesh filter or skinned mesh renderer not found.");
				return;
			}

			// no rubbers, no mesh
			if (RubberOn == null || RubberOff == null) {
				Debug.LogWarning("Rubber references not set.");
				return;
			}
			var m0 = RubberOff.GetComponent<MeshFilter>();
			var m1 = RubberOn.GetComponent<MeshFilter>();
			if (m0 == null || m1 == null || m0.sharedMesh == null || m1.sharedMesh == null) {
				Debug.LogWarning("Rubber references not found.");
				return;
			}

			if (m0.sharedMesh.vertices.Length != m1.sharedMesh.vertices.Length) {
				Debug.LogWarning($"Rubber vertices vary ({m0.sharedMesh.vertices.Length} vs {m1.sharedMesh.vertices.Length}).");
				return;
			}

			var mesh0 = m0.sharedMesh;
			var mesh1 = m1.sharedMesh;
			var triangles = mesh0.triangles;
			var vertices0 = mesh0.vertices;
			var normals0 = mesh0.normals;
			var vertices1 = mesh1.vertices;
			var normals1 = mesh1.normals;

			var mesh = new Mesh { name = "Slingshot Mesh" };
			mesh.vertices = vertices0;
			mesh.normals = normals0;
			mesh.triangles = triangles;

			var deltaVertices = new Vector3[vertices0.Length];
			var deltaNormals = new Vector3[vertices0.Length];
			for (var i = 0; i < vertices0.Length; i++) {
				deltaVertices[i] = vertices0[i] - vertices1[i];
				deltaNormals[i] = normals0[i] - normals1[i];
			}
			mesh.AddBlendShapeFrame("slingshot", 1, deltaVertices, deltaNormals, null);

			mf.sharedMesh = mesh;
			smr.sharedMesh = mesh;
		}

		#endregion

		#region IMainRenderableComponent

		public bool IsLocked { get => _isLocked; set => _isLocked = value; }
		public bool CanBeTransformed => false;
		public string ItemName => "Slingshot";
		public Entity Entity { get; set; }

		public void UpdateTransforms() { }
		public void UpdateVisibility() { }

		public ItemDataTransformType EditorPositionType => ItemDataTransformType.None;
		public Vector3 GetEditorPosition() => Vector3.zero;
		public void SetEditorPosition(Vector3 pos) { }

		public ItemDataTransformType EditorRotationType => ItemDataTransformType.None;
		public Vector3 GetEditorRotation() => Vector3.zero;
		public void SetEditorRotation(Vector3 pos) { }

		public ItemDataTransformType EditorScaleType => ItemDataTransformType.None;
		public Vector3 GetEditorScale() => Vector3.one;
		public void SetEditorScale(Vector3 pos) { }

		#endregion
	}
}
