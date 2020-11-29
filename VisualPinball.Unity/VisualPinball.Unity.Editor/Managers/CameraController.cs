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

		[SerializeReference]
		Camera _camera;
		[SerializeReference]
		Transform _transform;
		[SerializeReference]
		Transform _altitude;
		[SerializeReference]
		Transform _azimuth;
		[SerializeReference]
		Transform _offset;
		[SerializeReference]
		GameObject _table;
		[SerializeReference]
		TableAuthoring _tableAuth;

		bool initialized = false;
		#endregion

		#region Public Variables


		public int CameraPreset = 0;
		List<CameraPreset> cameraPresets;
		public int presetCount = 0;

		[Range(-0.2f, 0.2f)]
		public float XOffset = 0f;
		[Range(-1f, 1f)]
		public float YOffset = 0f;
		[Range(-1f, 1f)]
		public float ZOffset = 0f;

		public Vector3 Offset;

		[Range(0f, 10f)]
		public float Distance = 1.7f;

		[Range(2, 80)]
		public float FOV = 25f;

		[Range(0, 180)]
		public float Angle = 10f;

		[Range(0, 360)]
		public float Orbit = 0;

		public int Preset = 1;
		private int prevPreset = 1;
		private bool isAnimating = false;
		#endregion


		

		void Init()
		{
			
			cameraPresets = new List<CameraPreset>();
			//GetPrefereces(); 
			
			SetupPresets();

			if(EditorApplication.isPlaying)
			{
				initialized = true; 
				return;
			}
			_tableAuth = TableManager.GetActiveTable(true);
			if(_tableAuth)
			{
				_table = _tableAuth.gameObject.GetComponentInChildren<PlayfieldAuthoring>().gameObject;
				
				_camera = this.gameObject.GetComponentInChildren<Camera>();
				_transform = _camera.transform;
				_altitude = _transform.transform.parent;
				_azimuth = _altitude.parent;
				_offset = _azimuth.parent;

				if(_table != null) initialized = true;
			}
		}



		private void GetPrefereces()
		{
			Preset = EditorPrefs.GetInt("ccPreset");
			
		}

		private void Update()
		{
			if(isAnimating)
			{
				Orbit += Time.deltaTime * 15f;
				//ApplyProperties(); 
				_azimuth.localRotation = Quaternion.Euler(new Vector3(0, Orbit, 0));

				if(Orbit >= 360)
				{
					Orbit = 0;
					isAnimating = false; 
				}
				
			}
		}

		private void GetPresets()
		{
			presetCount = EditorPrefs.GetInt("ccPresetCount");
			/*
			for(int i = 0; i < presetCount; i++)
			{
				float offsetx = EditorPrefs.Get
			}
			*/
		}

		private void SetupPresets()
		{

			AddPreset("Med-High", new Vector3(0, 0, -0.25f), 9.9f, 4.25f, 51.2f, 0f);
			AddPreset("High-Flat", new Vector3(0, 0, -0.16f), 5f, 9f, 61.5f, 0f);
			AddPreset("Low-Wide", new Vector3(0, 0, -0.57f), 33.6f, 1.16f, 54.9f, 0f);

			presetCount = cameraPresets.Count;
			Debug.Log("Setup Presets: " + presetCount);

		}

		private void AddPreset(string name, Vector3 offset, float fOV, float distance, float angle, float orbit)
		{
			CameraPreset camPres = new CameraPreset();
			camPres.name = name;
			camPres.offset = offset;
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
				prevPreset = Preset;
				SetProperties(c.name, c.offset, c.fov, c.distance, c.angle, c.orbit);

			}
		}

		private void SetProperties(string name, Vector3 offset, float fov, float distance, float angle, float orbit)
		{
			XOffset = offset.x;
			YOffset = offset.y;
			ZOffset = offset.z;
			FOV = fov;
			Distance = distance;
			Angle = angle;
			Orbit = orbit;

			ApplyProperties();

		}
		public void CreatePreset()
		{
			AddPreset("Name", new Vector3(XOffset, YOffset, ZOffset), FOV, Distance, Angle, Orbit);
			presetCount = cameraPresets.Count;

		}
		public void RemovePreset()
		{
			cameraPresets.RemoveAt(Preset-1);
			prevPreset = Mathf.Max(0, Preset - 1);
			Preset = prevPreset; 
			presetCount = cameraPresets.Count; 

		}

		public void ApplyProperties()
		{
			
			if(!initialized || !_table || !_tableAuth || cameraPresets == null) Init(); 


			if(initialized && _table && _tableAuth)
			{
				if(prevPreset != Preset) ApplyPreset(Preset-1);
				 
				_camera.transform.localPosition = new Vector3(0, 0, -Distance);
				_altitude.localRotation = Quaternion.Euler(new Vector3(Angle, 0, 0));
				_azimuth.localRotation = Quaternion.Euler(new Vector3(0, Orbit, 0));
				Offset = new Vector3(XOffset, YOffset, ZOffset);
				_offset.localPosition = GetTableCenter() + Offset;
				_camera.fieldOfView = FOV;

				Bounds tb = TableManager.GetTableBounds(); 
				Vector3 p = _camera.transform.position;
				Vector3 nearPoint = tb.ClosestPoint(_camera.transform.position);
				float deltaN = Vector3.Magnitude(p - nearPoint);
				float deltaF = Mathf.Max(Vector3.Distance(p, tb.max), Vector3.Distance(p, tb.min)); 

				//TODO: Replace this with proper frustum distances.  
				float nearplane = Mathf.Max(0.001f, Mathf.Abs(deltaN * 0.9f));
				float farplane = Mathf.Max(1f, Mathf.Abs(deltaF*1.1f)); 

				_camera.nearClipPlane = nearplane;
				_camera.farClipPlane = farplane; 

			}
			
		}

		public void AnimateOrbit()
		{
			isAnimating = true;
			_camera = this.gameObject.GetComponentInChildren<Camera>();
			_transform = _camera.transform;
			_altitude = _transform.transform.parent;
			_azimuth = _altitude.parent;
			_offset = _azimuth.parent;
		}

		private Vector3 GetTableCenter()
		{
				
			Vector3 center = _table.GetComponent<MeshRenderer>().bounds.center;
			return center; 
		}



	}

}
