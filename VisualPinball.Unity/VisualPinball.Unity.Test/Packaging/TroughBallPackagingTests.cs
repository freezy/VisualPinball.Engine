// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// https://github.com/freezy/VisualPinball.Engine
//
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VisualPinball.Unity.Editor;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Test
{
	/// <summary>
	/// The balls a trough creates come from prefabs outside the table. The package carries copies of
	/// them, as inactive templates the trough points to, so a packaged table keeps its balls.
	/// </summary>
	public class TroughBallPackagingTests
	{
		[UnityTest]
		public IEnumerator TroughBallsAndCabinetMarkersSurviveARoundTrip()
		{
			var packagePath = Path.Combine(Path.GetTempPath(), $"vpe-trough-balls-{Guid.NewGuid():N}.vpe");
			GameObject source = null;
			GameObject prefab = null;
			GameObject imported = null;
			Mesh mesh = null;
			try {
				mesh = new Mesh { name = "Test ball mesh" };
				mesh.vertices = new[] { new Vector3(-0.01f, 0, 0), new Vector3(0.01f, 0, 0), new Vector3(0, 0.02f, 0) };
				mesh.triangles = new[] { 0, 1, 2 };
				mesh.RecalculateNormals();

				// stands in for a ball prefab asset: outside the table
				prefab = new GameObject("Shiny Ball");
				prefab.AddComponent<MeshFilter>().sharedMesh = mesh;
				prefab.AddComponent<MeshRenderer>();
				var prefabBall = prefab.AddComponent<BallComponent>();
				prefabBall.Radius = 27f;
				prefabBall.Mass = 1.2f;

				source = new GameObject("Table");
				source.AddComponent<TableComponent>();
				var troughGo = new GameObject("Trough");
				troughGo.transform.SetParent(source.transform, false);
				var trough = troughGo.AddComponent<TroughComponent>();
				trough.BallPrefabs = new[] { prefab, null, prefab };
				var cabinetGo = new GameObject("Cabinet");
				cabinetGo.transform.SetParent(source.transform, false);
				cabinetGo.AddComponent<CabinetComponent>();

				new PackageWriter(source).WritePackageSync(packagePath);

				// the export puts the author's table back as it was
				Assert.That(trough.BallPrefabs, Is.EqualTo(new[] { prefab, null, prefab }));
				Assert.That(source.transform.Find("Ball Templates"), Is.Null);

				var importTask = new RuntimePackageReader(packagePath).ImportIntoScene();
				while (!importTask.IsCompleted) { yield return null; }
				if (importTask.IsFaulted) { throw importTask.Exception!.GetBaseException(); }
				imported = importTask.Result;

				var restored = imported.GetComponentInChildren<TroughComponent>(true);
				Assert.That(restored, Is.Not.Null);
				Assert.That(restored.BallPrefabs, Has.Length.EqualTo(3));
				var template = restored.BallPrefabs[0];
				Assert.That(template, Is.Not.Null, "the trough must point to the packaged ball");
				Assert.That(restored.BallPrefabs[1], Is.Null, "an empty slot stays empty (the default ball)");
				Assert.That(restored.BallPrefabs[2], Is.SameAs(template), "one template per prefab");
				Assert.That(template.transform.IsChildOf(imported.transform), Is.True);
				Assert.That(template.activeSelf, Is.False, "a template must not be a ball in play");
				var ball = template.GetComponent<BallComponent>();
				Assert.That(ball, Is.Not.Null);
				Assert.That(ball.Radius, Is.EqualTo(27f));
				Assert.That(ball.Mass, Is.EqualTo(1.2f));
				Assert.That(template.GetComponent<MeshFilter>()?.sharedMesh, Is.Not.Null, "the ball's mesh comes with it");

				Assert.That(imported.GetComponentsInChildren<CabinetComponent>(true), Has.Length.EqualTo(1),
					"the cabinet marker is packaged so the player can hide the cabinet");
			} finally {
				if (source) { Object.DestroyImmediate(source); }
				if (prefab) { Object.DestroyImmediate(prefab); }
				if (imported) { Object.DestroyImmediate(imported); }
				if (mesh) { Object.DestroyImmediate(mesh); }
				File.Delete(packagePath);
			}
		}
	}
}
