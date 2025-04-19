// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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

using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using VisualPinball.Engine.Common;

namespace VisualPinball.Unity
{
	public class BallShotComponent : BallDebugComponent
	{
		bool _activated = false;       //!< DebugShot mode activation flag
		bool _mouseDown;          //!< mouse button down flag

		public float ForceMultiplier = 0.2F;

		float m_elasped = 0f;   //!< elapsed time since last click

		private const float GizmoHeight = 0.025f;
		private const string NameParent = "Current";
		private const string NameStart = "StartGizmo";
		private const string NameEnd = "EndGizmo";
		private const string NameDirection = "DirectionGizmo";

		private void Start()
		{
			CreateShotGizmos(NameParent);

			//for (int i = 1; i <= 12; i++)
			//{
			//	CreateShot("F" + i);
			//}

			SetVisible(false);
		}

		private void SetVisible(bool b, GameObject o = null)
		{
			if (o == null) {
				o = gameObject;
			}

			if (o.GetComponent<Renderer>() != null) {
				o.GetComponent<Renderer>().enabled = b;
			}

			for (var i = 0; i < o.transform.childCount; i++) {
				SetVisible(b, o.transform.GetChild(i).gameObject);
			}
		}

		private void CreateShotGizmos(string parent)
		{
			// If already loaded, to not modify
			if (transform.Find(parent) != null) {
				return;
			}

			// Father object
			var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
			Destroy(plane.GetComponent<UnityEngine.Collider>());
			Destroy(plane.GetComponent<Renderer>());
			plane.name = parent;
			plane.transform.SetParent(transform, false);

			// Shot start
			var point1 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			Destroy(point1.GetComponent<UnityEngine.Collider>());
			point1.name = NameStart;
			point1.transform.parent = plane.transform;
			point1.transform.localScale = Vector3.one * 0.027f; // ball size = 27mm

			// Shot direction end
			var point2 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			Destroy(point2.GetComponent<UnityEngine.Collider>());
			point2.name = NameEnd;
			point2.transform.parent = plane.transform;
			point2.transform.localScale = Vector3.one * 0.027f;

			var line = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
			Destroy(line.GetComponent<UnityEngine.Collider>());
			line.name = NameDirection;
			line.transform.parent = plane.transform;
			line.transform.localScale = new Vector3(0.005f, 1f, 0.005f);
		}

		private void SetShotStart(string parent, float3 p)
		{
			transform.Find($"{parent}/{NameStart}").localPosition = new float3(p.x, GizmoHeight, p.z);  // Sets the fathers position (start is the reference)
		}

		private void SetShotEnd(string parent, float3 p)
		{
			p = new float3(p.x, GizmoHeight, p.z);
			transform.Find($"{parent}/{NameEnd}").localPosition = p;    // Sets the direction's end position

			// Set the shooting line
			var ps = transform.Find($"{parent}/{NameStart}").localPosition;
			var pe = (Vector3)p;
			var dir = transform.Find($"{parent}/{NameDirection}");
			var length = (pe - ps).magnitude;
			dir.localPosition = (pe + ps) * 0.5f;
			dir.localScale = new Vector3(0.005f, length * 0.5f, 0.005f);// *Globals.g_Scale;
			dir.LookAt(_playfield.transform.localToWorldMatrix.MultiplyPoint(pe));
			dir.Rotate(90.0f, 0.0f, 0.0f);
		}

		private void Update()
		{
			if (!Camera.main || !_playfield || !_player) {
				return;
			}

			if (Mouse.current.middleButton.wasPressedThisFrame || _mouseDown) {
				if (!_mouseDown) {   // just clicked (set start)
					m_elasped = 0f;
					if (GetCursorPositionOnPlayfield(out var vpxPos, out var worldPos)) {
						SetShotStart(NameParent, worldPos);
					}

					if (!_activated) {
						SetVisible(true, transform.Find(NameParent).gameObject);
					}

				} else {
					m_elasped += Time.deltaTime;
					if (GetCursorPositionOnPlayfield(out var pos, out var worldPos)) {
						SetShotEnd(NameParent, worldPos);
						// SetShotForce (NameParent,50f);//1f/m_elasped);
					}
				}
				_mouseDown = true;
			}

			if (Mouse.current.middleButton.wasReleasedThisFrame) {
				if (_mouseDown) {    // just released
					LaunchShot(NameParent);
					if (!_activated) {
						SetVisible(false, transform.Find(NameParent).gameObject);
					}
				}
				_mouseDown = false;
			}

			if (Keyboard.current.spaceKey.wasPressedThisFrame) {
				LaunchShot(NameParent);
			}
		}

		private void LaunchShot(string n)
		{
			var ps = _wtl.MultiplyPoint(transform.Find($"{n}/{NameStart}").localPosition);
			var pe = _wtl.MultiplyPoint(transform.Find($"{n}/{NameEnd}").localPosition);

			var dir = pe - ps;
			var mag = dir.magnitude; // To reuse magnitude for force

			var angle = mag > Mathf.Epsilon
				? Vector3.SignedAngle(Vector3.up, dir/mag, Vector3.forward) + 180F
				: 0F;

			if (!Keyboard.current.leftCtrlKey.isPressed) {
				if (_player.BallManager.FindNearest(new float2(ps.x, ps.y), out var nearestBall)) {
					_player.BallManager.DestroyBall(nearestBall.Id);
				}
			}
			_player.BallManager.CreateBall(new DebugBallCreator(ps.x, ps.y, PhysicsConstants.PhysSkin, angle, mag * ForceMultiplier));
		}
	}
}
