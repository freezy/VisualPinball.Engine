// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace VisualPinball.Unity.Test
{
	public class SpringHingePlayModeFixtureTests
	{
		[UnityTearDown]
		public IEnumerator LeavePlayModeAfterEachTest()
		{
			if (Application.isPlaying) {
				yield return new ExitPlayMode();
			}
		}

		[UnityTest]
		public IEnumerator RealPlayerPhysicsRunsShotMagnetReleaseAndReset()
		{
			Assert.That(Application.isPlaying, Is.False);
			yield return new EnterPlayMode();
			Assert.That(Application.isPlaying, Is.True);
			var fixture = CreateFixture();
			try {
				fixture.Root.SetActive(true);
				yield return null;
				yield return null;

				Assert.That(fixture.PhysicsEngine.IsInitialized, Is.True);
				Assert.That(fixture.Hinge.SpringHingeApi, Is.Not.Null);
				Assert.That(fixture.Magnet.MagnetApi, Is.Not.Null);

				fixture.Magnet.MagnetApi.IsEnabled = true;
				var ballId = fixture.Player.BallManager.CreateBall(
					new DebugBallCreator(400f, 900f, 0f, 180f, 18f),
					25f, 1f, fixture.BallPrefab);
				yield return null;

				Assert.That(ballId, Is.Not.Zero);
				Assert.That(fixture.Player.BallManager.NumBallsCreated, Is.EqualTo(1));
				Assert.That(fixture.Magnet.MagnetApi.IsEnabled, Is.True);

				fixture.Magnet.MagnetApi.ReleaseBall();
				fixture.Magnet.MagnetApi.IsEnabled = false;
				fixture.Hinge.SpringHingeApi.Reset(fixture.Hinge.InitialAngle);
				fixture.Player.BallManager.DestroyBall(ballId);
				yield return null;

				Assert.That(fixture.Magnet.MagnetApi.IsEnabled, Is.False);
			} finally {
				Object.DestroyImmediate(fixture.Root);
			}
			yield return new ExitPlayMode();
			Assert.That(Application.isPlaying, Is.False);
		}

		private static Fixture CreateFixture()
		{
			var root = new GameObject("Spring Hinge Play Mode Fixture");
			root.SetActive(false);
			root.AddComponent<TableComponent>();
			root.AddComponent<DefaultGamelogicEngine>();
			var player = root.AddComponent<Player>();
			var physicsEngine = root.AddComponent<PhysicsEngine>();

			var playfieldObject = new GameObject("Playfield");
			playfieldObject.transform.SetParent(root.transform, false);
			var playfield = playfieldObject.AddComponent<PlayfieldComponent>();
			playfield.GlassHeight = 500f;
			playfield.RenderSlope = 0f;

			var hingeObject = new GameObject("Spring Hinge");
			hingeObject.transform.SetParent(playfieldObject.transform, false);
			hingeObject.transform.localPosition = new Vector3(-0.4f, 0f, 0.9f);
			var hinge = hingeObject.AddComponent<SpringHingeComponent>();
			hinge.HingeAxis = Vector3.forward;
			hinge.MinimumAngle = -20f;
			hinge.MaximumAngle = 20f;
			hinge.OverrideInertia = false;
			var proxy = hingeObject.AddComponent<SpringHingeColliderComponent>();
			proxy.LocalCentre = new Vector3(0f, 50f, 0f);
			proxy.HalfExtents = new Vector3(25f, 50f, 10f);

			var animation = hingeObject.AddComponent<SpringHingeAnimationComponent>();
			animation._emitter = hinge;
			animation.RotationAxis = Vector3.forward;

			var magnetObject = new GameObject("Owned Magnet");
			magnetObject.transform.SetParent(hingeObject.transform, false);
			magnetObject.transform.localPosition = new Vector3(0f, 0.05f, 0f);
			var magnet = magnetObject.AddComponent<MagnetComponent>();
			magnet.MagnetType = MagnetType.Spatial;
			magnet.ForceProfile = MagnetForceProfile.Physical;
			magnet.CoupleToParentHinge = true;
			magnet.GrabBall = true;
			magnet.IsEnabledOnStart = false;
			magnet.HeldBallCentreOffset = new Vector3(0f, 25f, 0f);
			magnet.HoldStiffness = 2f;
			magnet.HoldDamping = 2f;
			magnet.MaxHoldForce = 10f;

			var prefabHolder = new GameObject("Test Ball Prefab Holder");
			prefabHolder.transform.SetParent(root.transform, false);
			prefabHolder.SetActive(false);
			var ballPrefab = new GameObject("Test Ball Prefab");
			ballPrefab.transform.SetParent(prefabHolder.transform, false);
			ballPrefab.AddComponent<BallComponent>();

			return new Fixture(root, player, physicsEngine, hinge, magnet, ballPrefab);
		}

		private readonly struct Fixture
		{
			internal readonly GameObject Root;
			internal readonly Player Player;
			internal readonly PhysicsEngine PhysicsEngine;
			internal readonly SpringHingeComponent Hinge;
			internal readonly MagnetComponent Magnet;
			internal readonly GameObject BallPrefab;

			internal Fixture(GameObject root, Player player, PhysicsEngine physicsEngine,
				SpringHingeComponent hinge, MagnetComponent magnet, GameObject ballPrefab)
			{
				Root = root;
				Player = player;
				PhysicsEngine = physicsEngine;
				Hinge = hinge;
				Magnet = magnet;
				BallPrefab = ballPrefab;
			}
		}
	}
}
