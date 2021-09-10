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

using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using VisualPinball.Engine.VPT;
using Mesh = UnityEngine.Mesh;

namespace VisualPinball.Unity
{
	[DisallowMultipleComponent]
	public abstract class ItemColliderComponent<TData, TMainAuthoring> : ItemSubComponent<TData, TMainAuthoring>,
		IItemColliderAuthoring
		where TData : ItemData
		where TMainAuthoring : ItemMainComponent<TData>
	{
		[SerializeReference]
		public PhysicsMaterial PhysicsMaterial;

		[NonSerialized]
		public bool ShowGizmos;

		[SerializeField]
		public bool ShowColliderMesh;

		[SerializeField]
		public bool ShowAabbs;

		[NonSerialized]
		public int SelectedCollider = -1;

		public List<ICollider> Colliders { get; private set; }

		public IItemMainComponent MainAuthoring => MainComponent;

		private readonly Entity _colliderEntity = Player.PlayfieldEntity;

		protected abstract IApiColliderGenerator InstantiateColliderApi(Player player, Entity entity, Entity parentEntity);

		public abstract PhysicsMaterialData PhysicsMaterialData { get; }

		private void Start()
		{
			// make enable checkbox visible
		}

		protected PhysicsMaterialData GetPhysicsMaterialData(float elasticity = 1f, float elasticityFalloff = 1f,
			float friction = 0f, float scatterAngleDeg = 0f, bool overwrite = true)
		{
			return !overwrite && PhysicsMaterial != null
				? new PhysicsMaterialData {
					Elasticity = PhysicsMaterial.Elasticity,
					ElasticityFalloff = PhysicsMaterial.ElasticityFalloff,
					Friction = PhysicsMaterial.Friction,
					ScatterAngleRad = math.radians(PhysicsMaterial.ScatterAngle)
				}
				: new PhysicsMaterialData {
					Elasticity = elasticity,
					ElasticityFalloff = elasticityFalloff,
					Friction = friction,
					ScatterAngleRad = math.radians(scatterAngleDeg)
				};
		}

		private void OnDrawGizmosSelected()
		{
			Profiler.BeginSample("ItemColliderAuthoring.OnDrawGizmosSelected");

			if (!ShowGizmos || !ShowAabbs && !ShowColliderMesh) {
				Profiler.EndSample();
				return;
			}

			var player = GetComponentInParent<Player>();
			if (player == null) {
				Profiler.EndSample();
				return;
			}
			var api = InstantiateColliderApi(player, _colliderEntity, Entity.Null);
			Colliders = new List<ICollider>();
			api.CreateColliders(Colliders);

			var ltw = GetComponentInParent<PlayfieldComponent>().transform.localToWorldMatrix;

			if (ShowColliderMesh) {
				DrawColliderMesh(ltw);
			}

			if (ShowAabbs) {
				for (var i = 0; i < Colliders.Count; i++) {
					var col = Colliders[i];
					DrawAabb(ltw, col.Bounds.Aabb, i == SelectedCollider);
				}
			}

			Profiler.EndSample();
		}

		#region Collider Gizmos

		private void DrawColliderMesh(Matrix4x4 ltw)
		{
			var color = Color.green;
			Handles.color = color;
			color.a = 0.3f;
			Gizmos.color = color;
			Gizmos.matrix = ltw;
			Handles.matrix = ltw;
			var vertices = new List<Vector3>();
			var normals = new List<Vector3>();
			var indices = new List<int>();
			foreach (var col in Colliders) {
				switch (col) {

					case CircleCollider circleCol: {
						AddCollider(circleCol, vertices, normals, indices);
						break;
					}

					case FlipperCollider _: {
						AddFlipperCollider(vertices, normals, indices);
						break;
					}

					case GateCollider gateCol: {
						AddCollider(gateCol.LineSeg0, vertices, normals, indices);
						AddCollider(gateCol.LineSeg1, vertices, normals, indices);
						break;
					}

					case Line3DCollider line3DCol: {
						// todo
						break;
					}

					case LineCollider lineCol: {
						AddCollider(lineCol, vertices, normals, indices);
						break;
					}

					case LineSlingshotCollider lineSlingshotCol: {
						AddCollider(lineSlingshotCol, vertices, normals, indices);
						break;
					}

					case LineZCollider lineZCol: {
						var aabb = lineZCol.Bounds.Aabb;
						DrawLine(lineZCol.XY.ToFloat3(aabb.ZLow), lineZCol.XY.ToFloat3(aabb.ZHigh));
						//AddCollider(lineZColl, vertices, normals, indices);
						break;
					}

					case PlungerCollider plungerCol: {
						AddCollider(plungerCol.LineSegBase, vertices, normals, indices);
						AddCollider(plungerCol.JointBase0, vertices, normals, indices);
						AddCollider(plungerCol.JointBase1, vertices, normals, indices);
						break;
					}

					case PointCollider pointCol: {
						// ignoring points for now
						break;
					}

					case SpinnerCollider spinnerCol: {
						AddCollider(spinnerCol.LineSeg0, vertices, normals, indices);
						AddCollider(spinnerCol.LineSeg1, vertices, normals, indices);
						break;
					}

					case TriangleCollider triangleCol: {
						AddCollider(triangleCol, vertices, normals, indices);
						break;
					}
				}
			}

			var mesh = new Mesh { name = $"{name} (debug collider)" };
			mesh.vertices = vertices.ToArray();
			mesh.triangles = indices.ToArray();
			mesh.normals = normals.ToArray();

			Gizmos.DrawMesh(mesh);
			color = Color.gray;
			color.a = 0.1f;
			Gizmos.color = color;
			Gizmos.DrawWireMesh(mesh);
		}

		private static void AddCollider(CircleCollider circleCol, IList<Vector3> vertices, IList<Vector3> normals, ICollection<int> indices)
		{
			var startIdx = vertices.Count;
			const int m_Sides = 32;
			var aabb = circleCol.Bounds.Aabb;
			const float angleStep = 360.0f / m_Sides;
			var rotation = Quaternion.Euler(0.0f, 0.0f, angleStep);
			const int max = m_Sides - 1;
			var pos = new Vector3(circleCol.Center.x, circleCol.Center.y, 0);

			// Make first side.
			vertices.Add(rotation * new Vector3(circleCol.Radius, 0f, aabb.ZHigh) + pos);   // tr
			vertices.Add(rotation * new Vector3(circleCol.Radius, 0f, aabb.ZLow) + pos);    // bl
			vertices.Add(rotation * (vertices[vertices.Count - 1] - pos) + pos); // br
			vertices.Add(rotation * (vertices[vertices.Count - 3] - pos) + pos); // tl

			// Add triangle indices.
			indices.Add(startIdx + 0);
			indices.Add(startIdx + 1);
			indices.Add(startIdx + 2);
			indices.Add(startIdx + 0);
			indices.Add(startIdx + 2);
			indices.Add(startIdx + 3);

			// Making the first two normals:
			normals.Add(vertices[startIdx].normalized); // Points "out" of the cylinder.
			normals.Add(normals[startIdx]);
			normals.Add(normals[startIdx]);
			normals.Add(normals[startIdx]);

			// The remaining sides.
			for (var i = 0; i < max; i++) {

				// First vertex.
				vertices.Add(rotation * (vertices[vertices.Count - 2] - pos) + pos);

				indices.Add(vertices.Count - 1); // new vertex
				indices.Add(vertices.Count - 2); // shared
				indices.Add(vertices.Count - 3); // shared

				// Normal: rotate normal from the previous column.
				normals.Add(rotation * (normals[normals.Count - 1] - pos) + pos);

				// Second vertex.
				vertices.Add(rotation * (vertices[vertices.Count - 2] - pos) + pos);

				indices.Add(vertices.Count - 3); // shared
				indices.Add(vertices.Count - 2); // shared
				indices.Add(vertices.Count - 1); // new vertex

				// Normal: copy previous normal.
				normals.Add(normals[normals.Count - 1]);
			}
		}

		private static void DrawLine(Vector3 p1, Vector3 p2)
		{
			const int thickness = 10;
			Handles.DrawAAPolyLine(thickness, p1, p2);
		}

		private static void AddCollider(LineZCollider lineZCol, ICollection<Vector3> vertices, ICollection<Vector3> normals, ICollection<int> indices)
		{
			var aabb = lineZCol.Bounds.Aabb;
			const float width = 10f;

			var bottom = lineZCol.XY.ToFloat3(aabb.ZLow);
			var top = lineZCol.XY.ToFloat3(aabb.ZHigh);

			var i = vertices.Count;
			vertices.Add(bottom + new float3(width, 0, 0));
			vertices.Add(top + new float3(width, 0, 0));
			vertices.Add(top + new float3(-width, 0, 0));
			vertices.Add(bottom + new float3(-width, 0, 0));

			var normal = new Vector3(0, 1, 0);
			normals.Add(normal);
			normals.Add(normal);
			normals.Add(normal);
			normals.Add(normal);

			indices.Add(i+0);
			indices.Add(i+2);
			indices.Add(i+1);
			indices.Add(i+3);
			indices.Add(i+2);
			indices.Add(i);
		}

		private static void AddCollider(LineCollider lineCol, ICollection<Vector3> vertices, ICollection<Vector3> normals, ICollection<int> indices)
		{
			AddCollider(
				lineCol.V1, lineCol.V2, lineCol.Normal.ToFloat3(0),
				lineCol.ZLow, lineCol.ZHigh,
				vertices, normals, indices
			);
		}

		private static void AddCollider(LineSlingshotCollider lineCol, ICollection<Vector3> vertices, ICollection<Vector3> normals, ICollection<int> indices)
		{
			AddCollider(
				lineCol.V1, lineCol.V2, lineCol.Normal.ToFloat3(0),
				lineCol.ZLow, lineCol.ZHigh,
				vertices, normals, indices
			);
		}

		private static void AddCollider(float2 v1, float2 v2, float3 normal, float zLow, float zHigh, ICollection<Vector3> vertices, ICollection<Vector3> normals, ICollection<int> indices)
		{
			var h = zHigh - zLow;
			var i = vertices.Count;
			vertices.Add(v1.ToFloat3(zLow));
			vertices.Add(v1.ToFloat3(zLow + h));
			vertices.Add(v2.ToFloat3(zLow));
			vertices.Add(v2.ToFloat3(zLow + h));

			normals.Add(normal);
			normals.Add(normal);
			normals.Add(normal);
			normals.Add(normal);

			indices.Add(i);
			indices.Add(i+2);
			indices.Add(i+1);
			indices.Add(i+2);
			indices.Add(i+3);
			indices.Add(i+1);
		}

		private static void AddCollider(TriangleCollider triangleCol, ICollection<Vector3> vertices, ICollection<Vector3> normals, ICollection<int> indices)
		{
			var i = vertices.Count;

			vertices.Add(triangleCol.Rgv0);
			vertices.Add(triangleCol.Rgv1);
			vertices.Add(triangleCol.Rgv2);

			normals.Add(triangleCol.Normal());
			normals.Add(triangleCol.Normal());
			normals.Add(triangleCol.Normal());

			indices.Add(i);
			indices.Add(i+2);
			indices.Add(i+1);
		}

		private void AddFlipperCollider(List<Vector3> vertices, List<Vector3> normals, List<int> indices)
		{
			// first see if we already have a mesh
			var meshAuthoring = GetComponentInChildren<FlipperRubberMeshComponent>();
			if (meshAuthoring == null) {
				return;
			}
			var meshComponent = meshAuthoring.gameObject.GetComponent<MeshFilter>();
			if (meshComponent == null) {
				return;
			}
			var t = transform;
			var ltp = Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale);
			var i = vertices.Count;
			var mesh = meshComponent.sharedMesh;
			for (var j = 0; j < mesh.vertices.Length; j++) {
				vertices.Add(ltp.MultiplyPoint(mesh.vertices[j]));
				normals.Add(ltp.MultiplyPoint(mesh.normals[j]));
			}
			vertices.AddRange(mesh.vertices);
			normals.AddRange(mesh.normals);
			indices.AddRange(mesh.triangles.Select(n => i + n));
		}

