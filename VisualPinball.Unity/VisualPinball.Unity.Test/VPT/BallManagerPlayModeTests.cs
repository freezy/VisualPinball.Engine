// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace VisualPinball.Unity.Test
{
	public class BallManagerPlayModeTests
	{
		private const string BallMeshPath = "Packages/org.visualpinball.engine.unity/VisualPinball.Unity/Assets/Art/Meshes/Ball.fbx";
		private const string DefaultBallPath = "Packages/org.visualpinball.engine.unity/VisualPinball.Unity/Assets/Resources/Prefabs/DefaultBall.prefab";

		[UnityTearDown]
		public IEnumerator LeavePlayModeAfterEachTest()
		{
			if (Application.isPlaying) {
				yield return new ExitPlayMode();
			}
		}

		[Test]
		public void DefaultBallUsesCorrectlySizedUvMesh()
		{
			var uvMesh = AssetDatabase.LoadAllAssetsAtPath(BallMeshPath)
				.OfType<Mesh>()
				.Single(mesh => mesh.name == "Ball UV");
			var defaultBall = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultBallPath);

			Assert.That(defaultBall, Is.Not.Null);
			Assert.That(defaultBall.transform.localScale, Is.EqualTo(Vector3.one));
			Assert.That(defaultBall.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(uvMesh));
			Assert.That(uvMesh.bounds.size.x, Is.EqualTo(Physics.ScaleToWorld(new Vector3(50f, 50f, 50f)).x).Within(0.000001f));
		}

		[UnityTest]
		public IEnumerator ScalesRelativeToAuthoredPrefabAndTracksItsSource()
		{
			Assert.That(Application.isPlaying, Is.False);
			yield return new EnterPlayMode();

			var root = new GameObject("Ball Manager Play Mode Fixture");
			root.SetActive(false);
			root.AddComponent<TableComponent>();
			root.AddComponent<DefaultGamelogicEngine>();
			var player = root.AddComponent<Player>();
			var physicsEngine = root.AddComponent<PhysicsEngine>();

			var playfieldObject = new GameObject("Playfield");
			playfieldObject.transform.SetParent(root.transform, false);
			playfieldObject.AddComponent<PlayfieldComponent>();

			var prefabHolder = new GameObject("Prefab Holder");
			prefabHolder.transform.SetParent(root.transform, false);
			prefabHolder.SetActive(false);
			var ballPrefab = new GameObject("Large Authored Ball");
			ballPrefab.transform.SetParent(prefabHolder.transform, false);
			ballPrefab.transform.localScale = Vector3.one * 2f;
			ballPrefab.AddComponent<BallComponent>().Radius = 50f;

			try {
				root.SetActive(true);
				yield return null;
				yield return null;

				var ballId = player.BallManager.CreateBall(new DebugBallCreator(0f, 0f), 25f, 1f, ballPrefab);
				Assert.That(physicsEngine.TryGetBall(ballId, out var ball), Is.True);
				Assert.That(ball.transform.localScale, Is.EqualTo(Vector3.one));
				Assert.That(ball.SourcePrefab, Is.SameAs(ballPrefab));
				Assert.That(ball.Radius, Is.EqualTo(25f));
			} finally {
				Object.DestroyImmediate(root);
			}

			yield return new ExitPlayMode();
		}

		[UnityTest]
		public IEnumerator TroughRequeuesThePrefabOfEachDrainedBall()
		{
			Assert.That(Application.isPlaying, Is.False);
			yield return new EnterPlayMode();

			var root = new GameObject("Trough Ball Queue Play Mode Fixture");
			root.SetActive(false);
			root.AddComponent<TableComponent>();
			root.AddComponent<DefaultGamelogicEngine>();
			var player = root.AddComponent<Player>();
			root.AddComponent<PhysicsEngine>();

			var playfieldObject = new GameObject("Playfield");
			playfieldObject.transform.SetParent(root.transform, false);
			playfieldObject.AddComponent<PlayfieldComponent>();

			var entryObject = new GameObject("Drain Trigger");
			entryObject.transform.SetParent(playfieldObject.transform, false);
			var entry = entryObject.AddComponent<TriggerComponent>();

			var exitObject = new GameObject("Exit Kicker");
			exitObject.transform.SetParent(playfieldObject.transform, false);
			var exit = exitObject.AddComponent<KickerComponent>();
			exit.Coils[0].Id = "exit";

			var prefabHolder = new GameObject("Prefab Holder");
			prefabHolder.transform.SetParent(root.transform, false);
			prefabHolder.SetActive(false);
			var firstPrefab = CreateBallPrefab(prefabHolder.transform, "First Prefab");
			var secondPrefab = CreateBallPrefab(prefabHolder.transform, "Second Prefab");
			var thirdPrefab = CreateBallPrefab(prefabHolder.transform, "Third Prefab");
			var fourthPrefab = CreateBallPrefab(prefabHolder.transform, "Fourth Prefab");

			var troughObject = new GameObject("Trough");
			troughObject.transform.SetParent(playfieldObject.transform, false);
			var trough = troughObject.AddComponent<TroughComponent>();
			trough.Type = VisualPinball.Engine.VPT.TroughType.ModernMech;
			trough.BallCount = 4;
			trough.SwitchCount = 3;
			trough.BallPrefabs = new[] { firstPrefab, secondPrefab, thirdPrefab, fourthPrefab };
			trough.PlayfieldEntrySwitch = entry;
			trough.PlayfieldEntrySwitchItem = TriggerComponent.SwitchItem;
			trough.PlayfieldExitKicker = exit;
			trough.PlayfieldExitKickerItem = "exit";

			try {
				root.SetActive(true);
				yield return null;
				yield return null;

				Assert.That(trough.TroughApi.UncountedStackBalls, Is.EqualTo(1));
				Assert.That(trough.TroughApi.EjectBall(), Is.True);
				trough.TroughApi.StackSwitch(0).SetSwitch(true);
				Assert.That(trough.TroughApi.EjectBall(), Is.True);
				var firstBall = playfieldObject.transform.Find("Ball 0").GetComponent<BallComponent>();
				var secondBall = playfieldObject.transform.Find("Ball 1").GetComponent<BallComponent>();
				Assert.That(firstBall.SourcePrefab, Is.SameAs(firstPrefab));
				Assert.That(secondBall.SourcePrefab, Is.SameAs(secondPrefab));

				// Drain the second ejected ball first, then the first one.
				((IApiHittable)entry.TriggerApi).OnHit(secondBall.Id);
				((IApiHittable)entry.TriggerApi).OnHit(firstBall.Id);

				trough.TroughApi.StackSwitch(0).SetSwitch(true);
				Assert.That(trough.TroughApi.EjectBall(), Is.True);
				trough.TroughApi.StackSwitch(0).SetSwitch(true);
				Assert.That(trough.TroughApi.EjectBall(), Is.True);
				trough.TroughApi.StackSwitch(0).SetSwitch(true);
				Assert.That(trough.TroughApi.EjectBall(), Is.True);
				trough.TroughApi.StackSwitch(0).SetSwitch(true);
				Assert.That(trough.TroughApi.EjectBall(), Is.True);
				Assert.That(playfieldObject.transform.Find("Ball 2").GetComponent<BallComponent>().SourcePrefab, Is.SameAs(thirdPrefab));
				Assert.That(playfieldObject.transform.Find("Ball 3").GetComponent<BallComponent>().SourcePrefab, Is.SameAs(fourthPrefab));
				Assert.That(playfieldObject.transform.Find("Ball 4").GetComponent<BallComponent>().SourcePrefab, Is.SameAs(secondPrefab));
				Assert.That(playfieldObject.transform.Find("Ball 5").GetComponent<BallComponent>().SourcePrefab, Is.SameAs(firstPrefab));
			} finally {
				Object.DestroyImmediate(root);
			}

			yield return new ExitPlayMode();
		}

		private static GameObject CreateBallPrefab(Transform parent, string name)
		{
			var prefab = new GameObject(name);
			prefab.transform.SetParent(parent, false);
			prefab.AddComponent<BallComponent>();
			return prefab;
		}
	}
}
