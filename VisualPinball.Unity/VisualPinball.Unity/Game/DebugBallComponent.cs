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
using Codice.CM.Common.Merge;
using NLog.LayoutRenderers.Wrappers;
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

			var playfieldTransform = _playfield.transform;
			var ltw = playfieldTransform.localToWorldMatrix;
			var wtl = playfieldTransform.worldToLocalMatrix;

			var z = _playfield.PlayfieldHeight;
			var p1 = ltw.MultiplyPoint(new Vector3(-100f, 100f, z));
			var p2 = ltw.MultiplyPoint(new Vector3(100f, 100f, z));
			var p3 = ltw.MultiplyPoint(new Vector3(100f, -100f, z));

			Gizmos.DrawLine(p1, p2);
			Gizmos.DrawLine(p2, p3);
			Gizmos.DrawLine(p3, p1);

			var planeWorld = new Plane();
			planeWorld.Set3Points(p1, p2, p3);

			var ray = SceneView.lastActiveSceneView.camera.ScreenPointToRay(Event.current.mousePosition);
			if (planeWorld.Raycast(ray, out var enter)) {
				var playfieldPosWorld = ray.GetPoint(enter);
				var playfieldPosLocal = wtl.MultiplyPoint(playfieldPosWorld);

				Debug.Log($"Position on playfield: {playfieldPosLocal}");
			}
		}
	}
}