		private static void DrawAabb(Matrix4x4 ltw, Aabb aabb, bool isSelected)
		{
			var p00 = ltw.MultiplyPoint(new Vector3( aabb.Left, aabb.Top, aabb.ZHigh));
			var p01 = ltw.MultiplyPoint(new Vector3(aabb.Left, aabb.Bottom, aabb.ZHigh));
			var p02 = ltw.MultiplyPoint(new Vector3(aabb.Right, aabb.Bottom, aabb.ZHigh));
			var p03 = ltw.MultiplyPoint(new Vector3(aabb.Right, aabb.Top, aabb.ZHigh));

			var p10 = ltw.MultiplyPoint(new Vector3( aabb.Left, aabb.Top, aabb.ZLow));
			var p11 = ltw.MultiplyPoint(new Vector3(aabb.Left, aabb.Bottom, aabb.ZLow));
			var p12 = ltw.MultiplyPoint(new Vector3(aabb.Right, aabb.Bottom, aabb.ZLow));
			var p13 = ltw.MultiplyPoint(new Vector3(aabb.Right, aabb.Top, aabb.ZLow));

			Gizmos.color = isSelected ? ColliderColor.SelectedAabb : ColliderColor.Aabb;
			Gizmos.DrawLine(p00, p01);
			Gizmos.DrawLine(p01, p02);
			Gizmos.DrawLine(p02, p03);
			Gizmos.DrawLine(p03, p00);

			Gizmos.DrawLine(p10, p11);
			Gizmos.DrawLine(p11, p12);
			Gizmos.DrawLine(p12, p13);
			Gizmos.DrawLine(p13, p10);

			Gizmos.DrawLine(p00, p10);
			Gizmos.DrawLine(p01, p11);
			Gizmos.DrawLine(p02, p12);
			Gizmos.DrawLine(p03, p13);
		}

		#endregion
	}

	internal static class ColliderColor
	{
		internal static readonly Color Aabb = new Color32(255, 0, 252, 50);
		internal static readonly Color SelectedAabb = new Color32(255, 0, 252, 255);
		internal static readonly Color Collider = new Color32(0, 255, 75, 50);
		internal static readonly Color SelectedCollider = new Color32(0, 255, 75, 255);
	}
}
