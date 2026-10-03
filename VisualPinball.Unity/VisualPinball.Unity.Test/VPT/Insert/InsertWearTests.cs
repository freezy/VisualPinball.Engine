// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Test
{
	public class InsertWearTests
	{
		[Test]
		public void ShouldWearTheBodyPerInsert()
		{
			var go = new GameObject("Insert");
			try {
				var insert = go.AddComponent<InsertComponent>();
				var renderer = go.GetComponent<MeshRenderer>();
				var block = new MaterialPropertyBlock();

				insert.Roughness = 0.6f;
				renderer.GetPropertyBlock(block, 0);
				Assert.That(block.GetFloat("_InsertRoughness"), Is.EqualTo(0.6f).Within(1e-5f), "the body's block, which the insert light shares, carries the wear");

				insert.Roughness = 2f;
				Assert.That(insert.Roughness, Is.EqualTo(1f), "worn is as far as it goes");

				var packed = InsertPackable.Pack(insert);
				insert.Roughness = 0f;
				InsertPackable.Unpack(packed, insert);
				Assert.That(insert.Roughness, Is.EqualTo(1f), "the wear travels with the table");
			} finally {
				var filter = go.GetComponent<MeshFilter>();
				if (filter != null && filter.sharedMesh != null) { Object.DestroyImmediate(filter.sharedMesh); }
				Object.DestroyImmediate(go);
			}
		}
	}
}
