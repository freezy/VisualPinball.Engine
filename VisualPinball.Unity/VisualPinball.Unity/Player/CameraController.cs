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
using System.IO;

//TODO: Turn this into a proper editor authoring component.

namespace VisualPinball.Unity
{
	[ExecuteAlways]
	public class CameraController : MonoBehaviour
	{
		private const string SettingsFilename = "CameraSettings.json";
		private const string PresetsFilename =  "CameraPresets.json";

		public int cameraPreset;
		public List<CameraPreset> cameraPresets;
		public int presetCount;

		/*
		[Range(-0.2f, 0.2f)]
		public float xOffset;

		[Range(-1f, 1f)]
		public float yOffset;

		[Range(-1f, 1f)]
		public float zOffset;
*/
		public Vector3 offset;

		[Range(0f, 10f)]
		public float distance = 1.7f;

		[Range(2, 80)]
		public float fov = 25f;

		[Range(0, 180)]
		public float angle = 10f;

		[Range(0, 360)]
		public float orbit;

		public int preset = 1;
		public string presetName = "Preset";

		[SerializeReference] private Camera _camera;
		[SerializeReference] private Transform _transform;
		[SerializeReference] private Transform _altitude;
		[SerializeReference] private Transform _azimuth;
		[SerializeReference] private Transform _offset;
		[SerializeReference] private GameObject _table;
		[SerializeReference] private TableAuthoring _tableAuth;

		private int _prevPreset = 1;
		private bool _initialized;




		public void ApplyProperties()
		{

			if (!_initialized || !_table || !_tableAuth || cameraPresets == null)
			{
				Init();
			}


			if(_initialized && _table && _tableAuth)
			{
				if (_prevPreset != preset)
				{
					ApplyPreset(preset-1);
				}

				_camera.transform.localPosition = new Vector3(0, 0, -distance);
				_altitude.localRotation = Quaternion.Euler(new Vector3(angle, 0, 0));
				_azimuth.localRotation = Quaternion.Euler(new Vector3(0, orbit, 0));
				offset = new Vector3(xOffset, yOffset, zOffset);
				_offset.localPosition = GetTableCenter() + offset;
				_camera.fieldOfView = fov;

				Bounds tb = TableManager.GetTableBounds();
				var p = _camera.transform.position;
				var nearPoint = tb.ClosestPoint(_camera.transform.position);
				var deltaN = Vector3.Magnitude(p - nearPoint);
				var deltaF = Mathf.Max(Vector3.Distance(p, tb.max), Vector3.Distance(p, tb.min));

				//TODO: Replace this with proper frustum distances.
				var nearPlane = Mathf.Max(0.001f, Mathf.Abs(deltaN * 0.9f));
				var farPlane = Mathf.Max(1f, Mathf.Abs(deltaF*1.1f));

				_camera.nearClipPlane = nearPlane;
				_camera.farClipPlane = farPlane;
			}
		}

		private void Init()
		{

			cameraPresets = new List<CameraPreset>();
			GetSavedSettings();

			if (EditorApplication.isPlaying)
			{
				_initialized = true;
				return;
			}

			_tableAuth = TableManager.GetActiveTable(true);
			if (_tableAuth)
			{
				_table = _tableAuth.gameObject.GetComponentInChildren<PlayfieldAuthoring>().gameObject;

				_camera = gameObject.GetComponentInChildren<Camera>();
				_transform = _camera.transform;
				_altitude = _transform.transform.parent;
				_azimuth = _altitude.parent;
				_offset = _azimuth.parent;

				if(_table != null) _initialized = true;
			}
		}



		private void SetupPresets()
		{

			AddPreset("Standard Flat", new Vector3(0, -0.17f, 0.07f), 9.4f, 3.9f, 49.6f, 0f);
			AddPreset("Top Down", new Vector3(0, 0, -0.09f), 25.4f, 1.77f, 83.1f, 0f);
			AddPreset("Wide", new Vector3(0, -0.26f, -0.12f), 31.6f, 1.88f, 40.3f, 0f);

			presetCount = cameraPresets.Count;
			preset = 1;
			_prevPreset = 1;
		}

		private void AddPreset(string name, Vector3 offset, float fOv, float distance, float angle, float orbit)
		{
			var camPres = new CameraPreset {
				name = name,
				offset = offset,
				fov = fOv,
				distance = distance,
				angle = angle,
				orbit = orbit
			};

			cameraPresets.Add(camPres);
			_prevPreset = preset++;
		}

		private void ApplyPreset(int num)
		{
			if(num < cameraPresets.Count && num >= 0)
			{
				var c = cameraPresets[num];
				_prevPreset = preset;
				SetProperties(c.name, c.offset, c.fov, c.distance, c.angle, c.orbit);

			}
		}

		private void SetProperties(string name, Vector3 offset, float fov, float distance, float angle, float orbit)
		{
			xOffset = offset.x;
			yOffset = offset.y;
			zOffset = offset.z;
			this.fov = fov;
			this.distance = distance;
			this.angle = angle;
			this.orbit = orbit;
			presetName = name;

			ApplyProperties();
		}


		private Vector3 GetTableCenter()
		{
			var center = _table.GetComponent<MeshRenderer>().bounds.center;
			return center;
		}
	}
}
