using System;
using UnityEngine;

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
}
