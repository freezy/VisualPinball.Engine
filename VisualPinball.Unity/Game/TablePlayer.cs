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

		private Table _table;
		private Player _player;

		private Transform _leftFlipper;
		private Transform _rightFlipper;

		private void Start()
		{
			var tableComponent = gameObject.GetComponent<VisualPinballTable>();
			_table = tableComponent.CreateTable();
			_player = new Player(_table).Init();

			_leftFlipper  = transform.Find("Flippers/LeftFlipper");
			_rightFlipper  = transform.Find("Flippers/RightFlipper");
		}

		private void Update()
		{
			if (Input.GetKeyDown("left shift")) {
				_table.Flippers["LeftFlipper"].RotateToEnd();
			}
			if (Input.GetKeyUp("left shift")) {
				_table.Flippers["LeftFlipper"].RotateToStart();
			}

			if (Input.GetKeyDown("right shift")) {
				_table.Flippers["RightFlipper"].RotateToEnd();
			}
			if (Input.GetKeyUp("right shift")) {
				_table.Flippers["RightFlipper"].RotateToStart();
			}

			_player.UpdatePhysics();

			var rotL = _leftFlipper.transform.localRotation.eulerAngles;
			var rotR = _rightFlipper.transform.localRotation.eulerAngles;
			rotL.z = MathF.RadToDeg(_table.Flippers["LeftFlipper"].State.Angle);
			rotR.z = MathF.RadToDeg(_table.Flippers["RightFlipper"].State.Angle);
			_leftFlipper.transform.localRotation = Quaternion.Euler(rotL);
			_rightFlipper.transform.localRotation = Quaternion.Euler(rotR);
		}
	}
}
