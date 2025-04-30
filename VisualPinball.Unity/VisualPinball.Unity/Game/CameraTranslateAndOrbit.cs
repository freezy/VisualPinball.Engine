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

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

/// <summary>
/// A simple camera orbit script that works with Unity's new Input System.
/// </summary>
public class CameraTranslateAndOrbit : MonoBehaviour
{
	public float panSpeed = 10f;
	public float orbitSpeed = 10f;
	public float zoomSpeed = 10f;

	public float smoothing = 10f;

	public Transform initialOrbit;

	private Transform _transformCache;
	public bool isAnimating;

	private GameObject _dummyForRotation;
	private Transform _dummyTransform;

	private float _radius;

	private Vector3 _focusPoint;
	private float _radiusCurrent = 15f;

	private bool _isTrackingTouch0;
	private Vector2 _touch0StartPosition;

	private float _startMultiTouchRadius;
	private float _startMultiTouchDistance;

	private bool _isTrackingMouse0;
	private Vector2 _mouse0StartPosition;

	private bool _isTrackingMouse1;
	private Vector2 _mouse1StartPosition;

	public Vector3 positionOffset = Vector3.zero;
	private Vector3 _positionOffsetCurrent = Vector3.zero;

	private Quaternion _rot2 = Quaternion.identity;

	private const float RadiusMin = 0.5f;

	private void Awake()
	{
		EnhancedTouchSupport.Enable();
	}

	private void Start()
	{
		var pfr = initialOrbit == null ? null : initialOrbit.GetComponent<Renderer>();
		if (pfr != null) {
			positionOffset = pfr.bounds.center;
		}

		_radius = Vector3.Distance(Vector3.zero, transform.position);
		_transformCache = transform;
		_focusPoint = _transformCache.forward * -1f * _radius;
		_positionOffsetCurrent = positionOffset;
		_radiusCurrent = _radius;
		_dummyForRotation = new GameObject();
		_dummyTransform = _dummyForRotation.transform;

		_dummyTransform.rotation = _transformCache.rotation;
		_dummyTransform.position = _transformCache.position;
		_rot2 = _transformCache.rotation;
	}

	// Update is called once per frame
	private void Update()
	{
		if (Mouse.current != null) {
			UpdateMouse();
		}
	}

	private void UpdateMouse()
	{
		_transformCache.position = _dummyTransform.position = Vector3.zero;

		var hasHitRestrictedHitArea = false;

		if (Mouse.current.leftButton.wasPressedThisFrame) {
			_mouse0StartPosition = Mouse.current.position.ReadValue();
			_isTrackingMouse0 = true;
		}

		if (_mouse0StartPosition.x < 200f && _mouse0StartPosition.y > Screen.height - 200f) {
			hasHitRestrictedHitArea = true;
		}

		if (!hasHitRestrictedHitArea) {
			if (_isTrackingMouse0) {
				Vector3 mousePositionDifference = Mouse.current.position.ReadValue() - _mouse0StartPosition;
				_dummyTransform.Rotate(Vector3.up, mousePositionDifference.x * orbitSpeed / 75f, Space.World);
				_dummyTransform.Rotate(_dummyTransform.right.normalized, mousePositionDifference.y * -orbitSpeed / 75f, Space.World);
				_rot2 = _dummyTransform.rotation;
				_mouse0StartPosition = Mouse.current.position.ReadValue();
			}
			_transformCache.rotation = Quaternion.Lerp(_transformCache.rotation, _rot2, Time.deltaTime / smoothing * 80);
		}

		if (Mouse.current.leftButton.wasReleasedThisFrame) {
			_isTrackingMouse0 = false;
		}

		if (Mouse.current.rightButton.wasPressedThisFrame) {
			_mouse1StartPosition = Mouse.current.position.ReadValue();
			_isTrackingMouse1 = true;
		}

		if (!hasHitRestrictedHitArea) {
			if (_isTrackingMouse1) {
				var mousePositionDifference = Mouse.current.position.ReadValue() - _mouse1StartPosition;
				//Vector3 XZPlanerDirection = transformCache.forward.normalized;
				//XZPlanerDirection.y = 0;

				positionOffset += _transformCache.up.normalized * (mousePositionDifference.y * -(_radius * panSpeed / 60000f));
				positionOffset += _transformCache.right.normalized * (mousePositionDifference.x * -(_radius * panSpeed / 60000f));

				/*
				if(positionOffset.y < 0){
					positionOffset.y = 0;
				}*/

				_mouse1StartPosition = Mouse.current.position.ReadValue();
			}
		}

		if (Mouse.current.rightButton.wasReleasedThisFrame) {
			_isTrackingMouse1 = false;
		}

		if (!hasHitRestrictedHitArea) {
			if (!isAnimating) {
				var delta = Mouse.current.scroll.y.ReadValue() / 300f * zoomSpeed * -_radius;
				_radius += delta;
				if (_radius < RadiusMin) {
					var radDiff = RadiusMin - _radius;
					positionOffset += _transformCache.forward * (radDiff * 4f);
					_radius = RadiusMin;
				}
			}
		}

		_positionOffsetCurrent = Vector3.Lerp(_positionOffsetCurrent, positionOffset, Time.deltaTime / smoothing * 80);
		_radiusCurrent = Mathf.Lerp(_radiusCurrent, _radius, Time.deltaTime / smoothing * 80);

		_focusPoint = _transformCache.forward * -1f * _radiusCurrent;
		_transformCache.position = _focusPoint + _positionOffsetCurrent;
		_dummyTransform.position = _transformCache.position;
	}
}
