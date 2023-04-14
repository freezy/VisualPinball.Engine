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

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using VisualPinball.Engine.Common;
using UnityEngine.InputSystem;
using VisualPinball.Unity;
using Unity.Mathematics;
using Unity.Entities;
using Unity.Collections;

namespace VisualPinball.Unity
{
	public class DebugShotComponent : MonoBehaviour
	{
		private EntityManager _entityManager;
		private EntityQuery _ballEntityQuery;

		bool _activated = false;       //!< DebugShot mode activation flag
		bool _mouseDown = false;           //!< mouse button down flag

		private PlayfieldComponent _playfield;
		private Matrix4x4 _ltw;
		private Matrix4x4 _wtl;

		private Plane _playfieldPlane;

		public float ForceMultiplier = 0.2F;


		private void Awake()
		{
			_playfield = GameObject.FindObjectOfType<PlayfieldComponent>();
			_ltw = Physics.VpxToWorld;
			_wtl = Physics.WorldToVpx;

			_entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
			_ballEntityQuery = _entityManager.CreateEntityQuery(typeof(BallData));
		}

		void Start()
		{
			{
				CreateShot("Current");
			}

			//for (int i = 1; i <= 12; i++)
			//{
			//	CreateShot("F" + i);
			//}

			SetVisible(false);
		}

		void SetVisible(bool b, GameObject o = null)
		{
			if (o == null)
				o = gameObject;

			if (o.GetComponent<Renderer>() != null)
				o.GetComponent<Renderer>().enabled = b;

			for (int i = 0; i < o.transform.childCount; i++)
				SetVisible(b, o.transform.GetChild(i).gameObject);
		}

		void CreateShot(string name)
		{
			// If already loaded, to not modify
			if (transform.Find(name) != null)
				return;

			// Father object
			GameObject shot = GameObject.CreatePrimitive(PrimitiveType.Plane);
			Destroy(shot.GetComponent<UnityEngine.Collider>());
			Destroy(shot.GetComponent<Renderer>());
			shot.name = name;
			shot.transform.parent = transform;

			// Shot start
			GameObject point1 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			Destroy(point1.GetComponent<UnityEngine.Collider>());
			point1.name = "Start";
			point1.transform.parent = shot.transform;
			point1.transform.localScale = new Vector3(1f, 1f, 1f) * Physics.ScaleToWorld(PhysicsConstants.PhysSkin*2);// * Globals.g_Scale;
																																		// Shot direction end
			GameObject point2 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			Destroy(point2.GetComponent<UnityEngine.Collider>());
			point2.name = "DirectionEnd";
			point2.transform.parent = shot.transform;
			point2.transform.localScale = new Vector3(1f, 1f, 1f) * Physics.ScaleToWorld(PhysicsConstants.PhysSkin*2);// * Globals.g_Scale;

			GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
			Destroy(line.GetComponent<UnityEngine.Collider>());
			line.name = "Direction";
			line.transform.parent = shot.transform;
			line.transform.localScale = new Vector3(0.005f, 1f, 0.005f);
		}

		void SetShotStart(string name, Vector3 p)
		{
			transform.Find(name).position = p;  // Sets the fathers position (start is the reference)
		}

		void SetShotEnd(string name, Vector3 p)
		{
			transform.Find(name + "/DirectionEnd").position = p;    // Sets the direction's end position

			// Set the shooting line
			Vector3 ps = transform.Find(name).position;
			Vector3 pe = p;
			Transform dir = transform.Find(name + "/Direction");
			float length = (pe - ps).magnitude;
			dir.position = (pe + ps) * 0.5f;
			dir.localScale = new Vector3(0.005f, length * 0.5f, 0.005f);// *Globals.g_Scale;
			dir.LookAt(pe);
			dir.Rotate(90.0f, 0.0f, 0.0f);

		}

