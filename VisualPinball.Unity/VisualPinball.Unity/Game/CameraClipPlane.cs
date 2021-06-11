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
using Unity.Mathematics;
using UnityEngine;


namespace VisualPinball.Unity
{
	[ExecuteAlways]
	public class CameraClipPlane : MonoBehaviour
	{
		
		[NonSerialized]
		public Camera Camera;

		//Stores the table bounds. 
		private Bounds tableBounds;
		private Vector3 previousCameraPosition = Vector3.zero;

		private void OnEnable()
		{
			SetClipPlanes(0.001f, 9f); 
		}

		private void Update()
		{
			if(!GetCameraComponent())
			{
				return;
			}

			if(previousCameraPosition != Camera.transform.position)
			{
				UpdateClipPlanes();
				previousCameraPosition = Camera.transform.position; 
			}
		}

		/// <summary>
		/// Updates the camera clipping planes based on the table bounds.
		/// </summary>
		public bool UpdateClipPlanes()
		{

			//Early out if no table is selected.  
			if(!TableSelector.Instance.HasSelectedTable)
			{
				return false;
			}

			//Early out if we don't have a camera to work on. 
			if(!GetCameraComponent())
			{
				return false;
			}

			//Get selected table reference. 
			var table = TableSelector.Instance.SelectedTable; 

			if(Application.isPlaying)
			{
				tableBounds = table._tableBounds;  //When playing at runtime, get the stored table bounds value instead of calculating. 
			}
			else
			{
				tableBounds = table.GetTableBounds();  //When in editor, calculate the bounds in case things have changed.  
			}

			var trans = Camera.transform;

			var cameraPos = trans.position; // camera position. 
			var sphereExtent = tableBounds.extents.magnitude; //sphere radius of the bounds. 
			float cameraToCenter = Vector3.Distance(cameraPos, tableBounds.center);
			var nearPlane = math.max(cameraPos.magnitude - sphereExtent, 0.001f); //Assign initial near plane used when camera is not in the sphere. 
			var farPlane = math.max(1f, nearPlane + sphereExtent * 2f); //initial far bounds 

			if(cameraToCenter < sphereExtent)
			{
				nearPlane = 0.01f; //camera is in the bounds so drop the near plane to very low. 
				farPlane = math.max(0.01f, sphereExtent + Vector3.Distance(cameraPos, tableBounds.center)); //set far plane to delta between camera and furthest bound. 

			}

			SetClipPlanes(nearPlane, farPlane); 

			return true; 
		}

		/// <summary>
		/// Gets the currently active or attached camera component. 
		/// </summary>
		/// <returns>False when no camera can be found.</returns>
		private bool GetCameraComponent()
		{
			Camera = GetComponent<Camera>(); //Get the camera component we are attached to if present.  

			if(Camera == null)
			{
				Camera = UnityEngine.Camera.current;  //Get the current active camera if not on the camera component.  
			}

			return Camera != null;
		}
		
		/// <summary>
		/// Sets the camera clipping planes based on manually derived values. 
		/// </summary>
		/// <param name="near">The near clip plane value</param>
		/// <param name="far">The far clip plane value</param>
		/// <returns>False when no camera could be found.</returns>
		public bool SetClipPlanes(float near, float far)
		{
			if (Camera == null) {
				return false;
			}
			Camera.nearClipPlane = math.max(0.001f, near);
			Camera.farClipPlane = math.max(0.01f, far); 

			return true; 
		}
		

	}
}
