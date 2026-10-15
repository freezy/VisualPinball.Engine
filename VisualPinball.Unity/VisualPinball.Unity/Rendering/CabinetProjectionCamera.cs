// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// https://github.com/freezy/VisualPinball.Engine
//
// SPDX-License-Identifier: GPL-3.0-or-later

using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Owns a camera's pose and projection while it renders through a cabinet projection. Enabling
	/// it saves the camera's own state, disabling it restores it. The projection is applied when it
	/// changes, and again if something else overwrote it; steady frames only compare a few values.
	/// </summary>
	[RequireComponent(typeof(Camera))]
	[DefaultExecutionOrder(int.MaxValue - 100)]
	[DisallowMultipleComponent]
	public sealed class CabinetProjectionCamera : MonoBehaviour
	{
		private Camera _camera;
		private ICabinetProjectionAdapter _adapter;
		private CabinetProjectionResult _projection;
		private bool _hasProjection;
		private bool _dirty;
		private bool _discontinuous;

		// the camera's own state, restored when the cabinet projection lets go of it
		private Vector3 _position;
		private Quaternion _rotation;
		private bool _orthographic;
		private bool _physical;
		private float _fieldOfView;
		private float _near;
		private float _far;
		private float _aspect;
		private bool _customAspect;

		/// <summary>The projection currently applied, if any.</summary>
		public bool HasProjection => _hasProjection;
		public CabinetProjectionResult Projection => _projection;

		/// <summary>
		/// Sets the projection to render with. <paramref name="discontinuous"/> resets the temporal history,
		/// for a new table or a jump in the calibration, not for small steps.
		/// </summary>
		public void SetProjection(in CabinetProjectionResult projection, bool discontinuous)
		{
			if (!projection.IsValid) {
				return;
			}
			_projection = projection;
			_hasProjection = true;
			_dirty = true;
			_discontinuous |= discontinuous;
			if (isActiveAndEnabled) {
				Apply();
			}
		}

		private void OnEnable()
		{
			_camera = GetComponent<Camera>();
			var t = _camera.transform;
			_position = t.position;
			_rotation = t.rotation;
			_orthographic = _camera.orthographic;
			_physical = _camera.usePhysicalProperties;
			_fieldOfView = _camera.fieldOfView;
			_near = _camera.nearClipPlane;
			_far = _camera.farClipPlane;
			DisplayRotation.Exclude(_camera);
			// After the exclusion, which resets the aspect a display rotation set. Unity doesn't tell whether the
			// aspect is custom: one that differs from the viewport's is.
			_aspect = _camera.aspect;
			var viewport = _camera.pixelRect;
			_customAspect = viewport.height > 0f && math.abs(_aspect - viewport.width / viewport.height) > 1e-4f;
			CabinetProjection.SetActive(_camera);
			_adapter = CabinetProjection.Adapter;
			_adapter?.Attach(_camera);
			_dirty = true;
			_discontinuous = true;
			if (_hasProjection) {
				Apply();
			}
		}

		private void OnDisable()
		{
			if (_camera == null) {
				return;
			}
			// inactive first, so the adapter restores the camera's unrestricted settings
			if (CabinetProjection.IsActive(_camera)) {
				CabinetProjection.SetActive(null);
			}
			_camera.ResetProjectionMatrix();
			_adapter?.Detach(_camera);
			_adapter = null;
			if (_customAspect) {
				_camera.aspect = _aspect;
			} else {
				_camera.ResetAspect();
			}
			_camera.orthographic = _orthographic;
			_camera.usePhysicalProperties = _physical;
			_camera.fieldOfView = _fieldOfView;
			_camera.nearClipPlane = _near;
			_camera.farClipPlane = _far;
			_camera.transform.SetPositionAndRotation(_position, _rotation);
			DisplayRotation.Include(_camera);
		}

		private void LateUpdate()
		{
			if (!_hasProjection) {
				return;
			}
			var t = _camera.transform;
			if (_dirty || _camera.projectionMatrix != _projection.Projection
				|| t.position != _projection.Position || t.rotation != _projection.Rotation) {
				Apply();
			}
		}

		private void Apply()
		{
			// pose and metadata first, the authoritative projection last
			_camera.orthographic = false;
			_camera.usePhysicalProperties = false;
			_camera.transform.SetPositionAndRotation(_projection.Position, _projection.Rotation);
			_camera.nearClipPlane = _projection.Near;
			_camera.farClipPlane = _projection.Far;
			if (_adapter != null) {
				_adapter.ApplyMetadata(_camera, _projection);
			} else {
				_camera.fieldOfView = _projection.EnclosingFieldOfView;
			}
			_camera.projectionMatrix = _projection.Projection;
			_adapter?.ProjectionChanged(_camera, _projection, _discontinuous);
			_dirty = false;
			_discontinuous = false;
		}
	}
}
