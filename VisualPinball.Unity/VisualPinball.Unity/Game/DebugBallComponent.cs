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
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VisualPinball.Unity
{
	[ExecuteAlways]
	public class DebugBallComponent : MonoBehaviour
	{

		private PlayfieldComponent _playfield;

		private void Awake()
		{
			_playfield = GetComponentInChildren<PlayfieldComponent>();
		}

		private void Update()
		{
			if (Camera.main == null || _playfield == null) {
				return;
			}

			if (Mouse.current.middleButton.wasPressedThisFrame) {
				var mouseOnScreenPos = Mouse.current.position.ReadValue();
				var ray = Camera.main.ScreenPointToRay(mouseOnScreenPos);

				Debug.Log($"Ray = {ray.origin} -> {ray.direction} ({mouseOnScreenPos})");

				// if (Physics.Raycast(ray, out var hit, 100)) {
				// 	Debug.Log($"Got a hit: {hit.transform.gameObject.name}");
				// } else {
				// 	Debug.Log($"Not hit.");
				// }
			}
		}

		private void OnDrawGizmos()
		{
			var ltw = _playfield.transform.localToWorldMatrix;
			var origin = ltw.MultiplyPoint(Vector3.zero);
			var up = ltw.MultiplyPoint(new Vector3(0, 0, 500f));
			var normal = up - origin;
			normal.Normalize();

			var cameraWorld = SceneView.lastActiveSceneView.camera.transform.position;

			//var plane = new Plane(Vector3.forward, m_DistanceFromCamera);

			Ray ray = SceneView.lastActiveSceneView.camera.ScreenPointToRay(Event.current.mousePosition);

			Gizmos.DrawLine(Vector3.zero, normal);
		}
	}
}
