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
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
using VisualPinball.Engine.Math;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VisualPinball.Unity
{
	[AddComponentMenu("")]
	[DisallowMultipleComponent]
	public class DragPointSplineComponent : MonoBehaviour
	{
		[SerializeField] private SplineContainer _container;
		[SerializeField] private MonoBehaviour _owner;
		[SerializeField] private List<DragPointMetadata> _metadata = new();

		[NonSerialized] private DragPointData[] _dragPoints;
		[NonSerialized] private float3[] _positions = Array.Empty<float3>();
		[NonSerialized] private bool _handlingChange;

		public SplineContainer Container => _container;
		public IReadOnlyList<DragPointMetadata> Metadata => _metadata;
		public IDragPointSplineOwner Owner => _owner as IDragPointSplineOwner;

		public DragPointData[] DragPoints
		{
			get {
				if (_dragPoints == null) {
					_dragPoints = DragPointSplineConverter.ToDragPoints(_container.Spline, _metadata);
				}
				return _dragPoints;
			}
		}

		private void OnEnable()
		{
			Subscribe();
			RememberPositions();
		}


		private void OnDisable()
		{
			Spline.Changed -= OnSplineChanged;
		}

		public static DragPointSplineComponent Create(IDragPointSplineOwner owner,
			IReadOnlyList<DragPointData> dragPoints)
		{
			var splineObject = new GameObject("Spline");
			splineObject.transform.SetParent(owner.SplineTransform, false);
			splineObject.transform.localPosition = Vector3.zero;
			splineObject.transform.localRotation = ((Matrix4x4)Physics.VpxToWorld).rotation;
			splineObject.transform.localScale = Physics.ScaleInvVector;

			splineObject.AddComponent<GeneratedDragPointSplineComponent>();
			var container = splineObject.AddComponent<SplineContainer>();
			var component = splineObject.AddComponent<DragPointSplineComponent>();
			component._container = container;
			component.Bind(owner);
			component.SetDragPoints(dragPoints);

#if UNITY_EDITOR
			EditorUtility.SetDirty(owner.SplineOwner);
			EditorUtility.SetDirty(component);
#endif
			return component;
		}

		public static DragPointSplineComponent GetOrCreate(IDragPointSplineOwner owner,
			DragPointSplineComponent current, IReadOnlyList<DragPointData> dragPoints)
		{
			if (owner == null) {
				throw new ArgumentNullException(nameof(owner));
			}

			// Owners null their migration copy of the drag points once a spline exists, but Unity
			// deserializes a null array as an empty one - so after any serialization round trip
			// (entering play mode, a domain reload) an already-migrated owner hands us an empty list
			// rather than null. Applying it would overwrite the very spline we were asked to return.
			// "Nothing to migrate" and "migrate nothing" have to mean the same thing here.
			if (dragPoints is { Count: 0 }) {
				dragPoints = null;
			}

			// Only short-circuit on a spline that still holds knots. An empty one may be a blank that
			// was created while the hierarchy was incomplete, in which case the owner's real spline is
			// still sitting alongside it and the full scan below will find it.
			if (IsFunctionalChild(owner, current) && KnotCount(current) > 0) {
				current.Bind(owner);
				if (dragPoints != null) {
					current.SetDragPoints(dragPoints);
				}
				return current;
			}

			var component = ResolveExisting(owner, current);
			if (!component) {
				// Owners drop their migration copy of the drag points once a spline exists, so the
				// spline child is the only place the geometry lives. Creating a blank one here would
				// silently discard it - say so loudly instead of losing the shape without a trace.
				if (dragPoints == null || dragPoints.Count == 0) {
					Debug.LogError(
						$"No drag-point spline could be resolved for '{owner.SplineOwner.name}' and no drag points " +
						"were supplied, so an empty spline is being created. The previous shape is lost. " +
						$"Owner has {owner.SplineTransform.childCount} child object(s).",
						owner.SplineOwner);
				}
				return Create(owner, dragPoints ?? Array.Empty<DragPointData>());
			}

			component.Bind(owner);
			if (dragPoints != null) {
				component.SetDragPoints(dragPoints);
			}
			return component;
		}

		private static int KnotCount(DragPointSplineComponent component)
		{
			if (!component || !component.TryGetComponent<SplineContainer>(out var container)) {
				return 0;
			}
			return container.Spline?.Count ?? 0;
		}

		private static bool IsFunctionalChild(IDragPointSplineOwner owner,
			DragPointSplineComponent component)
		{
			return component && component.transform.parent == owner.SplineTransform
				&& component.GetComponent<SplineContainer>();
		}

		private static DragPointSplineComponent ResolveExisting(IDragPointSplineOwner owner,
			DragPointSplineComponent current)
		{
			var candidates = new List<DragPointSplineComponent>();
			var ownerTransform = owner.SplineTransform;
			var generatedChildren = new List<Transform>();
			for (var i = 0; i < ownerTransform.childCount; i++) {
				var child = ownerTransform.GetChild(i);
				var marker = child.GetComponent<GeneratedDragPointSplineComponent>();
				var component = child.GetComponent<DragPointSplineComponent>();
				if (marker || component) {
					generatedChildren.Add(child);
				}
			}

			foreach (var child in generatedChildren) {
				var marker = child.GetComponent<GeneratedDragPointSplineComponent>();
				var component = child.GetComponent<DragPointSplineComponent>();
				if (component && component.GetComponent<SplineContainer>()) {
					EnsureExportMarker(component.gameObject);
					candidates.Add(component);
				} else if (marker) {
					RemoveGeneratedChild(owner, child.gameObject,
						"it has no functional drag-point spline");
				}
			}

			DragPointSplineComponent selected = null;
			if (current && current.transform.parent == ownerTransform
				&& current.GetComponent<SplineContainer>()) {
				EnsureExportMarker(current.gameObject);
				selected = current;
				if (!candidates.Contains(current)) {
					candidates.Insert(0, current);
				}
			} else if (candidates.Count > 0) {
				selected = candidates[0];
			}

			// Whatever the owner's stale reference points at, the child carrying the most knots is the
			// one holding the real geometry. Preferring it lets a blank spline created during a scene
			// restore heal itself instead of permanently replacing the shape.
			foreach (var candidate in candidates) {
				if (KnotCount(candidate) > KnotCount(selected)) {
					selected = candidate;
				}
			}

			foreach (var candidate in candidates) {
				if (candidate != selected) {
					RemoveGeneratedChild(owner, candidate.gameObject,
						"it duplicates another drag-point spline");
				}
			}
			return selected;
		}

		private static void EnsureExportMarker(GameObject splineObject)
		{
			if (!splineObject.TryGetComponent<GeneratedDragPointSplineComponent>(out _)) {
#if UNITY_EDITOR
				if (!Application.isPlaying) {
					Undo.AddComponent<GeneratedDragPointSplineComponent>(splineObject);
					EditorUtility.SetDirty(splineObject);
					return;
				}
#endif
				splineObject.AddComponent<GeneratedDragPointSplineComponent>();
			}
		}

		private static void RemoveGeneratedChild(IDragPointSplineOwner owner,
			GameObject generatedChild, string reason)
		{
			// Never destroy a child that still carries knots. Whatever made it look unusable - a
			// component that failed to resolve, a duplicate - the knots are the only copy of the
			// owner's geometry, and they are worth more than a tidy hierarchy.
			if (generatedChild.TryGetComponent<SplineContainer>(out var container)
				&& container.Spline is { Count: > 0 }) {
				Debug.LogWarning(
					$"Keeping generated spline child '{generatedChild.name}' of '{owner.SplineOwner.name}' even though " +
					$"{reason}, because it still holds {container.Spline.Count} knots.", owner.SplineOwner);
				return;
			}

			var canDestroy = CanDestroyGeneratedChild(generatedChild);
			var action = canDestroy ? "Removing" : "Disabling";
			var suffix = canDestroy
				? string.Empty
				: " It belongs to a prefab instance and cannot be removed without unpacking it.";
			Debug.LogWarning(
				$"{action} generated spline child '{generatedChild.name}' from '{owner.SplineOwner.name}' because {reason}.{suffix}",
				owner.SplineOwner);
			generatedChild.SetActive(false);
			if (!canDestroy) {
				return;
			}
			if (Application.isPlaying) {
				UnityEngine.Object.Destroy(generatedChild);
			} else {
#if UNITY_EDITOR
				Undo.DestroyObjectImmediate(generatedChild);
#else
				UnityEngine.Object.DestroyImmediate(generatedChild);
#endif
			}
		}


		private static bool CanDestroyGeneratedChild(GameObject generatedChild)
		{
#if UNITY_EDITOR
			return Application.isPlaying || !PrefabUtility.IsPartOfPrefabInstance(generatedChild)
				|| PrefabUtility.IsAddedGameObjectOverride(generatedChild);
#else
			return true;
#endif
		}

		public void Bind(IDragPointSplineOwner owner)
		{
			_owner = owner.SplineOwner;
			if (!_container) {
				_container = GetComponent<SplineContainer>();
			}
			Subscribe();
			RememberPositions();
		}

		private void Subscribe()
		{
			Spline.Changed -= OnSplineChanged;
			Spline.Changed += OnSplineChanged;
		}

		public void SetDragPoints(IReadOnlyList<DragPointData> dragPoints)
		{
			if (dragPoints == null) {
				throw new ArgumentNullException(nameof(dragPoints));
			}

			_handlingChange = true;
			try {
				_metadata = DragPointSplineConverter.ToMetadata(dragPoints);
				_container.Spline = DragPointSplineConverter.ToSpline(dragPoints,
					Owner?.SplineClosed ?? false);
				_dragPoints = null;
				RememberPositions();
			}
			finally {
				_handlingChange = false;
			}
		}

		public void NotifyMetadataChanged()
		{
			_handlingChange = true;
			try {
				CaptureRuntimeMetadata();
				DragPointSplineConverter.RecalculateTangents(_container.Spline, _metadata);
				_dragPoints = null;
				RememberPositions();
			}
			finally {
				_handlingChange = false;
			}
			Owner?.RebuildSplineMeshes();
		}

		private void OnSplineChanged(Spline spline, int knotIndex,
			SplineModification modification)
		{
			if (_handlingChange || !_container || !ReferenceEquals(spline, _container.Spline)) {
				return;
			}

			var owner = Owner;
			if (owner == null) {
				return;
			}

			_handlingChange = true;
			try {
#if UNITY_EDITOR
				Undo.RecordObjects(new UnityEngine.Object[] { this, owner.SplineOwner },
					"Edit Drag Points");
#endif
				CaptureRuntimeMetadata();
				AlignMetadata(modification, knotIndex, spline.Closed);
				ApplyPlanarConstraint(spline, knotIndex, modification, owner.SplinePlanar);
				owner.ApplySplineConstraints(spline, knotIndex, modification, _positions);
				DragPointSplineConverter.RecalculateTangents(spline, _metadata);
				_dragPoints = null;
				RememberPositions();

#if UNITY_EDITOR
				EditorUtility.SetDirty(this);
				EditorUtility.SetDirty(owner.SplineOwner);
				PrefabUtility.RecordPrefabInstancePropertyModifications(this);
				PrefabUtility.RecordPrefabInstancePropertyModifications(owner.SplineOwner);
#endif
			}
			finally {
				_handlingChange = false;
			}

			owner.RebuildSplineMeshes();
		}

		private void AlignMetadata(SplineModification modification, int knotIndex, bool closed)
		{
			switch (modification) {
				case SplineModification.KnotInserted:
					if (_metadata.Count == 0) {
						_metadata.Add(new DragPointMetadata(new DragPointData(0f, 0f)));
						break;
					}
					var previousIndex = knotIndex - 1;
					if (previousIndex < 0) {
						previousIndex = closed ? _metadata.Count - 1 : 0;
					}
					var nextIndex = math.min(knotIndex, _metadata.Count - 1);
					_metadata.Insert(knotIndex, DragPointMetadata.CreateInserted(
						_metadata[previousIndex], _metadata[nextIndex]));
					break;

				case SplineModification.KnotRemoved:
					if (knotIndex >= 0 && knotIndex < _metadata.Count) {
						_metadata.RemoveAt(knotIndex);
					}
					break;
			}

			if (_metadata.Count != _container.Spline.Count) {
				throw new InvalidOperationException(
					$"Spline has {_container.Spline.Count} knots but metadata has {_metadata.Count} entries after {modification}.");
			}
		}

		private static void ApplyPlanarConstraint(Spline spline, int knotIndex,
			SplineModification modification, bool planar)
		{
			if (!planar || modification is not (SplineModification.KnotModified
				or SplineModification.KnotInserted) || knotIndex < 0 || knotIndex >= spline.Count) {
				return;
			}

			var knot = spline[knotIndex];
			knot.Position.z = 0f;
			spline.SetKnotNoNotify(knotIndex, knot);
		}

		private void CaptureRuntimeMetadata()
		{
			if (_dragPoints == null || _dragPoints.Length != _metadata.Count) {
				return;
			}
			for (var i = 0; i < _metadata.Count; i++) {
				_metadata[i].CalcHeight = _dragPoints[i].CalcHeight;
			}
		}

		private void RememberPositions()
		{
			if (!_container || _container.Spline == null) {
				_positions = Array.Empty<float3>();
				return;
			}

			_positions = new float3[_container.Spline.Count];
			for (var i = 0; i < _positions.Length; i++) {
				_positions[i] = _container.Spline[i].Position;
			}
		}
	}
}
