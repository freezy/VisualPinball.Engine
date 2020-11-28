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


//TODO: Turn this into a proper editor authoring compoenent. 

namespace VisualPinball.Unity
{
	[ExecuteAlways]
	public class CameraController : MonoBehaviour
	{
		#region Private Variables 
		
		//List<CameraPresetStruct> cameraPresets;
		Camera _camera;
		Transform _transform;
		Transform _altitude;
		Transform _azimuth;
		Transform _offset;
		GameObject _table; 

		bool initialized = false;
		#endregion

		#region Public Variables

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

		//TODO: Convert this to OnGUI
		private void Update()
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

				float nearplane = Mathf.Abs(deltaN.magnitude*0.9f);
				float farplane = Mathf.Abs(deltaF.magnitude*1.5f); 

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