		// Raycast from mouse pos
		bool RayCastMouse(ref Vector3 pos)
		{
			Camera cam = null;
			cam = Camera.main;
			if (cam == null)
				return false;

			var mouseOnScreenPos = Mouse.current.position.ReadValue();
			Ray ray = cam.ScreenPointToRay(mouseOnScreenPos);

			var p = _playfield.transform.position + _playfield.transform.up * Physics.ScaleToWorld(_playfield.TableHeight);
			_playfieldPlane.SetNormalAndPosition(_playfield.transform.up, p); // Need update as it is rotated for display at run


			if (_playfieldPlane.Raycast(ray, out var enter))
			{
				var playfieldPosWorld = ray.GetPoint(enter);

				pos = playfieldPosWorld + _playfieldPlane.normal * Physics.ScaleToWorld(PhysicsConstants.PhysSkin);
				// todo check playfield bounds
				return true;
			}

			return false;
		}

		float m_elasped = 0f;   //!< elapsed time since last click
		void Update()
		{
			if (Camera.main == null)// || _playfield == null)
				return;

			if (Mouse.current.middleButton.wasPressedThisFrame || _mouseDown)
			{
				Vector3 pos = new Vector3(); // hit point
				if (!_mouseDown)   // just clicked (set start)
				{
					m_elasped = 0f;
					if (RayCastMouse(ref pos))
						SetShotStart("Current", pos);

					if (!_activated)
						SetVisible(true, transform.Find("Current").gameObject);
				}
				else
				{
					m_elasped += Time.deltaTime;
					if (RayCastMouse(ref pos))
					{
						SetShotEnd("Current", pos);
						//						SetShotForce ("Current",50f);//1f/m_elasped);
					}
				}
				_mouseDown = true;
			}

			if (Mouse.current.middleButton.wasReleasedThisFrame)
			{
				if (_mouseDown == true)    // just released
				{
					LaunchShot("Current");
					if (!_activated)
						SetVisible(false, transform.Find("Current").gameObject);
				}
				_mouseDown = false;
			}

			if (Keyboard.current.spaceKey.wasPressedThisFrame)
				LaunchShot("Current");
		}

		public static float SignedAngle(Vector3 from, Vector3 to, Vector3 normal)
		{
			// angle in [0,180]
			float angle = Vector3.Angle(from, to);
			float sign = Mathf.Sign(Vector3.Dot(normal, Vector3.Cross(from, to)));
			return angle * sign;
		}

		Entity FindNearest(float2 physicPos)
		{
			Entity ret = Entity.Null;
			var ballEntities = _ballEntityQuery.ToEntityArray(Allocator.Temp);
			var nearestDistance = float.PositiveInfinity;
			BallData nearestBall = default;
			foreach (var ballEntity in ballEntities)
			{
				var ballData = _entityManager.GetComponentData<BallData>(ballEntity);
				if (ballData.IsFrozen)
				{
					continue;
				}
				var distance = math.distance(physicPos, ballData.Position.xy);
				if (distance < nearestDistance)
				{
					nearestDistance = distance;
					nearestBall = ballData;
					ret = ballEntity;
				}
			}

			return ret;

		}

		void LaunchShot(string name)
		{
			var ballManager = GameObject.FindObjectOfType<Player>()?.BallManager; // Need to update as it is not created at start (TODO: change script order but not important as one shot)
			if (ballManager == null)
				return;
			Vector3 ps = _wtl.MultiplyPoint(transform.Find(name).position);
			Vector3 pe = _wtl.MultiplyPoint(transform.Find(name + "/DirectionEnd").position);

			var dir = pe - ps;
			float mag = dir.magnitude; // To reuse magnitude for force

			float angle = mag>Mathf.Epsilon ? Vector3.SignedAngle(Vector3.up, dir/mag, Vector3.forward) + 180F : 0F;

			if (!Keyboard.current.leftCtrlKey.isPressed)
			{
				var nearest = FindNearest(new float2(ps.x, ps.y));
				if (nearest != Entity.Null)
					ballManager.DestroyEntity(nearest);
			}
			ballManager.CreateBall(new DebugBallCreator(ps.x, ps.y, PhysicsConstants.PhysSkin, angle, mag * ForceMultiplier));

		}

	}

}
