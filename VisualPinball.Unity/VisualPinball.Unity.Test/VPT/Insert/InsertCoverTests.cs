// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Test
{
	public class InsertCoverTests
	{
		[Test]
		public void ShouldWearTheCoverPerInsert()
		{
			var shader = Shader.Find("Visual Pinball/Insert Cover");
			if (shader == null) { Assert.Ignore("Requires the HDRP insert cover shader."); }
			var cover = new Material(shader);
			var go = new GameObject("Insert");
			try {
				var insert = go.AddComponent<InsertComponent>();
				insert.SetCoverMaterial(cover);
				var renderer = go.GetComponent<MeshRenderer>();
				var block = new MaterialPropertyBlock();

				insert.Roughness = 0.6f;
				renderer.GetPropertyBlock(block, 1);
				Assert.That(block.GetFloat("_CoverRoughness"), Is.EqualTo(0.6f).Within(1e-5f));
				Assert.That(block.GetFloat("_CoverThickness"), Is.GreaterThan(0f), "the slab's thickness comes along");

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
				Object.DestroyImmediate(cover);
			}
		}
	}
}
