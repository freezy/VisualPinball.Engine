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
using VisualPinball.Unity;

public class CameraTranslateAndOrbit : MonoBehaviour
{
	[Header("Speeds")] public float panSpeed = 10f;
	public float orbitSpeed = 10f;
	public float zoomSpeed = 10f;
	public float smoothing = 10f;
	public float ballCamSmoothing = 10f;

	[Header("Optional scene references")]
	public Transform initialOrbit;
	public Transform lockTarget;

	public Player player;

	private const float RadiusMin = 0.5f;

	private Transform _transformCache;
	private GameObject _dummyForRotation;
	private Transform _dummyTransform;

	private float _radius;
	private Vector3 _focusPoint;
	private float _radiusCurrent = 15f;

	private Vector3 _positionOffsetCurrent = Vector3.zero;
	private Quaternion _rot2 = Quaternion.identity;

	/* input tracking state (unchanged from original – trimmed for brevity) */
	private bool _isTrackingMouse0;
	private Vector2 _mouse0StartPosition;
	private bool _isTrackingMouse1;
	private Vector2 _mouse1StartPosition;
	private bool _isTrackingBall;

	public Vector3 positionOffset = Vector3.zero;
	public bool isAnimating;
	private Keyboard _keyboard;
	private Mouse _mouse;
	private PhysicsEngine _physicsEngine;

	private void Start()
	{
		// same logic you had before to center on renderer bounds, etc.
		if (initialOrbit != null) {
			var pfr = initialOrbit.GetComponent<Renderer>();
			if (pfr != null) positionOffset = pfr.bounds.center;
		}

		_transformCache = transform;
		_radius = Vector3.Distance(Vector3.zero, _transformCache.position);
		_focusPoint = -_transformCache.forward * _radius;
		_positionOffsetCurrent = positionOffset;
		_radiusCurrent = _radius;

		_dummyForRotation = new GameObject("[Camera-Dummy-Rotation]");
		_dummyTransform = _dummyForRotation.transform;
		_dummyTransform.position = _transformCache.position;
		_dummyTransform.rotation = _transformCache.rotation;
		_rot2 = _transformCache.rotation;

		if (player) {
			_physicsEngine = player.GetComponentInChildren<PhysicsEngine>();
		}
		_keyboard = Keyboard.current;
		_mouse = Mouse.current;
	}

	private void Update()
	{
		// toggle ball lock if "b" pressed
		if (player && _keyboard.bKey.wasPressedThisFrame) {
			if (_isTrackingBall) {
				lockTarget = null;

			} else {
				LockBall();
			}
			_isTrackingBall = !_isTrackingBall;
		}
		if (_isTrackingBall && lockTarget == null) { // if ball got destroyed but still tracking, try to lock again.
			LockBall();
		}

		if (_mouse != null) {
			UpdateMouse();
		}

		/* ΔΔΔ NEW – apply (or blend) “look-at target” AFTER all motion is done ΔΔΔ */
		if (lockTarget != null) {

		}
	}

	private void LockBall()
	{
		if (player.BallManager.FindBall(out var ballData)) {
			lockTarget = _physicsEngine.GetTransform(ballData.Id);
		}
	}

	private void UpdateMouse()
	{
		// If we are locked-on, we ignore user orbit-drag (left-mouse) but
		// still allow pan/zoom.  Easiest is to short-circuit early:
		var allowUserOrbit = lockTarget == null;

		_transformCache.position = _dummyTransform.position = Vector3.zero;
		var hasHitRestrictedHitArea = false;

		/* ---------------- left mouse: orbit ---------------- */
		if (_mouse.leftButton.wasPressedThisFrame) {
			_mouse0StartPosition = _mouse.position.ReadValue();
			_isTrackingMouse0 = true;
		}

		if (_mouse0StartPosition.x < 200f && _mouse0StartPosition.y > Screen.height - 200f) {
			hasHitRestrictedHitArea = true;
		}

		if (!hasHitRestrictedHitArea && allowUserOrbit && _isTrackingMouse0) {

			var delta = _mouse.position.ReadValue() - _mouse0StartPosition;
			_dummyTransform.Rotate(Vector3.up, delta.x * orbitSpeed / 75f, Space.World);
			_dummyTransform.Rotate(_dummyTransform.right.normalized, -delta.y * orbitSpeed / 75f, Space.World);
			_rot2 = _dummyTransform.rotation;
			_mouse0StartPosition = _mouse.position.ReadValue();
		}

		if (_mouse.leftButton.wasReleasedThisFrame) {
			_isTrackingMouse0 = false;
		}

		/* ---------------- right mouse: pan ---------------- */
		if (_mouse.rightButton.wasPressedThisFrame) {
			_mouse1StartPosition = _mouse.position.ReadValue();
			_isTrackingMouse1 = true;
		}

		if (!hasHitRestrictedHitArea && _isTrackingMouse1) {
			var delta = _mouse.position.ReadValue() - _mouse1StartPosition;
			positionOffset += _transformCache.up * (-delta.y * (_radius * panSpeed / 60000f));
			positionOffset += _transformCache.right * (-delta.x * (_radius * panSpeed / 60000f));
			_mouse1StartPosition = _mouse.position.ReadValue();
		}

		if (_mouse.rightButton.wasReleasedThisFrame) {
			_isTrackingMouse1 = false;
		}

		/* ---------------- scroll: zoom ---------------- */
		if (!hasHitRestrictedHitArea && !isAnimating) {

			var deltaScroll = _mouse.scroll.y.ReadValue() / 300f * zoomSpeed * -_radius;
			_radius += deltaScroll;
			if (_radius < RadiusMin) {
				var diff = RadiusMin - _radius;
				positionOffset += _transformCache.forward * (diff * 4f);
				_radius = RadiusMin;
			}
		}

		/* ---------------- smooth interpolation ---------------- */
		_transformCache.rotation = Quaternion.Lerp(_transformCache.rotation, _rot2, Time.deltaTime / smoothing * 80f);

		_positionOffsetCurrent = Vector3.Lerp(_positionOffsetCurrent, positionOffset, Time.deltaTime / smoothing * 80f);
		_radiusCurrent = Mathf.Lerp(_radiusCurrent, _radius, Time.deltaTime / smoothing * 80f);

		_focusPoint = _transformCache.forward * -_radiusCurrent;
		_transformCache.position = _focusPoint + _positionOffsetCurrent;
		_dummyTransform.position = _transformCache.position;
	}
}
