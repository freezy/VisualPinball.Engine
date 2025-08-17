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

using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Game;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity
{
	/// <summary>
	/// A singleton class that handles ball creation and destruction.
	/// </summary>
	[Serializable]
	public class BallManager
	{
		public int NumBallsCreated { get; private set; }

		private readonly PhysicsEngine _physicsEngine;
		private readonly Player _player;
		private readonly Transform _parent;

		public BallManager(PhysicsEngine physicsEngine, Player player, Transform parent)
		{
			_physicsEngine = physicsEngine;
			_player = player;
			_parent = parent;
		}

		public int CreateBall(IBallCreationPosition ballCreator, float radius = 25f, float mass = 1f, GameObject ballPrefab = null)
		{
			var localPos = ballCreator.GetBallCreationPosition().ToUnityFloat3();
			localPos.z += radius;

			if (!ballPrefab) {
				ballPrefab = RenderPipeline.Current.BallConverter.CreateDefaultBall();
			}
			var ballGo = Object.Instantiate(ballPrefab, _parent);
			var ballComp = ballGo.GetComponent<BallComponent>();
			ballGo.name = $"Ball {NumBallsCreated++}";
			ballGo.transform.localScale = Physics.ScaleToWorld(new Vector3(radius, radius, radius) * 2f);
			ballGo.transform.localPosition = localPos.TranslateToWorld();
			ballComp.Radius = radius;
			ballComp.Mass = mass;
			ballComp.Velocity = ballCreator.GetBallCreationVelocity().ToUnityFloat3();
			ballComp.IsFrozen = false;

			// register ball
			_physicsEngine.Register(ballComp);
			_player.BallCreated(ballGo.GetInstanceID(), ballGo);

			return ballComp.Id;
		}

		public void DestroyBall(int ballId)
		{
			var ballComponent = _physicsEngine.UnregisterBall(ballId);
			_player.BallDestroyed(ballId, ballComponent.gameObject);

			// destroy game object
			Object.DestroyImmediate(ballComponent.gameObject);
		}

		public bool FindBall(out BallState ball)
		{
			var ballFound = false;
			using var enumerator = _physicsEngine.Balls.GetEnumerator();
			ball = default;
			while (enumerator.MoveNext()) {
				ball = enumerator.Current.Value;
				ballFound = true;
				break;
			}
			return ballFound;
		}

		public bool FindNearest(float2 fromPosition, out BallState nearestBall)
		{
			var nearestDistance = float.PositiveInfinity;
			nearestBall = default;
			var ballFound = false;

			using var enumerator = _physicsEngine.Balls.GetEnumerator();
			while (enumerator.MoveNext()) {
				var ball = enumerator.Current.Value;

				if (ball.IsFrozen) {
					continue;
				}
				var distance = math.distance(fromPosition, ball.Position.xy);
				if (distance < nearestDistance) {
					nearestDistance = distance;
					nearestBall = ball;
					ballFound = true;
				}
			}
			return ballFound;
		}
	}
}
