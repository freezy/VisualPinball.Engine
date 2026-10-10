// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Test
{
	public class InsertBakeDataTests
	{
		private string _path;

		[SetUp]
		public void SetUp() => _path = $"Assets/InsertBakeDataTest-{Guid.NewGuid():N}.asset";

		[TearDown]
		public void TearDown() => AssetDatabase.DeleteAsset(_path);

		[Test]
		public void ShouldSaveTheCornersCompactlyAndReadThemBack()
		{
			const int corners = 3000;
			var uvs = Enumerable.Range(0, corners).Select(i => new Vector2((i % 97) / 97f, (i % 89) / 89f * 0.99f)).ToArray();
			var tangents = Enumerable.Range(0, corners).Select(i => {
				var t = new Vector3(Mathf.Cos(i % 97), Mathf.Sin(i % 97), 0.3f).normalized;
				return new Vector4(t.x, t.y, t.z, i % 2 == 0 ? 1f : -1f);
			}).ToArray();
			var bake = ScriptableObject.CreateInstance<InsertBakeData>();
			bake.Uvs = (Vector2[])uvs.Clone();
			bake.Tangents = (Vector4[])tangents.Clone();

			AssetDatabase.CreateAsset(bake, _path);
			AssetDatabase.SaveAssets();
			var text = File.ReadAllText(_path);
			Assert.That(text.Split('\n').Length, Is.LessThan(40), "a line per corner");
			Assert.That(text, Does.Not.Contain("- {x:"));

			// What the insert wears is what was saved, before and after a read.
			var read = Object.Instantiate(bake);
			try {
				Assert.That(read.Uvs, Is.EqualTo(bake.Uvs));
				Assert.That(read.Tangents, Is.EqualTo(bake.Tangents));
				for (var i = 0; i < corners; i++) {
					Assert.That(Vector2.Distance(read.Uvs[i], uvs[i]), Is.LessThanOrEqualTo(1f / 65536f), $"uv {i}");
					Assert.That(Vector3.Distance(read.Tangents[i], tangents[i]), Is.LessThanOrEqualTo(1.5f / 127f), $"tangent {i}");
					Assert.That(read.Tangents[i].w, Is.EqualTo(tangents[i].w), $"sign {i}");
				}
			} finally {
				Object.DestroyImmediate(read);
			}
		}

		[TestCase(65536, TestName = "ShouldReloadEveryCornerFromDiskWithSixteenBitIndices")]
		[TestCase(65537, TestName = "ShouldReloadEveryCornerFromDiskWithThirtyTwoBitIndices")]
		public void ShouldReloadEveryCornerFromDisk(int distinct)
		{
			// Distinct pairs on a grid the stored precision holds exactly, then the first thousand again.
			var pairs = Enumerable.Range(0, distinct).Select(i => (
				uv: new Vector2((i % 256) / 256f, (i / 256 % 256) / 256f),
				tangent: new Vector4(i < 65536 ? 1f : 0f, i < 65536 ? 0f : 1f, 0f, 1f)
			)).ToList();
			pairs.AddRange(pairs.Take(1000));
			var bake = ScriptableObject.CreateInstance<InsertBakeData>();
			bake.Uvs = pairs.Select(p => p.uv).ToArray();
			bake.Tangents = pairs.Select(p => p.tangent).ToArray();
			var uvs = (Vector2[])bake.Uvs.Clone();
			var tangents = (Vector4[])bake.Tangents.Clone();

			AssetDatabase.CreateAsset(bake, _path);
			AssetDatabase.SaveAssets();
			Assert.That(File.ReadAllText(_path), Does.Contain($"_cornerIndices: {(distinct > 65536 ? "00000000" : "0000")}"));

			// Forget them in memory and unload the asset, so that only a read from disk brings them back.
			bake.Uvs = null;
			bake.Tangents = null;
			Resources.UnloadAsset(bake);
			var read = AssetDatabase.LoadAssetAtPath<InsertBakeData>(_path);

			Assert.That(read.Uvs, Is.EqualTo(uvs));
			Assert.That(read.Tangents, Is.EqualTo(tangents));
		}

		[Test]
		public void ShouldReadADamagedBakeAsNone()
		{
			// Three corners, the last pointing past the only record.
			File.WriteAllText(_path, string.Join("\n",
				"%YAML 1.1",
				"%TAG !u! tag:unity3d.com,2011:",
				"--- !u!114 &11400000",
				"MonoBehaviour:",
				"  m_ObjectHideFlags: 0",
				"  m_Enabled: 1",
				"  m_Script: {fileID: 11500000, guid: 952b3dd998a9d2b47bbb3aaa4fce0c8f, type: 3}",
				"  m_Name: Damaged Bake",
				"  Hash: 0123456789abcdef0123456789abcdef",
				"  _cornerCount: 3",
				"  _cornerIndices: 000000000500",
				"  _cornerData: 004000c07f00007f",
				""));
			AssetDatabase.ImportAsset(_path, ImportAssetOptions.ForceUpdate);

			var bake = AssetDatabase.LoadAssetAtPath<InsertBakeData>(_path);
			Assert.That(bake, Is.Not.Null);
			Assert.That(bake.Uvs, Is.Null);
			Assert.That(bake.Tangents, Is.Null);
		}

		[Test]
		public void ShouldReadABakeSavedWithALineForEveryCorner()
		{
			File.WriteAllText(_path, string.Join("\n",
				"%YAML 1.1",
				"%TAG !u! tag:unity3d.com,2011:",
				"--- !u!114 &11400000",
				"MonoBehaviour:",
				"  m_ObjectHideFlags: 0",
				"  m_Enabled: 1",
				"  m_Script: {fileID: 11500000, guid: 952b3dd998a9d2b47bbb3aaa4fce0c8f, type: 3}",
				"  m_Name: Old Bake",
				"  Hash: 0123456789abcdef0123456789abcdef",
				"  Uvs:",
				"  - {x: 0.25, y: 0.75}",
				"  - {x: 0.5, y: 0.125}",
				"  - {x: 0.25, y: 0.75}",
				"  Tangents:",
				"  - {x: 1, y: 0, z: 0, w: 1}",
				"  - {x: 0, y: 1, z: 0, w: -1}",
				"  - {x: 1, y: 0, z: 0, w: 1}",
				""));
			AssetDatabase.ImportAsset(_path, ImportAssetOptions.ForceUpdate);

			var bake = AssetDatabase.LoadAssetAtPath<InsertBakeData>(_path);
			Assert.That(bake.Uvs, Is.EqualTo(new[] { new Vector2(0.25f, 0.75f), new Vector2(0.5f, 0.125f), new Vector2(0.25f, 0.75f) }));
			Assert.That(bake.Tangents, Is.EqualTo(new[] { new Vector4(1f, 0f, 0f, 1f), new Vector4(0f, 1f, 0f, -1f), new Vector4(1f, 0f, 0f, 1f) }));

			// Saved again, it takes the compact form.
			EditorUtility.SetDirty(bake);
			AssetDatabase.SaveAssets();
			Assert.That(File.ReadAllText(_path), Does.Not.Contain("- {x:"));
			Assert.That(bake.Uvs[1], Is.EqualTo(new Vector2(0.5f, 0.125f)));
		}
	}
}
