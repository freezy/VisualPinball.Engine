using System;
using UnityEngine;

namespace VisualPinball.Unity
{
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
}
