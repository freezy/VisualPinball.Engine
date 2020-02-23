using System;
using System.Collections.Generic;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Components;
using Math = VisualPinball.Unity.Extensions.Math;

namespace VisualPinball.Unity.Game
{
	public class TablePlayer : MonoBehaviour
	{
		public Table Table { get; private set; }
		public Player Player { get; private set; }

		public Material ballMaterial;

		private GameObject _ballGroup;

		private Transform _leftFlipper;
		private Transform _rightFlipper;
		private Dictionary<string, GameObject> _balls = new Dictionary<string,GameObject>();

		private void Start()
		{
			var tableComponent = gameObject.GetComponent<VisualPinballTable>();
			Table = tableComponent.CreateTable();
			Player = new Player(Table).Init();

			Player.BallCreated += OnBallCreated;

			// create ball parent
			_ballGroup = new GameObject("Balls");
			_ballGroup.transform.parent = transform;
			_ballGroup.transform.localPosition = Vector3.zero;
			_ballGroup.transform.localRotation = Quaternion.identity;
			_ballGroup.transform.localScale = Vector3.one;

			_leftFlipper = transform.Find("Flippers/LeftFlipper");
			_rightFlipper = transform.Find("Flippers/RightFlipper");
		}

		private void OnBallCreated(object sender, BallCreationArgs e)
		{
			var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			ball.name = e.Name;
			ball.transform.parent = _ballGroup.transform;
			ball.transform.localScale = new Vector3(e.Radius, e.Radius, e.Radius);
			ball.transform.localPosition = new Vector3(e.Position.X, e.Position.Y, e.Position.Z);

			if (ballMaterial != null) {
				ball.GetComponent<Renderer>().material = ballMaterial;
			}

			_balls[ball.name] = ball;
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

			foreach (var ball in Player.Balls) {
				if (_balls.ContainsKey(ball.Name)) {
					var b = _balls[ball.Name];
					var m = new Matrix4x4(
						new Vector4(ball.State.Orientation.Matrix[0][0], ball.State.Orientation.Matrix[1][0], ball.State.Orientation.Matrix[2][0], 0.0f),
						new Vector4(ball.State.Orientation.Matrix[0][1], ball.State.Orientation.Matrix[1][1], ball.State.Orientation.Matrix[2][1], 0.0f),
						new Vector4(ball.State.Orientation.Matrix[0][2], ball.State.Orientation.Matrix[1][2], ball.State.Orientation.Matrix[2][2], 0.0f),
						new Vector4(0, 0, 0, 1)
					);
					b.transform.localPosition = Math.ToUnityVector3(ball.State.Pos);
					b.transform.localRotation = Quaternion.LookRotation(
						new Vector3(m.m02, m.m12, m.m22),
						new Vector3(m.m01, m.m11, m.m21)
					);
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
