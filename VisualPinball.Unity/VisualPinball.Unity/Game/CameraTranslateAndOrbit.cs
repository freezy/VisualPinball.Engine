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
	public float zoomSpeed = 0.1f;

	public Transform transformCache;
	public bool isAnimating;

	public GameObject dummyForRotation;
	public Transform dummyTransform;

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
		_radius = Vector3.Distance(Vector3.zero, transform.position);
		transformCache = transform;
		_focusPoint = transformCache.forward * -1f * _radius;
		_positionOffsetCurrent = positionOffset;
		_radiusCurrent = _radius;
		dummyForRotation = new GameObject();
		dummyTransform = dummyForRotation.transform;

		dummyTransform.rotation = transformCache.rotation;
		dummyTransform.position = transformCache.position;
		_rot2 = transformCache.rotation;
	}

	private void OrbitAroundObject(Vector3 newOffset, float radiusRef)
	{
		positionOffset = newOffset;
		_positionOffsetCurrent = positionOffset;
		_radius = radiusRef;
		_radiusCurrent = _radius;
	}

	// Update is called once per frame
	private void Update()
	{
		if (Touchscreen.current != null) {
			UpdateTouchscreen();

			return;
		}

		if (Mouse.current != null) {
			UpdateMouse();
		}
	}

	private void UpdateTouchscreen()
	{
		if (Touch.activeFingers.Count == 1) {
			Touch touch = Touch.activeTouches[0];

			transformCache.position = dummyTransform.position = Vector3.zero;

			var hasHitRestrictedHitArea = false;

			if (touch.phase == TouchPhase.Began) {
				_touch0StartPosition = touch.screenPosition;
				_isTrackingTouch0 = true;
			}

			if (_touch0StartPosition.x < 200f && _touch0StartPosition.y > Screen.height - 200f) {
				hasHitRestrictedHitArea = true;
			}

			if (!hasHitRestrictedHitArea) {
				if (_isTrackingTouch0) {
					Vector3 touchPositionDifference = touch.screenPosition - _touch0StartPosition;
					dummyTransform.Rotate(Vector3.up, touchPositionDifference.x * 0.4f, Space.World);

					dummyTransform.Rotate(dummyTransform.right.normalized, touchPositionDifference.y * -0.4f,
						Space.World);
					_rot2 = dummyTransform.rotation;
					_touch0StartPosition = touch.screenPosition;
				}
				transformCache.rotation = Quaternion.Lerp(transformCache.rotation, _rot2, Time.deltaTime * 4f);
			}

			if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) {
				_isTrackingTouch0 = false;
			}
		}
		else if (Touch.activeFingers.Count == 2) {
			var firstTouch = Touch.activeTouches[0];
			var secondTouch = Touch.activeTouches[1];

			transformCache.position = dummyTransform.position = Vector3.zero;

			if (firstTouch.phase == TouchPhase.Began || secondTouch.phase == TouchPhase.Began) {
				_startMultiTouchDistance = Vector2.Distance(firstTouch.screenPosition, secondTouch.screenPosition);

				_startMultiTouchRadius = _radius;
			}

			if (firstTouch.phase == TouchPhase.Moved || secondTouch.phase == TouchPhase.Moved) {
				_radius = _startMultiTouchRadius;

				var distance = Vector2.Distance(firstTouch.screenPosition, secondTouch.screenPosition) - _startMultiTouchDistance;			
				var delta = distance / 250 * zoomSpeed * -_radius;

				_radius += delta;

				if (_radius < RadiusMin) {
					var radDiff = RadiusMin - _radius;
					positionOffset += transformCache.forward * (radDiff * 4f);
					_radius = RadiusMin;
				}
			}
		}

		_positionOffsetCurrent = Vector3.Lerp(_positionOffsetCurrent, positionOffset, Time.deltaTime * 4f);
		_radiusCurrent = Mathf.Lerp(_radiusCurrent, _radius, Time.deltaTime * 4f);
		_focusPoint = transformCache.forward * -1f * _radiusCurrent;
		transformCache.position = _focusPoint + _positionOffsetCurrent;
		dummyTransform.position = transformCache.position;
	}

	private void UpdateMouse()
	{
		transformCache.position = dummyTransform.position = Vector3.zero;

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
				dummyTransform.Rotate(Vector3.up, mousePositionDifference.x * 0.4f, Space.World);

				dummyTransform.Rotate(dummyTransform.right.normalized, mousePositionDifference.y * -0.4f,
					Space.World);
				_rot2 = dummyTransform.rotation;
				_mouse0StartPosition = Mouse.current.position.ReadValue();
			}
			transformCache.rotation = Quaternion.Lerp(transformCache.rotation, _rot2, Time.deltaTime * 4f);
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

				positionOffset += transformCache.up.normalized * (mousePositionDifference.y * -(_radius / 2f / 100f));

				positionOffset += transformCache.right.normalized *
								  (mousePositionDifference.x * -(_radius / 2f / 100f));
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
				var delta = Mouse.current.scroll.y.ReadValue() / 1000f * zoomSpeed * -_radius;
				_radius += delta;
				if (_radius < RadiusMin) {
					var radDiff = RadiusMin - _radius;
					positionOffset += transformCache.forward * (radDiff * 4f);
					_radius = RadiusMin;
				}
			}
		}

		_positionOffsetCurrent = Vector3.Lerp(_positionOffsetCurrent, positionOffset, Time.deltaTime * 4f);
		_radiusCurrent = Mathf.Lerp(_radiusCurrent, _radius, Time.deltaTime * 4f);
		_focusPoint = transformCache.forward * -1f * _radiusCurrent;
		transformCache.position = _focusPoint + _positionOffsetCurrent;
		dummyTransform.position = transformCache.position;
	}
}
