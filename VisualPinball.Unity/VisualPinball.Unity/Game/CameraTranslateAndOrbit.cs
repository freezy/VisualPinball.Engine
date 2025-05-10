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
	private bool _isTrackingBallTarget;				// ★ NEW: shift-B look-at lock

	// *** BALL-LOCK – offset we keep between camera and ball while tracking
	private Vector3 _ballFollowOffset = Vector3.zero;
	private Vector3 _ballFollowVel = Vector3.zero;
	private float   _orbitYaw;                 // ★ track user-added yaw
	private float   _orbitPitch;               // ★   …and pitch
	private Vector3 _smoothedBallPos;
	private Vector3 _ballPosVel;
	private Vector3 _ballFollowOffsetTarget;    // ★ where the user wants the offset
	private Vector3 _ballOffsetVel;             // ★ velocity for SmoothDamp

	public Vector3 positionOffset = Vector3.zero;
	public bool isAnimating;
	private Keyboard _keyboard;
	private Mouse _mouse;
	private PhysicsEngine _physicsEngine;

	/* -------------------------------------------------------- */

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

			if (_keyboard.shiftKey.isPressed) {					// ⇧B → target-lock
				if (_isTrackingBallTarget) {
					lockTarget = null;
					_isTrackingBallTarget = false;
				} else {
					LockBallTarget();
					_isTrackingBallTarget = lockTarget != null;
					_isTrackingBall = false;
				}

			} else {											// B → follow-lock

				if (_isTrackingBall) {
					lockTarget = null;

				} else {
					LockBall();
				}
				_isTrackingBall = !_isTrackingBall;
				if (_isTrackingBall) _isTrackingBallTarget = false;
			}
		}
		// if (_isTrackingBall && lockTarget == null) {			// if ball got destroyed but still tracking, try to lock again.
		// 	LockBall();
		// }
		if (_isTrackingBallTarget && lockTarget == null) {
			LockBallTarget();
		}

		if (_mouse != null) {
			UpdateMouse();
		}
	}

	private void LateUpdate()
	{
		if (lockTarget == null) return;

		/* --- 1. smooth the ball itself --- */
		_smoothedBallPos = Vector3.SmoothDamp(
			_smoothedBallPos,
			lockTarget.position,
			ref _ballPosVel,
			ballCamSmoothing * Time.deltaTime);

		/* --- follow-lock moves camera; target-lock doesn’t --- */
		if (_isTrackingBall) {

			/* --- 2. smooth the user-driven orbit offset --- */
			_ballFollowOffset = Vector3.SmoothDamp(
				_ballFollowOffset,
				_ballFollowOffsetTarget,
				ref _ballOffsetVel,
				ballCamSmoothing * Time.deltaTime);

			/* --- 3. final camera placement --- */
			_transformCache.position = _smoothedBallPos + _ballFollowOffset;
		}

		/* -------- look-at rotation (shared) -------- */
		_transformCache.rotation = Quaternion.LookRotation(
			_smoothedBallPos - _transformCache.position,
			Vector3.up);
		/* ------------------------------------------- */

		/* keep helpers coherent so you can unlock cleanly */
		_dummyTransform.position = _transformCache.position;
		_dummyTransform.rotation = _transformCache.rotation;
		_rot2 = _transformCache.rotation;
	}

	private void LockBall()
	{
		if (player.BallManager.FindBall(out var ballData)) {
			lockTarget = _physicsEngine.GetTransform(ballData.Id);
			if (lockTarget != null) {
				_ballFollowOffset       =
					_ballFollowOffsetTarget = _transformCache.position - lockTarget.position;   // ★ init both
				_ballFollowVel          = Vector3.zero;
				_ballOffsetVel          = Vector3.zero;                                     // ★
				_orbitYaw   = 0f;
				_orbitPitch = 0f;
				_smoothedBallPos = lockTarget.position;
			}
		}
	}

	private void LockBallTarget()
	{
		if (player.BallManager.FindBall(out var ballData)) {
			lockTarget = _physicsEngine.GetTransform(ballData.Id);
			if (lockTarget != null) {
				_smoothedBallPos = lockTarget.position;
				_ballPosVel = Vector3.zero;
			}
		}
	}

	private void UpdateMouse()
	{
		/* -------------- KEEP THIS NEW BLOCK AT THE VERY TOP -------------- */
		// ★ When we’re **not** locked-on, keep the “dummy” pivot parked at
		//   (0,0,0) every frame so free-orbit behaves exactly like before.
		if (lockTarget == null)
		{
			_transformCache.position  = Vector3.zero;
			_dummyTransform.position  = Vector3.zero;
		}
		/* ----------------------------------------------------------------- */

		bool hasHitRestrictedHitArea = false;

		/* ========== ORBIT (left mouse) ========== */
		if (_mouse.leftButton.wasPressedThisFrame) {
			_mouse0StartPosition = _mouse.position.ReadValue();
			_isTrackingMouse0 = true;
		}

		/* ---------------- ORBIT (left mouse) ---------------- */
		if (_isTrackingMouse0) {
			var delta = _mouse.position.ReadValue() - _mouse0StartPosition;
			_mouse0StartPosition = _mouse.position.ReadValue();

			if (!hasHitRestrictedHitArea) {
				float yawDelta   =  delta.x * orbitSpeed / 75f;
				float pitchDelta = -delta.y * orbitSpeed / 75f;

				if (lockTarget != null && _isTrackingBall) {
					/* ---------- locked-on orbit (already working) ---------- */
					_orbitYaw   += yawDelta;
					_orbitPitch  = Mathf.Clamp(_orbitPitch + pitchDelta, -80f, 80f);

					Quaternion q = Quaternion.Euler(_orbitPitch, _orbitYaw, 0f);
					float r = _ballFollowOffsetTarget.magnitude;
					_ballFollowOffsetTarget = q * (Vector3.back * r);
				}
				else if (lockTarget == null) {
					/* ---------- FREE ORBIT  —  PUT THESE LINES BACK ---------- */
					_transformCache.position  = Vector3.zero;         // pivot at world-origin
					_dummyTransform.position  = Vector3.zero;

					_dummyTransform.Rotate(Vector3.up,              yawDelta,   Space.World);
					_dummyTransform.Rotate(_dummyTransform.right.normalized,   pitchDelta, Space.World);

					_rot2 = _dummyTransform.rotation;                 // used by smoothing
				}
			}
		}

		if (_mouse.leftButton.wasReleasedThisFrame) {
			_isTrackingMouse0 = false;
		}

		if (lockTarget == null) {

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
		}

		/* ========== ZOOM (scroll) ========== */
		if (!isAnimating && !hasHitRestrictedHitArea) {
			float deltaScroll = _mouse.scroll.y.ReadValue() / 300f * zoomSpeed;
			/* ---------------  UpdateMouse() – ZOOM in lock  --------------- */
			if (lockTarget != null && _isTrackingBall) {      // ★ only follow-lock zooms
				float r = Mathf.Max(_ballFollowOffsetTarget.magnitude * (1f - deltaScroll), RadiusMin);
				_ballFollowOffsetTarget = _ballFollowOffsetTarget.normalized * r;
			} else if (lockTarget == null) {
				/* original zoom path (unchanged) */
				_radius  = Mathf.Max(_radius  - deltaScroll * _radius, RadiusMin);
			}
		}

		/* ========== free-orbit smoothing (only when unlocked) ========== */
		if (lockTarget == null) {
			_transformCache.rotation = Quaternion.Lerp(
				_transformCache.rotation, _rot2, Time.deltaTime / smoothing * 80f);

			_positionOffsetCurrent = Vector3.Lerp(
				_positionOffsetCurrent, positionOffset, Time.deltaTime / smoothing * 80f);
			_radiusCurrent = Mathf.Lerp(_radiusCurrent, _radius, Time.deltaTime / smoothing * 80f);

			_focusPoint          = _transformCache.forward * -_radiusCurrent;
			_transformCache.position = _focusPoint + _positionOffsetCurrent;
			_dummyTransform.position = _transformCache.position;
		}
	}
}
