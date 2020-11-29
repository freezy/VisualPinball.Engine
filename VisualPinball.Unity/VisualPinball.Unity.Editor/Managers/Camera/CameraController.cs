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
using System.IO;



//TODO: Turn this into a proper editor authoring compoenent. 

namespace VisualPinball.Unity
{
	/// <summary>
	/// Struct to store the camera presets. 
	/// </summary>
	[Serializable]
	public struct CameraPreset
	{

		public string name;// { get; set; }
		public Vector3 offset;// { get; set; }
		public float fov;// { get; set; }
		public float distance;// { get; set; }
		public float angle;// { get; set; }
		public float orbit;// { get; set; }


	}

	[Serializable]
	public class CameraSettings
	{
		//Preset Storage
		public int StoredPresets;
		//Current Var Storage
		public int cPreset;
		public string cName; 
		public Vector3 cOffset;
		public float cFOV;
		public float cDistance;
		public float cAngle;
		public float cOrbit;

	}

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
		public List<CameraPreset> cameraPresets;
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
		public string PresetName = "Preset"; 

		private int prevPreset = 1;
		#endregion


		

		void Init()
		{
			
			cameraPresets = new List<CameraPreset>();
			GetSavedSettings(); 
		
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

		public void LoadSettings()
		{
			GetSavedSettings(); 
		}

		public void SaveSettings()
		{
			SaveSettingsToFile(); 
		}

		private void GetSavedSettings()
		{
			string settingsPath =  Path.Combine(new string[] { Application.persistentDataPath, "CameraSettings.json" });
			string presetsPath = Path.Combine(new string[] { Application.persistentDataPath, "CameraPresets.json" });


			if(!System.IO.File.Exists(settingsPath) || !System.IO.File.Exists(presetsPath))
			{
				SetupPresets();
				return;
			}
			else
			{
				
				string json = System.IO.File.ReadAllText(settingsPath);
				CameraSettings readSettings = new CameraSettings();
				EditorJsonUtility.FromJsonOverwrite(json, readSettings); 
				if(readSettings.StoredPresets > 0)
				{
					
					cameraPresets.Clear();
					string campres = System.IO.File.ReadAllText(presetsPath);
					CameraPreset[] pres = JsonHelper.FromJson<CameraPreset>(campres);
					cameraPresets = new List<CameraPreset>(pres);
					if(cameraPresets.Count <= 0)
					{
						SetupPresets();
						return; 
					}

					Preset = 1;
					prevPreset = 1; 
				}

				Offset = readSettings.cOffset;
				XOffset = Offset.x;
				YOffset = Offset.y;
				ZOffset = Offset.z;
				FOV = readSettings.cFOV;
				Distance = readSettings.cDistance;
				Angle = readSettings.cAngle;
				Orbit = readSettings.cOrbit;
				Preset = readSettings.cPreset;
				prevPreset = Preset;
				presetCount = cameraPresets.Count; 

				Debug.Log("Loaded Camera Settings"); 
			}

		}

		private void SaveSettingsToFile()
		{
			string settingsPath = Path.Combine(new string[] { Application.persistentDataPath, "CameraSettings.json" });
			string presetsPath = Path.Combine(new string[] { Application.persistentDataPath, "CameraPresets.json" }); 

			CameraSettings cs = new CameraSettings();
			cs.cName = PresetName; 
			cs.cOffset = Offset;
			cs.cFOV = FOV;
			cs.cDistance = Distance;
			cs.cPreset = Preset;
			cs.cAngle = Angle;
			cs.cOrbit = Orbit;
			cs.StoredPresets = cameraPresets.Count; 

			
			string jsSave = EditorJsonUtility.ToJson(cs);
			File.WriteAllText(settingsPath, jsSave);
			
			CameraPreset[] camPres = cameraPresets.ToArray();
			string cPresets = JsonHelper.ToJson(camPres);
			File.WriteAllText(presetsPath, cPresets); 
			

			Debug.Log("Saved Camera Settings: " + settingsPath); 

		}

		private void SetupPresets()
		{

			AddPreset("Standard Flat", new Vector3(0, -0.17f, 0.07f), 9.4f, 3.9f, 49.6f, 0f);
			AddPreset("Top Down", new Vector3(0, 0, -0.09f), 25.4f, 1.77f, 83.1f, 0f);
			AddPreset("Wide", new Vector3(0, -0.26f, -0.12f), 31.6f, 1.88f, 40.3f, 0f);

			presetCount = cameraPresets.Count;
			Preset = 1;
			prevPreset = 1; 


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
			Preset = Preset + 1;
			prevPreset = Preset; 
		}

		private void ApplyPreset(int num)
		{
			if(num < cameraPresets.Count && num >= 0)
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
			PresetName = name; 

			ApplyProperties();

		}

		public void SavePreset()
		{
			CameraPreset camPres = new CameraPreset();
			camPres.name = PresetName;
			camPres.offset = new Vector3(XOffset, YOffset, ZOffset);
			camPres.fov = FOV;
			camPres.distance = Distance;
			camPres.angle = Angle;
			camPres.orbit = Orbit;
			int location = Mathf.Max(0, Preset - 1);
			cameraPresets[location] = camPres;
			SaveSettingsToFile(); 
		}

		public void CreatePreset()
		{
			AddPreset(PresetName, new Vector3(XOffset, YOffset, ZOffset), FOV, Distance, Angle, Orbit);
			presetCount = cameraPresets.Count;
			SaveSettingsToFile();

		}
		public void RemovePreset()
		{
			cameraPresets.RemoveAt(Preset-1);
			prevPreset = Mathf.Max(0, Preset - 1);
			Preset = prevPreset; 
			presetCount = cameraPresets.Count;
			SaveSettingsToFile();

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

		private Vector3 GetTableCenter()
		{
				
			Vector3 center = _table.GetComponent<MeshRenderer>().bounds.center;
			return center; 
		}



	}

}
