// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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


using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor; 
using VisualPinball.Unity.Editor.Utils;
using System;


//TODO: Turn this into a proper editor authoring compoenent. 

namespace VisualPinball.Unity
{
	[ExecuteAlways]
	public class CameraController : MonoBehaviour
	{
		#region Private Variables 
		
		
		Camera _camera;
		Transform _transform;
		Transform _altitude;
		Transform _azimuth;
		Transform _offset;
		GameObject _table; 

		bool initialized = false;
		#endregion

		#region Public Variables

		
		public int CameraPreset = 0;
		List<CameraPreset> cameraPresets;
		public int presetCount = 0; 

		[Range(-1f, 1f)]
		public float YOffset = 0f;

		[Range(0f, 10f)]
		public float Distance = 1.7f;

		[Range(2, 80)]
		public float FOV = 25f; 

		[Range(0, 180)]
		public float Angle = 10f;

		[Range(0, 360)]
		public float Orbit = 0;

		#endregion


		
		void Init()
		{
			cameraPresets = new List<CameraPreset>();
			SetupPresets(); 	

			TableAuthoring ta = TableManager.GetActiveTable(true);
			if(ta)
			{
				_table = ta.gameObject.GetComponentInChildren<PlayfieldAuthoring>().gameObject;
				_camera = this.gameObject.GetComponentInChildren<Camera>();
				_transform = _camera.transform;
				_altitude = _transform.transform.parent;
				_azimuth = _altitude.parent;
				_offset = _azimuth.parent;

				if(_table != null) initialized = true;
			}
		}

		private void SetupPresets()
		{
			
			AddPreset("Med-High", -0.25f, 9.9f, 4.25f, 51.2f, 0f);
			AddPreset("High-Flat", -0.16f, 5f, 9f, 61.5f, 0f);
			AddPreset("Low-Wide", -0.57f, 33.6f, 1.16f, 54.9f, 0f);
			
			presetCount = cameraPresets.Count; 

		}

		private void AddPreset(string name, float yOffset, float fOV, float distance, float angle, float orbit)
		{
			CameraPreset camPres = new CameraPreset();
			camPres.name = name; 
			camPres.yOffset = yOffset;
			camPres.fov = fOV;
			camPres.distance = distance;
			camPres.angle = angle;
			camPres.orbit = orbit;

			cameraPresets.Add(camPres); 
		}

		private void ApplyPreset(int num)
		{
			if(num < cameraPresets.Count)
			{
				CameraPreset c = cameraPresets[num];
				SetProperties(c.name, c.yOffset, c.fov, c.distance, c.angle, c.orbit); 
			}
		}

		private void SetProperties(string name, float yOffset, float fov, float distance, float angle, float orbit)
		{
			YOffset = yOffset;
			FOV = fov;
			Distance = distance;
			Angle = angle;
			Orbit = orbit;
			ApplyProperties(); 

		}

		//TODO: Convert this to OnGUI
		public void ApplyProperties()
		{
			
			if(!initialized || !_table) Init(); 


			if(initialized && _table)
			{
				_camera.transform.localPosition = new Vector3(0, 0, -Distance);
				_altitude.localRotation = Quaternion.Euler(new Vector3(Angle, 0, 0));
				_azimuth.localRotation = Quaternion.Euler(new Vector3(0, Orbit, 0));
				_offset.localPosition = GetTableCenter() + new Vector3(0, 0, YOffset);
				_camera.fieldOfView = FOV;

				Vector3 p = _camera.transform.position;
				Vector3 nearPoint = _table.GetComponent<MeshRenderer>().bounds.ClosestPoint(_camera.transform.position);
				Vector3 deltaN = p - nearPoint;
				Vector3 deltaF = p; 
				//TODO: Replace this with proper frustum distances.  
				float nearplane = Mathf.Abs(deltaN.magnitude * 0.7f);
				float farplane = Mathf.Abs(deltaF.magnitude * 1.5f); 

				_camera.nearClipPlane = nearplane;
				_camera.farClipPlane = farplane; 

			}
			
		}

		private Vector3 GetTableCenter()
		{
				
			Vector3 center = _table.GetComponent<MeshRenderer>().bounds.center;
			return center; 
		}

	}

}
