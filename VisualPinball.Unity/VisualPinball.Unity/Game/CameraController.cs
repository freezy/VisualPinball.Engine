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

using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VisualPinball.Unity
{
	[ExecuteAlways]
	public class CameraController : MonoBehaviour
	{
		public CameraSetting activeSetting;

		public List<CameraSetting> cameraPresets;

		public float mouseSpeedH = 1f;	//Horizontal Mouse Speed
		public float mouseSpeedV = 1f;	//Vertical Mouse Speed
		public float mouseSpeedT =1f;	//Translation Speed
		//public float mouseSpeedD = 1f;		//Distance Increment Speed.  Disabled Temporarily
		//public float mouseSpeedZ = 1f;      //FOV Change Speed			 Disabled Temporarily
		public bool useInertia = true;      //Enables inertia on orientation change
		public bool invertX = false;        //Inverts the horizontal movement
		public bool invertY = false;		//Inverts the vertical movement

		[NonSerialized]
		public Camera Camera;


		//Private variables used for motion functions.
		private Vector3 _tableCenter = Vector3.zero;	//Stored table center for offset
		private float _hAccum = 0f;						//Accumulated horizontal value
		private float _vAccum = 0f;						//Accumulated vertical value
		private Vector3 _tAccum = Vector3.zero;			//Translation accumulation
		private float _cDistance = 0f;					//Current camera local Z distance
		private float _hInertial = 0f;					//Horizontal inertia value
		private float _vInertial = 0f;					//Vertical inertia value
		private bool _inertia = false;					//Current inertia state
		private Vector2 _lastAngularVector;				//Last vector for motion from camera angle shift.
		private Vector2 _prevLmbPosition = Vector2.zero;//Previous LMB Position
		private Vector2 _prevRmbPosition = Vector2.zero; //Previous RMB Position
		private float _cFOV;							//Current FOV

		private void OnEnable()
		{
			Camera = GetComponentInChildren<Camera>();

			// validate structure
			var trans = Camera.transform;
			var altitude = trans.transform.parent;
			if (altitude == null) {
				Camera = null;
				return;
			}
			var azimuth = altitude.parent;
			if (azimuth == null) {
				Camera = null;
				return;
			}
			var offset = azimuth.parent;
			if (offset == null) {
				Camera = null;
			}
		}

		private void Start()
		{
			//Initialize values from active settings
			_hAccum = activeSetting.orbit;
			_vAccum = activeSetting.angle;
			_cDistance = activeSetting.distance;
			_cFOV = activeSetting.fov;
		}

		/// <summary>
		/// Applies the current controller settings to the camera.
		/// </summary>
		public void ApplySetting()
		{
			if(!activeSetting)
			{
				return;
			}

			if(!TableSelector.Instance.HasSelectedTable)
			{
				return;
			}

			var table = TableSelector.Instance.SelectedTable;

			var trans = Camera.transform;
			var altitude = trans.transform.parent;
			var azimuth = altitude.parent;
			var offset = azimuth.parent;

			trans.localPosition = new Vector3(0, 0, -activeSetting.distance);
			altitude.localRotation = Quaternion.Euler(new Vector3(activeSetting.angle, 0, 0));
			azimuth.localRotation = Quaternion.Euler(new Vector3(0, activeSetting.orbit, 0));

			Camera.fieldOfView = activeSetting.fov;

			if(Application.isPlaying)
			{
				_tableCenter = table._tableCenter;
			}
			else
			{
				_tableCenter = table.GetTableCenter();
			}

			offset.localPosition = _tableCenter + activeSetting.offset;
		}

		public void FixedUpdate()
		{
			if(!Application.isPlaying)
			{
				return;
			}

			if(Mouse.current != null)
			{
				Debug.Log("I has mouse");

				var trans = Camera.transform;
				var altitude = trans.transform.parent;
				var azimuth = altitude.parent;
				var offset = azimuth.parent;


				//LMB Initialization
				if(Mouse.current.leftButton.wasPressedThisFrame)
				{
					_prevLmbPosition = Mouse.current.position.ReadValueFromPreviousFrame();
					float deltaMagnitude = Vector2.Distance(Mouse.current.position.ReadValue(), _prevLmbPosition);

					//Assume that a large magnitude means they clicked on another part of the screen and give it a default value.
					if(deltaMagnitude > 10)
					{
						_prevLmbPosition = Mouse.current.position.ReadValue();

					}

					_cDistance = Camera.transform.localPosition.z;
				}

				//LMB Hold Behavior:  Azimuth and Elevation adjustment.
				if(Mouse.current.leftButton.isPressed)
				{
					var curPos = Mouse.current.position.ReadValue();
					float h = curPos.x - _prevLmbPosition.x;
					float v = curPos.y - _prevLmbPosition.y;

					_prevLmbPosition = curPos;

					if(invertX) h = -h;
					if(invertY) v = -v;

					_hAccum = math.lerp(_hAccum, _hAccum + h, Time.deltaTime * (3f * mouseSpeedH));
					_vAccum = math.lerp(_vAccum, _vAccum - v, Time.deltaTime * (3f * mouseSpeedV));

					azimuth.localRotation = Quaternion.Euler(new Vector3(0, _hAccum, 0));
					altitude.localRotation = Quaternion.Euler(new Vector3(_vAccum, 0, 0));


					_lastAngularVector = Mouse.current.position.ReadValue() - Mouse.current.position.ReadValueFromPreviousFrame();
					_lastAngularVector.Normalize();
					_hInertial = _lastAngularVector.x * mouseSpeedH;
					_vInertial = _lastAngularVector.y * mouseSpeedV;

				}

				//Scroll Behavior: Distance to pivot adjustment
				if(Mouse.current.scroll.IsActuated() && !Keyboard.current.ctrlKey.isPressed)
				{
					var scrollDist = Mouse.current.scroll.ReadValue();
					_cDistance += 0.002f * scrollDist.y - Mouse.current.scroll.ReadValueFromPreviousFrame().y;
					Camera.transform.localPosition = new Vector3(0, 0, math.clamp(_cDistance, -8f, -0.1f));

				}

				//LMB Release
				if(Mouse.current.leftButton.wasReleasedThisFrame)
				{
					_inertia = useInertia;
				}


				//RMB Initialization
				if(Mouse.current.rightButton.wasPressedThisFrame)
				{
					//tAccum = Vector3.zero; // offset.localPosition;
					_prevRmbPosition = Mouse.current.position.ReadValue();

				}

				//RMB Hold Behavior:  Pivot translation adjustment
				if(Mouse.current.rightButton.isPressed)
				{
					var curTPos = Mouse.current.position.ReadValue();
					float h = curTPos.x - _prevRmbPosition.x;
					float v = curTPos.y - _prevRmbPosition.y;

					//Angle correction based on current azimuth
					var theta = math.radians(-azimuth.localRotation.eulerAngles.y);
					var cos = math.cos(theta);
					var sin = math.sin(theta);

					if(invertX) h = -h;
					if(invertY) v = -v;

					h = -h;
					v = -v;

					var newh = h * cos - v * sin;
					var newv = h * sin + v * cos;

					var posDelta = new Vector3(newh*0.03f, 0, newv*0.03f);
					_prevRmbPosition = curTPos;

					_tAccum = Vector3.Lerp(_tAccum, _tAccum + posDelta, Time.deltaTime * (3f * mouseSpeedT));


					offset.localPosition = _tableCenter + _tAccum;

				}

				//FOV Control
				if(Mouse.current.scroll.IsActuated() && Keyboard.current.ctrlKey.isPressed)
				{
					var scrollDist = Mouse.current.scroll.ReadValue();
					_cFOV += 0.002f * scrollDist.y - Mouse.current.scroll.ReadValueFromPreviousFrame().y;
					Camera.fieldOfView = _cFOV;

				}

				//Inertia
				if(_inertia == true)
				{
					_hInertial *= 0.8f;
					_vInertial *= 0.8f;

					_hAccum += _hInertial * mouseSpeedH;
					_vAccum += -_vInertial * mouseSpeedV;

					azimuth.localRotation = Quaternion.Euler(new Vector3(0, _hAccum, 0));
					altitude.localRotation = Quaternion.Euler(new Vector3(_vAccum, 0, 0));


					if(_hInertial <= 0.01f && _vInertial <= 0.01f)
					{
						_inertia = false;
					}
				}
			}

		}

		public void AdjustCameraHorizontal(float amount)
		{
			activeSetting.orbit += amount;
			ApplySetting();
		}




	}
}
