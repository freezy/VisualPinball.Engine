using System;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Components;

namespace VisualPinball.Unity.Game
{
	public class TablePlayer : MonoBehaviour
	{
		public Table Table { get; private set; }
		public Player Player { get; private set; }

		private Transform _leftFlipper;
		private Transform _rightFlipper;

		private void Start()
		{
			var tableComponent = gameObject.GetComponent<VisualPinballTable>();
			Table = tableComponent.CreateTable();
			Player = new Player(Table).Init();

			_leftFlipper  = transform.Find("Flippers/LeftFlipper");
			_rightFlipper  = transform.Find("Flippers/RightFlipper");
		}

		private void Update()
		{
			// all of this is hacky and only serves as proof of concept.
			// flippers will obviously be handled via script later.
			if (Table.Flippers.ContainsKey("LeftFlipper")) {
				if (Input.GetKeyDown("left shift")) {
					Table.Flippers["LeftFlipper"].RotateToEnd();
				}
				if (Input.GetKeyUp("left shift")) {
					Table.Flippers["LeftFlipper"].RotateToStart();
				}
			}

			if (Table.Flippers.ContainsKey("RightFlipper")) {
				if (Input.GetKeyDown("right shift")) {
					Table.Flippers["RightFlipper"].RotateToEnd();
				}
				if (Input.GetKeyUp("right shift")) {
					Table.Flippers["RightFlipper"].RotateToStart();
				}
			}

			Player.UpdatePhysics();

			if (Table.Flippers.ContainsKey("LeftFlipper")) {
				var rotL = _leftFlipper.transform.localRotation.eulerAngles;
				rotL.z = MathF.RadToDeg(Table.Flippers["LeftFlipper"].State.Angle);
				_leftFlipper.transform.localRotation = Quaternion.Euler(rotL);
			}
			if (Table.Flippers.ContainsKey("RightFlipper")) {
				var rotR = _rightFlipper.transform.localRotation.eulerAngles;
				rotR.z = MathF.RadToDeg(Table.Flippers["RightFlipper"].State.Angle);
				_rightFlipper.transform.localRotation = Quaternion.Euler(rotR);
			}
		}
	}
}
