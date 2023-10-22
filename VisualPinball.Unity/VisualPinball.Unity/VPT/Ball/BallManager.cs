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

		public void CreateBall(IBallCreationPosition ballCreator, float radius = 25f, float mass = 1f)
		{
			CreateBall(ballCreator, radius, mass, 0);
		}

		public void CreateBall(IBallCreationPosition ballCreator, float radius, float mass, int kickerId, GameObject ballPrefab = null)
		{
			var localPos = ballCreator.GetBallCreationPosition().ToUnityFloat3();
			localPos.z += radius;

			var ballId = NumBallsCreated++;
			if (!ballPrefab) {
				ballPrefab = RenderPipeline.Current.BallConverter.CreateDefaultBall();
			}
			var ballGo = Object.Instantiate(ballPrefab, _parent);
			var ballComp = ballGo.GetComponent<BallComponent>();
			ballGo.name = $"Ball {ballId}";
			ballGo.transform.localScale = Physics.ScaleToWorld(new Vector3(radius, radius, radius) * 2f);
			ballGo.transform.localPosition = localPos.TranslateToWorld();
			ballComp.Radius = radius;
			ballComp.Mass = mass;
			ballComp.Velocity = ballCreator.GetBallCreationVelocity().ToUnityFloat3();
			ballComp.IsFrozen = kickerId != 0;

			// register ball
			_physicsEngine.Register(ballComp);
			_player.BallCreated(ballGo.GetInstanceID(), ballGo);

			// handle inside-kicker creation
			if (kickerId != 0) {
				ref var kickerData = ref _physicsEngine.KickerState(kickerId);
				if (!kickerData.Static.FallThrough) {
					_physicsEngine.SetBallInsideOf(ballComp.Id, kickerId);
					kickerData.Collision.BallId = ballComp.Id;
					kickerData.Collision.LastCapturedBallId = ballComp.Id;
				}
			}
		}

		public void DestroyBall(int ballId)
		{
			var ballTransform = _physicsEngine.UnregisterBall(ballId);
			_player.BallDestroyed(ballId, ballTransform.gameObject);

			// destroy game object
			Object.DestroyImmediate(ballTransform.gameObject);
		}
	}
}
