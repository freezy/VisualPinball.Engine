// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using NUnit.Framework;
using UnityEngine;
using VisualPinball.Unity.Editor;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Test
{
	public class InsertLampPresetTests
	{
		[Test]
		public void ShouldShipAnIncandescentAndAnLedLamp()
		{
			var lamps = InsertLampPresets.Find();
			Assert.That(lamps, Has.Some.Matches<InsertLampPreset>(preset => preset.LampName == "Incandescent" && preset.Color.b < preset.Color.r && preset.FadeSpeedDown > 0f));
			Assert.That(lamps, Has.Some.Matches<InsertLampPreset>(preset => preset.LampName == "Cool White LED" && preset.Color.b > preset.Color.r && preset.FadeSpeedDown == 0f));
			Assert.That(lamps, Has.Some.Matches<InsertLampPreset>(preset => preset.LampName == "RGB LED" && preset.Color == Color.white));
		}

		[Test]
		public void ShouldGiveEveryBulbThePresetAndNoticeEdits()
		{
			var preset = ScriptableObject.CreateInstance<InsertLampPreset>();
			var go = new GameObject("Insert");
			try {
				preset.LampName = "Test Lamp";
				preset.Color = new Color(1f, 0.7f, 0.4f);
				preset.FadeSpeedUp = 0.1f;
				preset.FadeSpeedDown = 0.3f;
				go.AddComponent<InsertComponent>();
				var light = go.AddComponent<InsertLightComponent>();
				light.ExtraBulbs[0].Color = Color.blue;

				InsertLampPresets.Apply(preset, new[] { light });

				Assert.That(light.Lamp, Is.EqualTo("Test Lamp"));
				Assert.That(light.Color, Is.EqualTo(preset.Color));
				Assert.That(light.ExtraBulbs[0].Color, Is.EqualTo(preset.Color));
				Assert.That(light.FadeSpeedUp, Is.EqualTo(0.1f));
				Assert.That(light.FadeSpeedDown, Is.EqualTo(0.3f));
				Assert.That(InsertLampPresets.Differs(light, preset), Is.False);

				light.Color = Color.white;
				Assert.That(InsertLampPresets.Differs(light, preset), Is.True);

				InsertLampPresets.Apply(null, new[] { light });
				Assert.That(light.Lamp, Is.Empty);
				Assert.That(light.Color, Is.EqualTo(Color.white), "unfollowing keeps the bulb as it is");
			} finally {
				var filter = go.GetComponent<MeshFilter>();
				if (filter != null && filter.sharedMesh != null) { Object.DestroyImmediate(filter.sharedMesh); }
				Object.DestroyImmediate(go);
				Object.DestroyImmediate(preset);
			}
		}
	}
}
