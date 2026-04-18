// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
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

#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT.Flipper;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Pinball/Debug/Physics Render Mask")]
	[DisallowMultipleComponent]
	public class PhysicsRenderMaskComponent : MonoBehaviour
	{
		[Tooltip("Master toggle for this debug rendering pass.")]
		public bool IsEnabled = true;

		[Tooltip("Include inactive children when collecting renderers/colliders.")]
		public bool IncludeInactiveChildren = true;

		[Tooltip("Objects in this list (and their children) are left untouched and render as usual.")]
		public List<GameObject> RenderAsUsualObjects = new();

		[Header("Per-Game-Item Materials")]
		public Material BallMaterial;
		public Material BumperMaterial;
		public Material DropTargetMaterial;
		public Material FlipperMaterial;
		public Material GateMaterial;
		public Material HitTargetMaterial;
		public Material KickerMaterial;
		public Material MetalWireGuideMaterial;
		public Material PlayfieldMaterial;
		public Material PlungerMaterial;
		public Material PrimitiveMaterial;
		public Material RampMaterial;
		public Material RubberMaterial;
		public Material SpinnerMaterial;
		public Material SurfaceMaterial;
		public Material TriggerMaterial;
		public Material UnknownGameItemMaterial;

		[Header("State Override Materials")]
		public Material KinematicColliderMaterial;
		public Material DisabledColliderMaterial;

		[NonSerialized] private readonly Dictionary<MeshRenderer, RendererState> _originalRendererStates = new();
		[NonSerialized] private readonly Dictionary<MeshFilter, Mesh> _originalFilterMeshes = new();
		[NonSerialized] private readonly List<MeshRenderer> _addedRenderers = new();
		[NonSerialized] private readonly List<MeshFilter> _addedFilters = new();
		[NonSerialized] private readonly List<Mesh> _generatedMeshes = new();
		[NonSerialized] private readonly Dictionary<int, bool> _runtimeColliderEnabledByItemId = new();

		[NonSerialized] private PhysicsEngine _physicsEngine;
		[NonSerialized] private bool _isApplied;
		[NonSerialized] private bool _previousEnabledState;

		private enum ColliderRenderState
		{
			Normal,
			Kinematic,
			Disabled
		}

		private enum GameItemType
		{
			Unknown,
			Ball,
			Bumper,
			DropTarget,
			Flipper,
			Gate,
			HitTarget,
			Kicker,
			MetalWireGuide,
			Playfield,
			Plunger,
			Primitive,
			Ramp,
			Rubber,
			Spinner,
			Surface,
			Trigger
		}

		private readonly struct RendererState
		{
			public readonly bool Enabled;
			public readonly Material[] Materials;

			public RendererState(bool enabled, Material[] materials)
			{
				Enabled = enabled;
				Materials = materials;
			}
		}

		private sealed class MeshData
		{
			public readonly List<Vector3> Vertices = new();
			public readonly List<Vector3> Normals = new();
			public readonly List<int> Indices = new();
			public bool HasGeometry => Vertices.Count > 0 && Indices.Count > 0;
		}

		private IEnumerator Start()
		{
			_previousEnabledState = IsEnabled;
			if (!IsEnabled) {
				yield break;
			}

			_physicsEngine ??= GetComponentInParent<PhysicsEngine>();
			var waitFrames = 0;
			while (_physicsEngine != null && !_physicsEngine.IsInitialized && waitFrames < 300) {
				waitFrames++;
				yield return null;
			}

			Apply();
		}

		private void Update()
		{
			if (_previousEnabledState == IsEnabled) {
				return;
			}

			_previousEnabledState = IsEnabled;
			if (IsEnabled) {
				Apply();
			} else {
				Restore();
			}
		}

		private void OnDisable()
		{
			if (_isApplied) {
				Restore();
			}
		}

		private void OnDestroy()
		{
			CleanupGeneratedArtifacts();
		}

		[ContextMenu("Apply")]
		public void Apply()
		{
			if (_isApplied) {
				Restore();
			}

			_physicsEngine ??= GetComponentInParent<PhysicsEngine>();
			if (_physicsEngine == null || !_physicsEngine.IsInitialized) {
				Debug.LogWarning($"[{nameof(PhysicsRenderMaskComponent)}] PhysicsEngine not ready on \"{name}\".");
				return;
			}

			_runtimeColliderEnabledByItemId.Clear();
			DisableOriginalRenderers();
			RenderColliderMeshesOnColliderObjects();
			_isApplied = true;
		}

		[ContextMenu("Restore")]
		public void Restore()
		{
			foreach (var rendererAndState in _originalRendererStates) {
				var renderer = rendererAndState.Key;
				if (!renderer) {
					continue;
				}

				var state = rendererAndState.Value;
				renderer.enabled = state.Enabled;
				renderer.sharedMaterials = state.Materials;
			}

			foreach (var filterAndMesh in _originalFilterMeshes) {
				if (filterAndMesh.Key) {
					filterAndMesh.Key.sharedMesh = filterAndMesh.Value;
				}
			}

			_originalRendererStates.Clear();
			_originalFilterMeshes.Clear();
			CleanupGeneratedArtifacts();
			_isApplied = false;
		}

		private void DisableOriginalRenderers()
		{
			var meshRenderers = GetComponentsInChildren<MeshRenderer>(IncludeInactiveChildren);
			foreach (var meshRenderer in meshRenderers) {
				if (!meshRenderer) {
					continue;
				}
				if (ShouldRenderAsUsual(meshRenderer.gameObject)) {
					continue;
				}

				if (!_originalRendererStates.ContainsKey(meshRenderer)) {
					_originalRendererStates[meshRenderer] = new RendererState(meshRenderer.enabled, meshRenderer.sharedMaterials);
				}
				meshRenderer.enabled = false;
			}
		}

		private void RenderColliderMeshesOnColliderObjects()
		{
			var playfield = GetComponentInParent<PlayfieldComponent>();
			var playfieldToWorld = playfield ? playfield.transform.localToWorldMatrix : Matrix4x4.identity;
			var worldToPlayfield = playfield ? playfield.transform.worldToLocalMatrix : Matrix4x4.identity;

			var collidables = GetComponentsInChildren<ICollidableComponent>(IncludeInactiveChildren);
			foreach (var collidable in collidables) {
				if (collidable is not Behaviour behaviour) {
					continue;
				}
				if (ShouldRenderAsUsual(behaviour.gameObject)) {
					continue;
				}

				var colliders = GetRuntimeColliders(collidable);
				if (colliders.Length == 0) {
					continue;
				}

				var renderState = Classify(collidable, behaviour);
				var gameItemType = ResolveGameItemType(behaviour);
				var material = ResolveMaterial(renderState, gameItemType);

				var targetTransform = behaviour.transform;
				var transformedToTarget = targetTransform.worldToLocalMatrix * playfieldToWorld * (Matrix4x4)Physics.VpxToWorld;
				var untransformedToTarget = transformedToTarget * (Matrix4x4)collidable.GetLocalToPlayfieldMatrixInVpx(worldToPlayfield);

				var mesh = BuildColliderMesh(behaviour, colliders, transformedToTarget, untransformedToTarget);
				if (mesh == null) {
					continue;
				}

				AssignMeshToColliderObject(behaviour.gameObject, mesh, material);
			}
		}

		private void AssignMeshToColliderObject(GameObject targetObject, Mesh mesh, Material material)
		{
			var meshFilter = targetObject.GetComponent<MeshFilter>();
			if (meshFilter == null) {
				meshFilter = targetObject.AddComponent<MeshFilter>();
				_addedFilters.Add(meshFilter);
			} else if (!_originalFilterMeshes.ContainsKey(meshFilter)) {
				_originalFilterMeshes[meshFilter] = meshFilter.sharedMesh;
			}
			meshFilter.sharedMesh = mesh;

			var meshRenderer = targetObject.GetComponent<MeshRenderer>();
			if (meshRenderer == null) {
				meshRenderer = targetObject.AddComponent<MeshRenderer>();
				_addedRenderers.Add(meshRenderer);
			} else if (!_originalRendererStates.ContainsKey(meshRenderer)) {
				_originalRendererStates[meshRenderer] = new RendererState(meshRenderer.enabled, meshRenderer.sharedMaterials);
			}

			meshRenderer.sharedMaterial = material;
			meshRenderer.enabled = true;
		}

		private ICollider[] GetRuntimeColliders(ICollidableComponent collidable)
		{
			try {
				return collidable.IsKinematic
					? _physicsEngine.GetKinematicColliders(collidable.ItemId)
					: _physicsEngine.GetColliders(collidable.ItemId);
			} catch {
				return Array.Empty<ICollider>();
			}
		}

		private ColliderRenderState Classify(ICollidableComponent collidable, Behaviour behaviour)
		{
			if (!behaviour.isActiveAndEnabled) {
				return ColliderRenderState.Disabled;
			}

			if (TryGetRuntimeColliderEnabled(collidable.ItemId, out var runtimeEnabled) && !runtimeEnabled) {
				return ColliderRenderState.Disabled;
			}

			if (collidable.IsKinematic) {
				return ColliderRenderState.Kinematic;
			}

			return ColliderRenderState.Normal;
		}

		private bool TryGetRuntimeColliderEnabled(int itemId, out bool isEnabled)
		{
			if (_runtimeColliderEnabledByItemId.TryGetValue(itemId, out isEnabled)) {
				return true;
			}

			try {
				isEnabled = _physicsEngine.IsColliderEnabled(itemId);
				_runtimeColliderEnabledByItemId[itemId] = isEnabled;
				return true;
			} catch {
				isEnabled = true;
				return false;
			}
		}

		private static bool TryGetIsTransformed(ICollider collider, out bool isTransformed)
		{
			const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
			var colliderType = collider.GetType();
			var headerField = colliderType.GetField("Header", flags);
			if (headerField == null) {
				isTransformed = true;
				return false;
			}

			var headerValue = headerField.GetValue(collider);
			if (headerValue == null) {
				isTransformed = true;
				return false;
			}

			var headerType = headerValue.GetType();
			var transformedField = headerType.GetField("IsTransformed", flags);
			if (transformedField != null && transformedField.FieldType == typeof(bool)) {
				isTransformed = (bool)transformedField.GetValue(headerValue);
				return true;
			}

			isTransformed = true;
			return false;
		}

		private static GameItemType ResolveGameItemType(Behaviour colliderBehaviour)
		{
			var typeName = colliderBehaviour.GetType().Name;
			return typeName switch {
				"BallColliderComponent" => GameItemType.Ball,
				"BumperColliderComponent" => GameItemType.Bumper,
				"DropTargetColliderComponent" => GameItemType.DropTarget,
				"FlipperColliderComponent" => GameItemType.Flipper,
				"GateColliderComponent" => GameItemType.Gate,
				"HitTargetColliderComponent" => GameItemType.HitTarget,
				"KickerColliderComponent" => GameItemType.Kicker,
				"MetalWireGuideColliderComponent" => GameItemType.MetalWireGuide,
				"PlayfieldColliderComponent" => GameItemType.Playfield,
				"PlungerColliderComponent" => GameItemType.Plunger,
				"PrimitiveColliderComponent" => GameItemType.Primitive,
				"RampColliderComponent" => GameItemType.Ramp,
				"RubberColliderComponent" => GameItemType.Rubber,
				"SpinnerColliderComponent" => GameItemType.Spinner,
				"SurfaceColliderComponent" => GameItemType.Surface,
				"TriggerColliderComponent" => GameItemType.Trigger,
				_ => GameItemType.Unknown
			};
		}

		private Material ResolveMaterial(ColliderRenderState renderState, GameItemType gameItemType)
		{
			var baseMaterial = GetMaterialForGameItemType(gameItemType);
			return renderState switch {
				ColliderRenderState.Disabled => DisabledColliderMaterial ? DisabledColliderMaterial : baseMaterial,
				ColliderRenderState.Kinematic => KinematicColliderMaterial ? KinematicColliderMaterial : baseMaterial,
				_ => baseMaterial
			};
		}

		private Material GetMaterialForGameItemType(GameItemType gameItemType)
		{
			return gameItemType switch {
				GameItemType.Ball => BallMaterial,
				GameItemType.Bumper => BumperMaterial,
				GameItemType.DropTarget => DropTargetMaterial,
				GameItemType.Flipper => FlipperMaterial,
				GameItemType.Gate => GateMaterial,
				GameItemType.HitTarget => HitTargetMaterial,
				GameItemType.Kicker => KickerMaterial,
				GameItemType.MetalWireGuide => MetalWireGuideMaterial,
				GameItemType.Playfield => PlayfieldMaterial,
				GameItemType.Plunger => PlungerMaterial,
				GameItemType.Primitive => PrimitiveMaterial,
				GameItemType.Ramp => RampMaterial,
				GameItemType.Rubber => RubberMaterial,
				GameItemType.Spinner => SpinnerMaterial,
				GameItemType.Surface => SurfaceMaterial,
				GameItemType.Trigger => TriggerMaterial,
				_ => UnknownGameItemMaterial
			};
		}

		private Mesh BuildColliderMesh(Behaviour sourceComponent, IReadOnlyList<ICollider> colliders, Matrix4x4 transformedMatrix, Matrix4x4 untransformedMatrix)
		{
			var transformed = new MeshData();
			var untransformed = new MeshData();

			for (var i = 0; i < colliders.Count; i++) {
				var collider = colliders[i];
				var target = TryGetIsTransformed(collider, out var isTransformed) && !isTransformed ? untransformed : transformed;

				switch (collider) {
					case CircleCollider circleCollider:
						AddCollider(circleCollider, target);
						break;
					case FlipperCollider:
						AddFlipperCollider(sourceComponent, target, isTransformed ? Origin.Global : Origin.Original);
						break;
					case GateCollider gateCollider:
						AddCollider(gateCollider.LineSeg0, target);
						AddCollider(gateCollider.LineSeg1, target);
						break;
					case LineCollider lineCollider:
						AddCollider(lineCollider, target);
						break;
					case LineSlingshotCollider lineSlingshotCollider:
						AddCollider(lineSlingshotCollider, target);
						break;
					case LineZCollider lineZCollider:
						AddCollider(lineZCollider, target);
						break;
					case PlungerCollider plungerCollider:
						AddCollider(plungerCollider.LineSegBase, target);
						AddCollider(plungerCollider.JointBase0, target);
						AddCollider(plungerCollider.JointBase1, target);
						break;
					case SpinnerCollider spinnerCollider:
						AddCollider(spinnerCollider.LineSeg0, target);
						AddCollider(spinnerCollider.LineSeg1, target);
						break;
					case TriangleCollider triangleCollider:
						AddCollider(triangleCollider, target);
						break;
				}
			}

			if (!transformed.HasGeometry && !untransformed.HasGeometry) {
				return null;
			}

			var vertices = new List<Vector3>(transformed.Vertices.Count + untransformed.Vertices.Count);
			var normals = new List<Vector3>(transformed.Normals.Count + untransformed.Normals.Count);
			var indices = new List<int>(transformed.Indices.Count + untransformed.Indices.Count);

			AppendMeshData(transformed, transformedMatrix, vertices, normals, indices);
			AppendMeshData(untransformed, untransformedMatrix, vertices, normals, indices);

			var mesh = new Mesh {
				name = $"{sourceComponent.name} (Physics Mask)"
			};
			mesh.SetVertices(vertices);
			mesh.SetNormals(normals);
			mesh.SetTriangles(indices, 0, true);
			mesh.RecalculateBounds();
			_generatedMeshes.Add(mesh);
			return mesh;
		}

		private static void AppendMeshData(MeshData source, Matrix4x4 matrix, ICollection<Vector3> vertices, ICollection<Vector3> normals, ICollection<int> indices)
		{
			if (!source.HasGeometry) {
				return;
			}

			var baseIndex = vertices.Count;
			for (var i = 0; i < source.Vertices.Count; i++) {
				vertices.Add(matrix.MultiplyPoint3x4(source.Vertices[i]));
			}
			for (var i = 0; i < source.Normals.Count; i++) {
				normals.Add(matrix.MultiplyVector(source.Normals[i]).normalized);
			}
			for (var i = 0; i < source.Indices.Count; i++) {
				indices.Add(baseIndex + source.Indices[i]);
			}
		}

		private void CleanupGeneratedArtifacts()
		{
			for (var i = 0; i < _addedRenderers.Count; i++) {
				if (_addedRenderers[i]) {
					DestroyUnityObject(_addedRenderers[i]);
				}
			}
			for (var i = 0; i < _addedFilters.Count; i++) {
				if (_addedFilters[i]) {
					DestroyUnityObject(_addedFilters[i]);
				}
			}
			for (var i = 0; i < _generatedMeshes.Count; i++) {
				if (_generatedMeshes[i]) {
					DestroyUnityObject(_generatedMeshes[i]);
				}
			}

			_addedRenderers.Clear();
			_addedFilters.Clear();
			_generatedMeshes.Clear();
		}

		private bool ShouldRenderAsUsual(GameObject targetObject)
		{
			for (var i = 0; i < RenderAsUsualObjects.Count; i++) {
				var keepObject = RenderAsUsualObjects[i];
				if (!keepObject) {
					continue;
				}
				if (targetObject.transform.IsChildOf(keepObject.transform)) {
					return true;
				}
			}
			return false;
		}

		private static void DestroyUnityObject(UnityEngine.Object obj)
		{
			if (!obj) {
				return;
			}
			if (Application.isPlaying) {
				Destroy(obj);
			} else {
				DestroyImmediate(obj);
			}
		}

		private static void AddCollider(CircleCollider circleCol, MeshData mesh)
		{
			var startIdx = mesh.Vertices.Count;
			const int sides = 32;
			const float angleStep = 360f / sides;
			var rotation = Quaternion.Euler(0f, 0f, angleStep);
			const int max = sides - 1;
			var pos = new Vector3(circleCol.Center.x, circleCol.Center.y, 0);

			mesh.Vertices.Add(rotation * new Vector3(circleCol.Radius, 0f, circleCol.ZHigh) + pos);
			mesh.Vertices.Add(rotation * new Vector3(circleCol.Radius, 0f, circleCol.ZLow) + pos);
			mesh.Vertices.Add(rotation * (mesh.Vertices[mesh.Vertices.Count - 1] - pos) + pos);
			mesh.Vertices.Add(rotation * (mesh.Vertices[mesh.Vertices.Count - 3] - pos) + pos);

			mesh.Indices.Add(startIdx + 0);
			mesh.Indices.Add(startIdx + 1);
			mesh.Indices.Add(startIdx + 2);
			mesh.Indices.Add(startIdx + 0);
			mesh.Indices.Add(startIdx + 2);
			mesh.Indices.Add(startIdx + 3);

			mesh.Normals.Add((mesh.Vertices[startIdx] - pos).normalized);
			mesh.Normals.Add(mesh.Normals[startIdx]);
			mesh.Normals.Add(mesh.Normals[startIdx]);
			mesh.Normals.Add(mesh.Normals[startIdx]);

			for (var i = 0; i < max; i++) {
				mesh.Vertices.Add(rotation * (mesh.Vertices[mesh.Vertices.Count - 2] - pos) + pos);
				mesh.Indices.Add(mesh.Vertices.Count - 1);
				mesh.Indices.Add(mesh.Vertices.Count - 2);
				mesh.Indices.Add(mesh.Vertices.Count - 3);
				mesh.Normals.Add(rotation * mesh.Normals[mesh.Normals.Count - 1]);

				mesh.Vertices.Add(rotation * (mesh.Vertices[mesh.Vertices.Count - 2] - pos) + pos);
				mesh.Indices.Add(mesh.Vertices.Count - 3);
				mesh.Indices.Add(mesh.Vertices.Count - 2);
				mesh.Indices.Add(mesh.Vertices.Count - 1);
				mesh.Normals.Add(mesh.Normals[mesh.Normals.Count - 1]);
			}
		}

		private static void AddCollider(LineZCollider lineZCol, MeshData mesh)
		{
			const float width = 10f;
			var bottom = new Vector3(lineZCol.XY.x, lineZCol.XY.y, lineZCol.ZLow);
			var top = new Vector3(lineZCol.XY.x, lineZCol.XY.y, lineZCol.ZHigh);

			var i = mesh.Vertices.Count;
			mesh.Vertices.Add(bottom + new Vector3(width, 0, 0));
			mesh.Vertices.Add(top + new Vector3(width, 0, 0));
			mesh.Vertices.Add(top + new Vector3(-width, 0, 0));
			mesh.Vertices.Add(bottom + new Vector3(-width, 0, 0));

			var normal = Vector3.up;
			mesh.Normals.Add(normal);
			mesh.Normals.Add(normal);
			mesh.Normals.Add(normal);
			mesh.Normals.Add(normal);

			mesh.Indices.Add(i + 0);
			mesh.Indices.Add(i + 2);
			mesh.Indices.Add(i + 1);
			mesh.Indices.Add(i + 3);
			mesh.Indices.Add(i + 2);
			mesh.Indices.Add(i + 0);
		}

		private static void AddCollider(LineCollider lineCol, MeshData mesh)
		{
			AddCollider(lineCol.V1, lineCol.V2, new float3(lineCol.Normal.x, lineCol.Normal.y, 0f), lineCol.ZLow, lineCol.ZHigh, mesh);
		}

		private static void AddCollider(LineSlingshotCollider lineCol, MeshData mesh)
		{
			AddCollider(lineCol.V1, lineCol.V2, new float3(lineCol.Normal.x, lineCol.Normal.y, 0f), lineCol.ZLow, lineCol.ZHigh, mesh);
		}

		private static void AddCollider(float2 v1, float2 v2, float3 normal, float zLow, float zHigh, MeshData mesh)
		{
			var h = zHigh - zLow;
			var i = mesh.Vertices.Count;
			mesh.Vertices.Add(new Vector3(v1.x, v1.y, zLow));
			mesh.Vertices.Add(new Vector3(v1.x, v1.y, zLow + h));
			mesh.Vertices.Add(new Vector3(v2.x, v2.y, zLow));
			mesh.Vertices.Add(new Vector3(v2.x, v2.y, zLow + h));

			var n = new Vector3(normal.x, normal.y, normal.z);
			mesh.Normals.Add(n);
			mesh.Normals.Add(n);
			mesh.Normals.Add(n);
			mesh.Normals.Add(n);

			mesh.Indices.Add(i + 0);
			mesh.Indices.Add(i + 2);
			mesh.Indices.Add(i + 1);
			mesh.Indices.Add(i + 2);
			mesh.Indices.Add(i + 3);
			mesh.Indices.Add(i + 1);
		}

		private static void AddCollider(TriangleCollider triangleCol, MeshData mesh)
		{
			var i = mesh.Vertices.Count;
			mesh.Vertices.Add(new Vector3(triangleCol.Rgv0.x, triangleCol.Rgv0.y, triangleCol.Rgv0.z));
			mesh.Vertices.Add(new Vector3(triangleCol.Rgv1.x, triangleCol.Rgv1.y, triangleCol.Rgv1.z));
			mesh.Vertices.Add(new Vector3(triangleCol.Rgv2.x, triangleCol.Rgv2.y, triangleCol.Rgv2.z));

			var n = triangleCol.Normal();
			var normal = new Vector3(n.x, n.y, n.z);
			mesh.Normals.Add(normal);
			mesh.Normals.Add(normal);
			mesh.Normals.Add(normal);

			mesh.Indices.Add(i + 0);
			mesh.Indices.Add(i + 2);
			mesh.Indices.Add(i + 1);
		}

		private static void AddFlipperCollider(Component sourceComponent, MeshData mesh, Origin origin)
		{
			var flipperComponent = sourceComponent.GetComponentInChildren<FlipperComponent>();
			if (flipperComponent == null) {
				return;
			}

			var startIdx = mesh.Vertices.Count;
			var flipperMesh = new FlipperMeshGenerator(flipperComponent).GetMesh(FlipperMeshGenerator.Rubber, 0, 0.01f, origin, false, 0.2f);
			for (var i = 0; i < flipperMesh.Vertices.Length; i++) {
				var vertex = flipperMesh.Vertices[i];
				mesh.Vertices.Add(vertex.ToUnityFloat3());
				mesh.Normals.Add(vertex.ToUnityNormalVector3());
			}
			for (var i = 0; i < flipperMesh.Indices.Length; i++) {
				mesh.Indices.Add(startIdx + flipperMesh.Indices[i]);
			}
		}
	}
}
#endif
