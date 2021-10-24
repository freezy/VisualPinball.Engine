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

using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VisualPinball.Unity
{
	public class DebugBallComponent : MonoBehaviour
	{

		private PlayfieldComponent _playfield;
		private Matrix4x4 _ltw;
		private Matrix4x4 _wtl;

		private Plane _playfieldPlane;
		private GameObject _ball;

		private void Awake()
		{
			_playfield = GetComponentInChildren<PlayfieldComponent>();
			_ball = _playfield.transform.Find("Ball").gameObject;

			var playfieldTransform = _playfield.transform;
			_ltw = playfieldTransform.localToWorldMatrix;
			_wtl = playfieldTransform.worldToLocalMatrix;

			var z = _playfield.PlayfieldHeight;
			var p1 = _ltw.MultiplyPoint(new Vector3(-100f, 100f, z));
			var p2 = _ltw.MultiplyPoint(new Vector3(100f, 100f, z));
			var p3 = _ltw.MultiplyPoint(new Vector3(100f, -100f, z));
			_playfieldPlane.Set3Points(p1, p2, p3);
		}

		private void Update()
		{
			if (Camera.main == null || _playfield == null) {
				return;
			}

			if (Mouse.current.middleButton.isPressed) {
				var mouseOnScreenPos = Mouse.current.position.ReadValue();
				var ray = Camera.main.ScreenPointToRay(mouseOnScreenPos);

				if (_playfieldPlane.Raycast(ray, out var enter)) {
					var playfieldPosWorld = ray.GetPoint(enter);
					var playfieldPosLocal = _wtl.MultiplyPoint(playfieldPosWorld);

					var ballPosLocal = _ball.transform.localPosition;
					ballPosLocal.x = playfieldPosLocal.x;
					ballPosLocal.y = playfieldPosLocal.y;
					_ball.transform.localPosition = ballPosLocal;

				} else {
					Debug.Log($"Missed.");
				}
			}
		}
	}
}
