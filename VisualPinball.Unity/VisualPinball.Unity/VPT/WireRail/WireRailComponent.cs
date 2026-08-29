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

using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Splines;
using VisualPinball.Engine.VPT;
using Material = UnityEngine.Material;
using Mesh = UnityEngine.Mesh;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VisualPinball.Unity
{
	public enum WireRailThirdRailSide
	{
		Left,
		Right,
	}

	/// <summary>
	/// Creates useful starting positions for wire-rail centerlines. All values are in VPX units
	/// and describe the X/Z cross-section around a route whose initial direction is +Y.
	/// </summary>
	public static class WireRailLayout
	{
		public const float ReferenceBallDiameter = 50f;
		public const float ReferenceWireDiameter = 8f;
		public const float BottomRailSpacing = 38f;
		public const float MiddleRailHeight = 44f;
		public const float TopRailHeight = 52f;

		public static Vector2[] CreateDefaultOffsets(int railCount,
			WireRailThirdRailSide thirdRailSide = WireRailThirdRailSide.Right)
		{
			if (railCount < 1) {
				throw new ArgumentOutOfRangeException(nameof(railCount), railCount,
					"A wire-rail segment needs at least one rail.");
			}

			if (railCount == 1) {
				return new[] { Vector2.zero };
			}

			var halfSpacing = BottomRailSpacing * 0.5f;
			var offsets = new List<Vector2>(railCount) {
				new(-halfSpacing, 0f),
				new(halfSpacing, 0f),
			};

			if (railCount == 3) {
				offsets.Add(new Vector2(
					thirdRailSide == WireRailThirdRailSide.Left ? -halfSpacing : halfSpacing,
					MiddleRailHeight));
				return offsets.ToArray();
			}

			if (railCount >= 4) {
				offsets.Add(new Vector2(-halfSpacing, MiddleRailHeight));
				offsets.Add(new Vector2(halfSpacing, MiddleRailHeight));
			}

			var topRailCount = railCount - 4;
			for (var i = 0; i < topRailCount; i++) {
				var x = topRailCount == 1
					? 0f
					: math.lerp(-halfSpacing, halfSpacing, i / (float)(topRailCount - 1));
				offsets.Add(new Vector2(x, TopRailHeight));
			}
			return offsets.ToArray();
		}
	}

	[Serializable]
	public sealed class WireRailSegment
	{
		[SerializeField] private WireRailThirdRailSide _thirdRailSide = WireRailThirdRailSide.Right;
		[SerializeField] private List<Vector2> _railOffsets = new(
			WireRailLayout.CreateDefaultOffsets(4));
		[SerializeField] private List<float> _wireDiameters = new();

		public WireRailThirdRailSide ThirdRailSide => _thirdRailSide;
		public int RailCount => _railOffsets?.Count ?? 0;
		public IReadOnlyList<Vector2> RailOffsets => _railOffsets;
		public IReadOnlyList<float> WireDiameters => _wireDiameters;

		public Vector2 GetRailOffset(int railIndex)
		{
			EnsureInitialized(WireRailLayout.ReferenceWireDiameter);
			return _railOffsets[railIndex];
		}

		public float GetWireDiameter(int railIndex)
		{
			EnsureInitialized(WireRailLayout.ReferenceWireDiameter);
			return _wireDiameters[railIndex];
		}

		internal void SetRailCount(int railCount, float defaultWireDiameter)
		{
			if (railCount == RailCount) {
				return;
			}
			_railOffsets = new List<Vector2>(
				WireRailLayout.CreateDefaultOffsets(railCount, _thirdRailSide));
			_wireDiameters = new List<float>(railCount);
			for (var i = 0; i < railCount; i++) {
				_wireDiameters.Add(math.max(0.1f, defaultWireDiameter));
			}
		}

		internal void SetThirdRailSide(WireRailThirdRailSide side)
		{
			if (_thirdRailSide == side) {
				return;
			}
			_thirdRailSide = side;
			if (RailCount == 3) {
				ResetLayout();
			}
		}

		internal void SetRailOffset(int railIndex, Vector2 offset)
		{
			EnsureInitialized(WireRailLayout.ReferenceWireDiameter);
			_railOffsets[railIndex] = offset;
		}

		internal void SetWireDiameter(int railIndex, float diameter)
		{
			EnsureInitialized(WireRailLayout.ReferenceWireDiameter);
			_wireDiameters[railIndex] = math.max(0.1f, diameter);
		}

		internal void ResetLayout()
		{
			EnsureInitialized(WireRailLayout.ReferenceWireDiameter);
			_railOffsets = new List<Vector2>(
				WireRailLayout.CreateDefaultOffsets(_railOffsets.Count, _thirdRailSide));
		}

		internal bool EnsureInitialized(float defaultWireDiameter)
		{
			var changed = false;
			if (_railOffsets == null || _railOffsets.Count == 0) {
				_railOffsets = new List<Vector2>(
					WireRailLayout.CreateDefaultOffsets(4, _thirdRailSide));
				changed = true;
			}
			_wireDiameters ??= new List<float>();
			while (_wireDiameters.Count < _railOffsets.Count) {
				_wireDiameters.Add(math.max(0.1f, defaultWireDiameter));
				changed = true;
			}
			if (_wireDiameters.Count > _railOffsets.Count) {
				_wireDiameters.RemoveRange(_railOffsets.Count,
					_wireDiameters.Count - _railOffsets.Count);
				changed = true;
			}
			for (var i = 0; i < _wireDiameters.Count; i++) {
				var clamped = math.max(0.1f, _wireDiameters[i]);
				if (!Mathf.Approximately(clamped, _wireDiameters[i])) {
					_wireDiameters[i] = clamped;
					changed = true;
				}
			}
			return changed;
		}

		internal WireRailSegment Clone(float defaultWireDiameter)
		{
			EnsureInitialized(defaultWireDiameter);
			return new WireRailSegment {
				_thirdRailSide = _thirdRailSide,
				_railOffsets = new List<Vector2>(_railOffsets),
				_wireDiameters = new List<float>(_wireDiameters),
			};
		}
	}

	/// <summary>
	/// First authoring slice for a native Unity spline with a rail layout per curve segment.
	/// The spline helper stores raw VPX coordinates; its transform converts them into Unity space.
	/// </summary>
	[ExecuteAlways]
	[DisallowMultipleComponent]
	[AddComponentMenu("Pinball/Game Item/Wire Rail")]
	public class WireRailComponent : MonoBehaviour, ICollidableComponent
	{
		private const string SplineObjectName = "Wire Rail Spline";

		[SerializeField] private SplineContainer _splineContainer;
		[SerializeField] private List<WireRailSegment> _segments = new();
		[SerializeField, Min(0.1f)] private float _wireDiameter = WireRailLayout.ReferenceWireDiameter;
		[SerializeField, Range(6, 16)] private int _radialSegments = 8;
		[SerializeField, Range(2, 64)] private int _renderSamplesPerSegment = 16;
		[SerializeField] private Material _renderMaterial;
		[SerializeField, Min(1f)] private float _referenceBallDiameter = WireRailLayout.ReferenceBallDiameter;
		[SerializeField, Range(2, 32)] private int _colliderSamplesPerSegment = 8;
		[SerializeField] private bool _showColliderPreview;
		[SerializeReference] private PhysicsMaterialAsset _physicsMaterial;
		[SerializeField] private bool _overwritePhysics = true;
		[SerializeField, Min(0f)] private float _elasticity = 0.3f;
		[SerializeField, Min(0f)] private float _elasticityFalloff = 0.5f;
		[SerializeField, Min(0f)] private float _friction = 0.3f;
		[SerializeField, Range(-90f, 90f)] private float _scatter;
		[SerializeField, HideInInspector] private Mesh _renderMesh;
		[SerializeField, HideInInspector] private Mesh _colliderMesh;
		[SerializeField, HideInInspector] private Vector3[] _colliderEdgeVertices = Array.Empty<Vector3>();

#if UNITY_EDITOR
		[SerializeField, HideInInspector] private int _generatedMeshOwnerId;
#endif

		[NonSerialized] private bool _rebuildingGeneratedMeshes;
		[NonSerialized] private bool _collidersDirty = true;
		[NonSerialized] private string _generationError;
#if UNITY_EDITOR
		[NonSerialized] private bool _validationRebuildScheduled;
#endif

		public SplineContainer SplineContainer => GetSplineContainerWithoutCreating();
		public IReadOnlyList<WireRailSegment> Segments => _segments;
		public string GenerationError => _generationError;
		public bool ShowColliderPreview => _showColliderPreview;
		public Mesh RenderMesh => _renderMesh;
		public Mesh ColliderMesh => _colliderMesh;

		private void Reset()
		{
			EnsureSplineContainer();
			SynchronizeSegments();
			RebuildGeneratedMeshes();
		}

		private void OnEnable()
		{
			Subscribe();

			var container = GetSplineContainerWithoutCreating();
			if (container) {
				SynchronizeSegments();
				RebuildGeneratedMeshes();
			}
			GetComponentInParent<PhysicsEngine>()?.EnableCollider(ItemId);
		}

		private void Subscribe()
		{
			Spline.Changed -= OnSplineChanged;
			Spline.Changed += OnSplineChanged;
			UnityEngine.Splines.SplineContainer.SplineAdded -= OnSplineCollectionChanged;
			UnityEngine.Splines.SplineContainer.SplineAdded += OnSplineCollectionChanged;
			UnityEngine.Splines.SplineContainer.SplineRemoved -= OnSplineCollectionChanged;
			UnityEngine.Splines.SplineContainer.SplineRemoved += OnSplineCollectionChanged;
#if UNITY_EDITOR
			Undo.undoRedoPerformed -= OnUndoRedo;
			Undo.undoRedoPerformed += OnUndoRedo;
#endif
		}

		private void OnDisable()
		{
			Spline.Changed -= OnSplineChanged;
			UnityEngine.Splines.SplineContainer.SplineAdded -= OnSplineCollectionChanged;
			UnityEngine.Splines.SplineContainer.SplineRemoved -= OnSplineCollectionChanged;
#if UNITY_EDITOR
			Undo.undoRedoPerformed -= OnUndoRedo;
			if (_validationRebuildScheduled) {
				EditorApplication.delayCall -= RebuildAfterValidation;
				_validationRebuildScheduled = false;
			}
#endif
			GetComponentInParent<PhysicsEngine>()?.DisableCollider(ItemId);
		}

		private void OnValidate()
		{
			_wireDiameter = math.max(0.1f, _wireDiameter);
			_radialSegments = math.clamp(_radialSegments, 6, 16);
			_renderSamplesPerSegment = math.clamp(_renderSamplesPerSegment, 2, 64);
			_referenceBallDiameter = math.max(1f, _referenceBallDiameter);
			_colliderSamplesPerSegment = math.clamp(_colliderSamplesPerSegment, 2, 32);
			if (!GetSplineContainerWithoutCreating()) {
				return;
			}
#if UNITY_EDITOR
			if (!_validationRebuildScheduled) {
				_validationRebuildScheduled = true;
				EditorApplication.delayCall += RebuildAfterValidation;
			}
#else
			SynchronizeSegments();
			RebuildGeneratedMeshes();
#endif
		}

#if UNITY_EDITOR
		private void RebuildAfterValidation()
		{
			_validationRebuildScheduled = false;
			if (!this || !GetSplineContainerWithoutCreating()) {
				return;
			}
			SynchronizeSegments();
			RebuildGeneratedMeshes();
		}

		private void OnUndoRedo()
		{
			if (!this || !GetSplineContainerWithoutCreating()) {
				return;
			}
			SynchronizeSegments();
			RebuildGeneratedMeshes();
			SceneView.RepaintAll();
		}
#endif

		public void SetRailCount(int segmentIndex, int railCount)
		{
			if (railCount < 1) {
				throw new ArgumentOutOfRangeException(nameof(railCount), railCount,
					"A wire-rail segment needs at least one rail.");
			}
			GetSegment(segmentIndex).SetRailCount(railCount, _wireDiameter);
			RebuildGeneratedMeshes();
			MarkDirty();
		}

		public void SetThirdRailSide(int segmentIndex, WireRailThirdRailSide side)
		{
			GetSegment(segmentIndex).SetThirdRailSide(side);
			RebuildGeneratedMeshes();
			MarkDirty();
		}

		public void SetRailOffset(int segmentIndex, int railIndex, Vector2 offset)
		{
			GetSegment(segmentIndex).SetRailOffset(railIndex, offset);
			RebuildGeneratedMeshes();
			MarkDirty();
		}

		public void SetWireDiameter(int segmentIndex, int railIndex, float diameter)
		{
			if (diameter <= 0f) {
				throw new ArgumentOutOfRangeException(nameof(diameter), diameter,
					"Wire diameter must be positive.");
			}
			GetSegment(segmentIndex).SetWireDiameter(railIndex, diameter);
			RebuildGeneratedMeshes();
			MarkDirty();
		}

		public void SetWireProperties(int segmentIndex, IReadOnlyList<int> railIndices,
			IReadOnlyList<Vector2> offsets, IReadOnlyList<float> diameters)
		{
			if (railIndices == null || offsets == null || diameters == null
				|| railIndices.Count != offsets.Count || railIndices.Count != diameters.Count) {
				throw new ArgumentException("Wire indices, offsets, and diameters must have "
					+ "matching counts.");
			}
			var segment = GetSegment(segmentIndex);
			for (var i = 0; i < railIndices.Count; i++) {
				if (railIndices[i] < 0 || railIndices[i] >= segment.RailCount) {
					throw new ArgumentOutOfRangeException(nameof(railIndices), railIndices[i],
						$"Segment {segmentIndex + 1} has {segment.RailCount} wire(s).");
				}
				if (diameters[i] <= 0f) {
					throw new ArgumentOutOfRangeException(nameof(diameters), diameters[i],
						"Wire diameter must be positive.");
				}
			}
			for (var i = 0; i < railIndices.Count; i++) {
				segment.SetRailOffset(railIndices[i], offsets[i]);
				segment.SetWireDiameter(railIndices[i], diameters[i]);
			}
			RebuildGeneratedMeshes();
			MarkDirty();
		}

		public void ResetSegmentLayout(int segmentIndex)
		{
			GetSegment(segmentIndex).ResetLayout();
			RebuildGeneratedMeshes();
			MarkDirty();
		}

		public bool SynchronizeSegments()
		{
			_segments ??= new List<WireRailSegment>();
			var changed = false;
			for (var i = 0; i < _segments.Count; i++) {
				if (_segments[i] == null) {
					_segments[i] = new WireRailSegment();
					changed = true;
				} else if (_segments[i].RailCount == 0) {
					changed = true;
				}
				changed |= _segments[i].EnsureInitialized(_wireDiameter);
			}

			var segmentCount = GetSplineSegmentCount();
			while (_segments.Count < segmentCount) {
				var segment = _segments.Count == 0
					? new WireRailSegment()
					: _segments[^1].Clone(_wireDiameter);
				segment.EnsureInitialized(_wireDiameter);
				_segments.Add(segment);
				changed = true;
			}
			if (_segments.Count > segmentCount) {
				_segments.RemoveRange(segmentCount, _segments.Count - segmentCount);
				changed = true;
			}

			if (changed) {
				MarkDirty();
			}
			return changed;
		}

		private WireRailSegment GetSegment(int segmentIndex)
		{
			SynchronizeSegments();
			if (segmentIndex < 0 || segmentIndex >= _segments.Count) {
				throw new ArgumentOutOfRangeException(nameof(segmentIndex), segmentIndex,
					$"The spline has {_segments.Count} segment(s).");
			}
			return _segments[segmentIndex];
		}

		private void OnSplineChanged(Spline spline, int knotIndex,
			SplineModification modification)
		{
			var container = GetSplineContainerWithoutCreating();
			if (!container || !ReferenceEquals(spline, container.Spline)) {
				return;
			}

			var expectedCount = GetSplineSegmentCount();
			_segments ??= new List<WireRailSegment>();
			var layoutsChanged = _segments.Count != expectedCount;
#if UNITY_EDITOR
			if (layoutsChanged && !Application.isPlaying && !Undo.isProcessing) {
				Undo.RecordObject(this, "Edit Wire Rail Spline");
			}
#endif

			if (layoutsChanged) {
				if (modification == SplineModification.KnotInserted
					&& _segments.Count == expectedCount - 1 && _segments.Count > 0
					&& knotIndex >= 0) {
					var insertIndex = math.clamp(knotIndex, 0, _segments.Count);
					var sourceIndex = spline.Closed && knotIndex == 0
						? _segments.Count - 1
						: math.clamp(knotIndex - 1, 0, _segments.Count - 1);
					_segments.Insert(insertIndex,
						_segments[sourceIndex].Clone(_wireDiameter));

				} else if (modification == SplineModification.KnotRemoved
					&& _segments.Count == expectedCount + 1 && knotIndex >= 0) {
					_segments.RemoveAt(math.clamp(knotIndex, 0, _segments.Count - 1));

				} else {
					SynchronizeSegments();
				}
			}
			RebuildGeneratedMeshes();
			MarkDirty();
		}

		private void OnSplineCollectionChanged(SplineContainer container, int _)
		{
			if (container == GetSplineContainerWithoutCreating()) {
				SynchronizeSegments();
				RebuildGeneratedMeshes();
			}
		}

		private int GetSplineSegmentCount()
		{
			var container = GetSplineContainerWithoutCreating();
			var spline = container ? container.Spline : null;
			if (spline == null || spline.Count == 0) {
				return 0;
			}
			return math.max(0, spline.Count - (spline.Closed ? 0 : 1));
		}

		public void RebuildGeneratedMeshes()
		{
			if (_rebuildingGeneratedMeshes) {
				return;
			}
			_rebuildingGeneratedMeshes = true;
			try {
				var container = GetSplineContainerWithoutCreating();
				if (!container) {
					_generationError = "The generated Wire Rail Spline child is missing.";
					return;
				}
				if (_segments == null || _segments.Count != GetSplineSegmentCount()) {
					SynchronizeSegments();
				}
#if UNITY_EDITOR
				EnsureUniqueGeneratedMeshes();
#endif
				_renderMesh = WireRailRenderMeshGenerator.Generate(container.Spline, _segments,
					_renderSamplesPerSegment, _radialSegments, _renderMesh);
				if (!WireRailColliderMeshGenerator.TryGenerate(container.Spline, _segments,
						_referenceBallDiameter, _colliderSamplesPerSegment,
						_colliderMesh, out _colliderMesh, out _colliderEdgeVertices,
						out _generationError)) {
					_colliderMesh?.Clear(false);
					_colliderEdgeVertices = Array.Empty<Vector3>();
				}

				var meshFilter = GetOrAddComponent<MeshFilter>(container.gameObject);
				var meshRenderer = GetOrAddComponent<MeshRenderer>(container.gameObject);
				var meshChanged = meshFilter.sharedMesh != _renderMesh;
				var materialBeforeAssignment = meshRenderer.sharedMaterial;
				meshFilter.sharedMesh = _renderMesh;
				AssignRenderMaterial(meshRenderer);
				var materialChanged = materialBeforeAssignment != meshRenderer.sharedMaterial;
				_collidersDirty = true;

#if UNITY_EDITOR
				if (!Application.isPlaying && (meshChanged || materialChanged)) {
					EditorUtility.SetDirty(meshFilter);
					EditorUtility.SetDirty(meshRenderer);
					PrefabUtility.RecordPrefabInstancePropertyModifications(meshFilter);
					PrefabUtility.RecordPrefabInstancePropertyModifications(meshRenderer);
				}
#endif
			} finally {
				_rebuildingGeneratedMeshes = false;
			}
		}

		public SplineContainer EnsureSplineContainerExists()
			=> EnsureSplineContainer();

		private void AssignRenderMaterial(MeshRenderer renderer)
		{
			if (_renderMaterial) {
				renderer.sharedMaterial = _renderMaterial;
				return;
			}
			var pipeline = GraphicsSettings.currentRenderPipeline;
			renderer.sharedMaterial = pipeline
				? pipeline.defaultMaterial
				: Resources.Load<Material>("Materials/Table Opaque (Builtin)");
		}

		private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
		{
			var component = gameObject.GetComponent<T>();
			return component ? component : gameObject.AddComponent<T>();
		}

#if UNITY_EDITOR
		private void EnsureUniqueGeneratedMeshes()
		{
			var currentOwnerId = UnityObjectId.Get(this);
			if (_generatedMeshOwnerId == currentOwnerId) {
				return;
			}
			_renderMesh = null;
			_colliderMesh = null;
			_colliderEdgeVertices = Array.Empty<Vector3>();
			_generatedMeshOwnerId = currentOwnerId;
		}
#endif

		private SplineContainer EnsureSplineContainer()
		{
			var existing = GetSplineContainerWithoutCreating();
			if (existing) {
				return existing;
			}

			var splineObject = new GameObject(SplineObjectName);
			splineObject.transform.SetParent(transform, false);
			splineObject.transform.localPosition = Vector3.zero;
			splineObject.transform.localRotation = ((Matrix4x4)Physics.VpxToWorld).rotation;
			splineObject.transform.localScale = Physics.ScaleInvVector;
			_splineContainer = splineObject.AddComponent<SplineContainer>();
			_splineContainer.Spline = CreateDefaultSpline();

#if UNITY_EDITOR
			if (!Application.isPlaying) {
				if (!Undo.isProcessing) {
					Undo.RegisterCreatedObjectUndo(splineObject, "Create Wire Rail Spline");
				}
				EditorUtility.SetDirty(this);
			}
#endif
			return _splineContainer;
		}

		private SplineContainer GetSplineContainerWithoutCreating()
		{
			if (_splineContainer && _splineContainer.transform.parent == transform) {
				return _splineContainer;
			}

			var child = transform.Find(SplineObjectName);
			_splineContainer = child ? child.GetComponent<SplineContainer>() : null;
			return _splineContainer;
		}

		private static Spline CreateDefaultSpline()
		{
			var spline = new Spline(2, false);
			var rotation = quaternion.RotateX(math.PI * 0.5f);
			var start = new BezierKnot(new float3(0f, 0f, 0f)) { Rotation = rotation };
			var end = new BezierKnot(new float3(0f, 500f, 0f)) { Rotation = rotation };
			spline.Add(start, TangentMode.AutoSmooth);
			spline.Add(end, TangentMode.AutoSmooth);
			return spline;
		}

		public int ItemId => UnityObjectId.Get(gameObject);
		public bool IsKinematic => false;

		public bool CollidersDirty {
			set => _collidersDirty = value;
		}

		public float PhysicsElasticity {
			get => _elasticity;
			set => _elasticity = value;
		}

		public float PhysicsElasticityFalloff {
			get => _elasticityFalloff;
			set => _elasticityFalloff = value;
		}

		public float PhysicsFriction {
			get => _friction;
			set => _friction = value;
		}

		public float PhysicsScatter {
			get => _scatter;
			set => _scatter = value;
		}

		public bool PhysicsOverwrite {
			get => _overwritePhysics;
			set => _overwritePhysics = value;
		}

		public PhysicsMaterialAsset PhysicsMaterialReference {
			get => _physicsMaterial;
			set => _physicsMaterial = value;
		}

		public float4x4 GetLocalToPlayfieldMatrixInVpx(float4x4 worldToPlayfield)
			=> Physics.GetLocalToPlayfieldMatrixInVpx(transform.localToWorldMatrix,
				worldToPlayfield);

		public void OnTransformationChanged(float4x4 currTransformationMatrix)
		{
			_collidersDirty = true;
		}

		void ICollidableComponent.GetColliders(Player player, PhysicsEngine physicsEngine,
			ref ColliderReference colliders, float4x4 translateWithinPlayfieldMatrix, float margin)
		{
			if (!_colliderMesh || _colliderMesh.vertexCount == 0) {
				return;
			}
			var material = !_overwritePhysics && _physicsMaterial
				? new PhysicsMaterialData {
					Elasticity = _physicsMaterial.Elasticity,
					ElasticityFalloff = _physicsMaterial.ElasticityFalloff,
					Friction = _physicsMaterial.Friction,
					ScatterAngleRad = _physicsMaterial.ScatterAngle,
				}
				: new PhysicsMaterialData {
					Elasticity = _elasticity,
					ElasticityFalloff = _elasticityFalloff,
					Friction = _friction,
					ScatterAngleRad = math.radians(_scatter),
				};
			var info = new ColliderInfo {
				Id = -1,
				ItemId = ItemId,
				ItemType = ItemType.MetalWireGuide,
				Material = material,
				HitThreshold = 0f,
				FireEvents = false,
			};
			using var vertices = new NativeArray<Vector3>(_colliderMesh.vertices,
				Allocator.TempJob);
			using var indices = new NativeArray<int>(_colliderMesh.triangles,
				Allocator.TempJob);
			ColliderUtils.GenerateCollidersFromMesh(in vertices, in indices,
				translateWithinPlayfieldMatrix, info, ref colliders, true);

			var points = new HashSet<Vector3>();
			for (var i = 0; i + 1 < _colliderEdgeVertices.Length; i += 2) {
				var start = _colliderEdgeVertices[i];
				var end = _colliderEdgeVertices[i + 1];
				colliders.Add(new Line3DCollider(start, end, info),
					translateWithinPlayfieldMatrix);
				points.Add(start);
				points.Add(end);
			}
			foreach (var point in points) {
				colliders.Add(new PointCollider(point, info), translateWithinPlayfieldMatrix);
			}
			_collidersDirty = false;
		}

		bool ICollidableComponent.IsCollidable
			=> isActiveAndEnabled && _colliderMesh && _colliderMesh.vertexCount > 0;

		private void OnDestroy()
		{
			DestroyGeneratedMesh(_renderMesh);
			DestroyGeneratedMesh(_colliderMesh);
			_renderMesh = null;
			_colliderMesh = null;
		}

		private static void DestroyGeneratedMesh(Mesh mesh)
		{
			if (!mesh) {
				return;
			}
			if (Application.isPlaying) {
				Destroy(mesh);
			} else {
				DestroyImmediate(mesh);
			}
		}

		private void MarkDirty()
		{
#if UNITY_EDITOR
			if (!Application.isPlaying) {
				EditorUtility.SetDirty(this);
				PrefabUtility.RecordPrefabInstancePropertyModifications(this);
			}
#endif
		}
	}
}
