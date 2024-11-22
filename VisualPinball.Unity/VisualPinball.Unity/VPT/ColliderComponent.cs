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

// ReSharper disable InconsistentNaming

using System;
using System.Collections.Generic;
using System.Linq;
using NativeTrees;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Flipper;
using Mesh = UnityEngine.Mesh;

namespace VisualPinball.Unity
{
	[DisallowMultipleComponent]
	public abstract class ColliderComponent<TData, TMainComponent> : SubComponent<TData, TMainComponent>,
		IColliderComponent, ICollidableComponent
		where TData : ItemData
		where TMainComponent : MainComponent<TData>
	{
		[SerializeReference]
		public PhysicsMaterialAsset PhysicsMaterial;

		[NonSerialized]
		public bool ShowGizmos;

		[SerializeField]
		public bool ShowColliderMesh;

		[SerializeField]
		public bool ShowColliderOctree;

		[SerializeField]
		public bool ShowAabbs;

		[NonSerialized]
		public int SelectedCollider = -1;

		public bool CollidersDirty { set => _collidersDirty = value; }

		[NonSerialized] private Mesh _staticColliderMesh;
		[NonSerialized] private Mesh _kinematicColliderMesh;
		[NonSerialized] private Mesh _nonTransformableColliderMesh;
		[NonSerialized] private readonly List<ICollider> _nonMeshColliders = new List<ICollider>();
		[NonSerialized] private bool _collidersDirty;

		protected abstract IApiColliderGenerator InstantiateColliderApi(Player player, PhysicsEngine physicsEngine);

		public virtual float4x4 TranslateWithinPlayfieldMatrix(float4x4 worldToPlayfield)
			=> MainComponent.transform.localToWorldMatrix.LocalToWorldTranslateWithinPlayfield(worldToPlayfield);

		public abstract PhysicsMaterialData PhysicsMaterialData { get; }

		private bool HasCachedColliders => false;// _colliderMesh != null && !_collidersDirty;

		private void Start()
		{
			_staticColliderMesh = null;
			_kinematicColliderMesh = null;
			_nonTransformableColliderMesh = null;
			_collidersDirty = true;
			// make enable checkbox visible
		}

		private void OnEnable()
		{
			GetComponentInParent<PhysicsEngine>()?.EnableCollider(MainComponent.gameObject.GetInstanceID());
		}

		private void OnDisable()
		{
			GetComponentInParent<PhysicsEngine>()?.DisableCollider(MainComponent.gameObject.GetInstanceID());
		}

		protected PhysicsMaterialData GetPhysicsMaterialData(float elasticity = 1f, float elasticityFalloff = 1f,
			float friction = 0f, float scatterAngleDeg = 0f, bool overwrite = true)
		{
			if (!overwrite && PhysicsMaterial != null) {
				//Debug.Log("load special Physics Material Data:" + PhysicsMaterial.name);
				PhysicsMaterialData physicsMaterialData = new PhysicsMaterialData();
				physicsMaterialData.Elasticity = PhysicsMaterial.Elasticity;
				physicsMaterialData.ElasticityFalloff = PhysicsMaterial.ElasticityFalloff;
				physicsMaterialData.Friction = PhysicsMaterial.Friction;
				physicsMaterialData.ScatterAngleRad = PhysicsMaterial.ScatterAngle;

				physicsMaterialData.ElasticityOverVelocityLUT = PhysicsMaterial.ElasticityOverVelocityLUT;
				physicsMaterialData.UseElasticityOverVelocity = PhysicsMaterial.UseElasticictyOverVelocity;

				return physicsMaterialData;
			}
			return new PhysicsMaterialData {
				Elasticity = elasticity,
				ElasticityFalloff = elasticityFalloff,
				Friction = friction,
				ScatterAngleRad = math.radians(scatterAngleDeg)
			};
		}

		#region Collider Gizmos

#if UNITY_EDITOR

		private PhysicsEngine _physicsEngine;
		private bool IsKinematic => this is IKinematicColliderComponent { IsKinematic: true };

		private void OnDrawGizmos()
		{
			Profiler.BeginSample("ItemColliderComponent.OnDrawGizmosSelected");

			var playfieldColliderComponent = GetComponentInParent<PlayfieldColliderComponent>();
			var overrideColliderMesh = playfieldColliderComponent && playfieldColliderComponent.ShowAllColliderMeshes;
			var showColliders = ShowColliderMesh || overrideColliderMesh;

			if (!(ShowGizmos || overrideColliderMesh) || !ShowAabbs && !showColliders && !ShowColliderOctree) {
				Profiler.EndSample();
				return;
			}

			var player = GetComponentInParent<Player>();
			if (player == null) {
				Profiler.EndSample();
				return;
			}
			var playfieldToWorld = GetComponentInParent<PlayfieldComponent>().transform.localToWorldMatrix;

			// todo optimize
			var translateWithinPlayfieldMatrix = TranslateWithinPlayfieldMatrix(math.inverse(playfieldToWorld));

			var nonTransformableColliderMatrices = new NativeParallelHashMap<int, float4x4>(0, Allocator.Temp);

			var generateColliders = ShowAabbs || showColliders && !HasCachedColliders;
			if (generateColliders) {

				if (Application.isPlaying && IsKinematic) {

					if (!_physicsEngine) {
						_physicsEngine = GetComponentInParent<PhysicsEngine>(); // todo cache
					}

					var colliders = _physicsEngine.GetKinematicColliders(MainComponent.gameObject.GetInstanceID());
					if (showColliders) {
						GenerateColliderMesh(colliders);
						//_collidersDirty = false;
					}

				} else {
					var api = InstantiateColliderApi(player, null);
					var colliders = new ColliderReference(ref nonTransformableColliderMatrices, Allocator.Temp);
					var kinematicColliders = new ColliderReference(ref nonTransformableColliderMatrices, Allocator.Temp, true);
					try {
						api.CreateColliders(ref colliders, ref kinematicColliders, translateWithinPlayfieldMatrix, 0.1f);

						if (showColliders) {
							if (IsKinematic) {
								GenerateColliderMesh(ref kinematicColliders);
							} else {
								GenerateColliderMesh(ref colliders);
							}
							_collidersDirty = false;
						}

						if (ShowAabbs) {
							Gizmos.matrix = playfieldToWorld * (Matrix4x4)Physics.VpxToWorld;
							for (var i = 0; i < colliders.Count; i++) {
								var col = colliders[i];
								DrawAabb(col.Bounds.Aabb, i == SelectedCollider);
							}
						}
					} finally {
						colliders.Dispose();
						kinematicColliders.Dispose();
					}
				}
			}
			if (ShowColliderOctree) {

				var api = InstantiateColliderApi(player, null);
				var colliders = new ColliderReference(ref nonTransformableColliderMatrices, Allocator.TempJob);
				var kinematicColliders = new ColliderReference(ref nonTransformableColliderMatrices, Allocator.TempJob, true);
				try {
					api.CreateColliders(ref colliders, ref kinematicColliders, translateWithinPlayfieldMatrix, 0.1f);

				var playfieldBounds = GetComponentInParent<PlayfieldComponent>().Bounds;
					var octree = new NativeOctree<int>(playfieldBounds, 32, 10, Allocator.Persistent);
					var nativeColliders = new NativeColliders(ref colliders, Allocator.TempJob);
					var populateJob = new PhysicsPopulateJob {
						Colliders = nativeColliders,
						Octree = octree,
					};
					populateJob.Run();
					Gizmos.color = Color.yellow;
					octree.DrawGizmos();
				} finally {
					colliders.Dispose();
					kinematicColliders.Dispose();
				}
			}

			if (showColliders) {

				//Gizmos.matrix = playfieldToWorld * (Matrix4x4)Physics.VpxToWorld * (Matrix4x4)translateFullyWithinPlayfieldMatrix;
				Gizmos.matrix = playfieldToWorld * (Matrix4x4)Physics.VpxToWorld;
				Handles.matrix = Gizmos.matrix;

				// var color = Application.isPlaying && IsKinematic
				// 	? Color.magenta
				// 	: IsKinematic ? new Color(0, 1, 1) : Color.green;
				// Handles.color = color;
				// color.a = 0.3f;
				// Gizmos.color = color;
				// Gizmos.DrawMesh(_colliderMesh);
				// color = Color.white;
				// color.a = 0.01f;
				// Gizmos.color = color;
				// Gizmos.DrawWireMesh(_colliderMesh);

				var white = Color.white;
				var blue = new Color(0, 1, 1);
				var green = Color.green;
				green.a = 0.3f;
				blue.a = 0.3f;
				white.a = 0.01f;

				if (_nonTransformableColliderMesh) {
					var m = ((float4x4)MainComponent.transform.localToWorldMatrix).LocalToWorldTranslateWithinPlayfield(math.inverse(playfieldToWorld));
					Gizmos.matrix = playfieldToWorld * (Matrix4x4)Physics.VpxToWorld * (Matrix4x4)m;
					Handles.matrix = Gizmos.matrix;
					Handles.color = blue;
					Gizmos.color = blue;
					Gizmos.DrawMesh(_nonTransformableColliderMesh);
				}

				if (_staticColliderMesh) {
					Gizmos.color = green;
					Gizmos.DrawMesh(_staticColliderMesh);

					Gizmos.color = white;
					Gizmos.DrawWireMesh(_staticColliderMesh);
					Gizmos.DrawWireMesh(_nonTransformableColliderMesh);
				}

				DrawNonMeshColliders();
			}

			Gizmos.matrix = Matrix4x4.identity;
			Handles.matrix = Matrix4x4.identity;

			Profiler.EndSample();
		}

		private void GenerateColliderMesh(ref ColliderReference colliders)
		{
			var color = Color.green;
			Handles.color = color;
			color.a = 0.3f;
			Gizmos.color = color;
			var vertices = new List<Vector3>();
			var verticesNonTransformable = new List<Vector3>();
			var normals = new List<Vector3>();
			var normalsNonTransformable = new List<Vector3>();
			var indices = new List<int>();
			var indicesNonTransformable = new List<int>();
			_nonMeshColliders.Clear();
			foreach (var col in colliders.CircleColliders) {
				if (col.Header.IsTransformed) {
					AddCollider(col, vertices, normals, indices);
				} else {
					AddCollider(col, verticesNonTransformable, normalsNonTransformable, indicesNonTransformable);
				}
			}
			foreach (var col in colliders.FlipperColliders) {
				if (col.Header.IsTransformed) {
					AddFlipperCollider(vertices, normals, indices, Origin.Global);
				} else {
					AddFlipperCollider(verticesNonTransformable, normalsNonTransformable, indicesNonTransformable, Origin.Original);
				}
			}
			foreach (var col in colliders.GateColliders) {
				if (col.Header.IsTransformed) {
					AddCollider(col.LineSeg0, vertices, normals, indices);
					AddCollider(col.LineSeg1, vertices, normals, indices);
				} else {
					AddCollider(col.LineSeg0, verticesNonTransformable, normalsNonTransformable, indicesNonTransformable);
					AddCollider(col.LineSeg1, verticesNonTransformable, normalsNonTransformable, indicesNonTransformable);
				}
			}
			foreach (var col in colliders.LineColliders) {
				if (col.Header.IsTransformed) {
					AddCollider(col, vertices, normals, indices);
				} else {
					AddCollider(col, verticesNonTransformable, normalsNonTransformable, indicesNonTransformable);
				}
			}
			foreach (var col in colliders.LineSlingshotColliders) {
				if (col.Header.IsTransformed) {
					AddCollider(col, vertices, normals, indices);
				} else {
					AddCollider(col, verticesNonTransformable, normalsNonTransformable, indicesNonTransformable);
				}
			}
			foreach (var col in colliders.LineZColliders) {
				_nonMeshColliders.Add(col);
			}
			foreach (var col in colliders.PlungerColliders) {
				if (col.Header.IsTransformed) {
					AddCollider(col.LineSegBase, vertices, normals, indices);
					AddCollider(col.JointBase0, vertices, normals, indices);
					AddCollider(col.JointBase1, vertices, normals, indices);
				} else {
					AddCollider(col.LineSegBase, verticesNonTransformable, normalsNonTransformable, indicesNonTransformable);
					AddCollider(col.JointBase0, verticesNonTransformable, normalsNonTransformable, indicesNonTransformable);
					AddCollider(col.JointBase1, verticesNonTransformable, normalsNonTransformable, indicesNonTransformable);
				}
			}
			foreach (var col in colliders.SpinnerColliders) {
				if (col.Header.IsTransformed) {
					AddCollider(col.LineSeg0, vertices, normals, indices);
					AddCollider(col.LineSeg1, vertices, normals, indices);
				} else {
					AddCollider(col.LineSeg0, verticesNonTransformable, normalsNonTransformable, indicesNonTransformable);
					AddCollider(col.LineSeg1, verticesNonTransformable, normalsNonTransformable, indicesNonTransformable);
				}
			}
			foreach (var col in colliders.TriangleColliders) {
				if (col.Header.IsTransformed) {
					AddCollider(col, vertices, normals, indices);
				} else {
					AddCollider(col, verticesNonTransformable, normalsNonTransformable, indicesNonTransformable);
				}
			}

			// todo Line3DCollider

			if (vertices.Count > 0) {
				_staticColliderMesh = new Mesh {
					name = $"{name} (static collider)",
					vertices = vertices.ToArray(),
					triangles = indices.ToArray(),
					normals = normals.ToArray()
				};
			} else {
				_staticColliderMesh = null;
			}

			if (verticesNonTransformable.Count > 0) {
				_nonTransformableColliderMesh = new Mesh {
					name = $"{name} (non-transformable colliders)",
					vertices = verticesNonTransformable.ToArray(),
					triangles = indicesNonTransformable.ToArray(),
					normals = normalsNonTransformable.ToArray()
				};
			} else {
				_nonTransformableColliderMesh = null;
			}
		}

		private void GenerateColliderMesh(IEnumerable<ICollider> colliders)
		{
			var color = Color.magenta;
			Handles.color = color;
			color.a = 0.3f;
			Gizmos.color = color;
			var vertices = new List<Vector3>();
			var verticesNonTransformable = new List<Vector3>();
			var normals = new List<Vector3>();
			var normalsNonTransformable = new List<Vector3>();
			var indices = new List<int>();
			var indicesNonTransformable = new List<int>();
			_nonMeshColliders.Clear();

			foreach (var coll in colliders) {

				switch (coll) {

					// circle collider
					case CircleCollider { Header: { IsTransformed: true } } circleCollider:
						AddCollider(circleCollider, vertices, normals, indices);
						break;
					case CircleCollider circleCollider:
						AddCollider(circleCollider, verticesNonTransformable, normalsNonTransformable, indicesNonTransformable);
						break;

					// flipper collider
					case FlipperCollider { Header: { IsTransformed: true } }:
						AddFlipperCollider(vertices, normals, indices, Origin.Global);
						break;
					case FlipperCollider:
						AddFlipperCollider(verticesNonTransformable, normalsNonTransformable, indicesNonTransformable, Origin.Global);
						break;

					// gate collider
					case GateCollider { Header: { IsTransformed: true } } gateCollider:
						AddCollider(gateCollider.LineSeg0, vertices, normals, indices);
						AddCollider(gateCollider.LineSeg1, vertices, normals, indices);
						break;
					case GateCollider gateCollider:
						AddCollider(gateCollider.LineSeg0, verticesNonTransformable, normalsNonTransformable, indicesNonTransformable);
						AddCollider(gateCollider.LineSeg1, verticesNonTransformable, normalsNonTransformable, indicesNonTransformable);
						break;

					// line collider
					case LineCollider { Header: { IsTransformed: true } } lineCollider:
						AddCollider(lineCollider, vertices, normals, indices);
						break;
					case LineCollider lineCollider:
						AddCollider(lineCollider, verticesNonTransformable, normalsNonTransformable, indicesNonTransformable);
						break;

					// line slingshot collider
					case LineSlingshotCollider { Header: { IsTransformed: true } } lineSlingshotCollider:
						AddCollider(lineSlingshotCollider, vertices, normals, indices);
						break;
					case LineSlingshotCollider lineSlingshotCollider:
						AddCollider(lineSlingshotCollider, verticesNonTransformable, normalsNonTransformable, indicesNonTransformable);
						break;

					// line z collider
					case LineZCollider lineZCollider:
						_nonMeshColliders.Add(lineZCollider);
						break;

					// plunger collider
					case PlungerCollider { Header: { IsTransformed: true } } plungerCollider:
						AddCollider(plungerCollider.LineSegBase, vertices, normals, indices);
						AddCollider(plungerCollider.JointBase0, vertices, normals, indices);
						AddCollider(plungerCollider.JointBase1, vertices, normals, indices);
						break;
					case PlungerCollider plungerCollider:
						AddCollider(plungerCollider.LineSegBase, verticesNonTransformable, normalsNonTransformable, indicesNonTransformable);
						AddCollider(plungerCollider.JointBase0, verticesNonTransformable, normalsNonTransformable, indicesNonTransformable);
						AddCollider(plungerCollider.JointBase1, verticesNonTransformable, normalsNonTransformable, indicesNonTransformable);
						break;

					// spinner collider
					case SpinnerCollider { Header: { IsTransformed: true } } spinnerCollider:
						AddCollider(spinnerCollider.LineSeg0, vertices, normals, indices);
						AddCollider(spinnerCollider.LineSeg1, vertices, normals, indices);
						break;
					case SpinnerCollider spinnerCollider:
						AddCollider(spinnerCollider.LineSeg0, verticesNonTransformable, normalsNonTransformable, indicesNonTransformable);
						AddCollider(spinnerCollider.LineSeg1, verticesNonTransformable, normalsNonTransformable, indicesNonTransformable);
						break;

					// triangle collider
					case TriangleCollider { Header: { IsTransformed: true } } triangleCollider:
						AddCollider(triangleCollider, vertices, normals, indices);
						break;
					case TriangleCollider triangleCollider:
						AddCollider(triangleCollider, verticesNonTransformable, normalsNonTransformable, indicesNonTransformable);
						break;
				}
			}

			// todo Line3DCollider

			_staticColliderMesh = new Mesh {
				name = $"{name} (static collider)",
				vertices = vertices.ToArray(),
				triangles = indices.ToArray(),
				normals = normals.ToArray()
			};

			_nonTransformableColliderMesh = new Mesh {
				name = $"{name} (static collider)",
				vertices = verticesNonTransformable.ToArray(),
				triangles = indicesNonTransformable.ToArray(),
				normals = normalsNonTransformable.ToArray()
			};
		}

		private void DrawNonMeshColliders()
		{
			foreach (var col in _nonMeshColliders) {
				switch (col) {
					case LineZCollider lineZCol: {
						DrawLine(lineZCol.XY.ToFloat3(lineZCol.ZLow), lineZCol.XY.ToFloat3(lineZCol.ZHigh));
							break;
						}
				}
			}
		}

		private static void AddCollider(CircleCollider circleCol, IList<Vector3> vertices, IList<Vector3> normals, ICollection<int> indices)
		{
			var startIdx = vertices.Count;
			const int m_Sides = 32;
			const float angleStep = 360.0f / m_Sides;
			var rotation = Quaternion.Euler(0.0f, 0.0f, angleStep);
			const int max = m_Sides - 1;
			var pos = new Vector3(circleCol.Center.x, circleCol.Center.y, 0);

			// Make first side.
			vertices.Add(rotation * new Vector3(circleCol.Radius, 0f, circleCol.ZHigh) + pos);   // tr
			vertices.Add(rotation * new Vector3(circleCol.Radius, 0f, circleCol.ZLow) + pos);    // bl
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
			normals.Add((vertices[startIdx] - pos).normalized); // Points "out" of the cylinder.
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
				normals.Add(rotation * normals[normals.Count - 1]);

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
			const float width = 10f;

			var bottom = lineZCol.XY.ToFloat3(lineZCol.ZLow);
			var top = lineZCol.XY.ToFloat3(lineZCol.ZHigh);

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

			indices.Add(i + 0);
			indices.Add(i + 2);
			indices.Add(i + 1);
			indices.Add(i + 3);
			indices.Add(i + 2);
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
			indices.Add(i + 2);
			indices.Add(i + 1);
			indices.Add(i + 2);
			indices.Add(i + 3);
			indices.Add(i + 1);
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
			indices.Add(i + 2);
			indices.Add(i + 1);
		}

		private void AddFlipperCollider(List<Vector3> vertices, List<Vector3> normals, List<int> indices, Origin origin)
		{
			// first see if we already have a mesh
			var flipperComponent = GetComponentInChildren<FlipperComponent>();
			if (flipperComponent == null) {
				return;
			}

			var startIdx = vertices.Count;
			var mesh = new FlipperMeshGenerator(flipperComponent).GetMesh(FlipperMeshGenerator.Rubber, 0, 0.01f, origin, false, 0.2f);
			for (var i = 0; i < mesh.Vertices.Length; i++) {
				var vertex = mesh.Vertices[i];
				vertices.Add(vertex.ToUnityFloat3());
				normals.Add(vertex.ToUnityNormalVector3());
			}
			indices.AddRange(mesh.Indices.Select(n => startIdx + n));
		}

		private static void DrawAabb(Aabb aabb, bool isSelected)
		{
			Gizmos.color = isSelected ? ColliderColor.SelectedAabb : ColliderColor.Aabb;
			Gizmos.DrawWireCube(aabb.Center, aabb.Size);
		}

#endif

		#endregion

		void ICollidableComponent.GetColliders(Player player, PhysicsEngine physicsEngine, ref ColliderReference colliders,
			ref ColliderReference kinematicColliders, float4x4 translateWithinPlayfieldMatrix, float margin)
			=> InstantiateColliderApi(player, physicsEngine)
				.CreateColliders(ref colliders, ref kinematicColliders, translateWithinPlayfieldMatrix, margin);

		int ICollidableComponent.ItemId => MainComponent.gameObject.GetInstanceID();
		bool ICollidableComponent.IsCollidable => isActiveAndEnabled;
	}

	internal static class ColliderColor
	{
		internal static readonly Color Aabb = new Color32(255, 0, 252, 50);
		internal static readonly Color SelectedAabb = new Color32(255, 0, 252, 255);
		internal static readonly Color Collider = new Color32(0, 255, 75, 50);
		internal static readonly Color SelectedCollider = new Color32(0, 255, 75, 255);
	}
}
