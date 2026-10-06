// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using NUnit.Framework;
using VisualPinball.Engine.Game.Engines;
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
			Assert.That(lamps, Has.Some.Matches<InsertLampPreset>(preset => preset.LampName == "Incandescent" && preset.Color.b < preset.Color.r && preset.FadeSpeedDown > 0f && preset.Output == 1f));
			Assert.That(lamps, Has.Some.Matches<InsertLampPreset>(preset => preset.LampName == "LED" && preset.Color == Color.white && preset.FadeSpeedDown == 0f && preset.Output > 1f));
		}

		[Test]
		public void ShouldLightTheWholeTableButTheFlashers()
		{
			var preset = ScriptableObject.CreateInstance<InsertLampPreset>();
			var table = new GameObject("Table");
			try {
				preset.LampName = "Test Lamp";
				preset.Color = new Color(1f, 0.6f, 0.3f);
				preset.FadeSpeedUp = 0.1f;
				preset.FadeSpeedDown = 0.3f;
				var tableComponent = table.AddComponent<TableComponent>();
				var insert = new GameObject("Insert").AddComponent<InsertComponent>().gameObject.AddComponent<InsertLightComponent>();
				insert.transform.SetParent(table.transform);
				var lamp = new GameObject("Lamp").AddComponent<LightComponent>();
				lamp.transform.SetParent(table.transform);
				var flasher = new GameObject("Flasher").AddComponent<LightComponent>();
				flasher.transform.SetParent(table.transform);
				tableComponent.MappingConfig.AddLamp(new LampMapping { IsCoil = true, Device = flasher });
				var rgb = new GameObject("RGB Insert").AddComponent<InsertComponent>().gameObject.AddComponent<InsertLightComponent>();
				rgb.transform.SetParent(table.transform);
				tableComponent.MappingConfig.AddLamp(new LampMapping { Type = LampType.Rgb, Device = rgb });

				var (inserts, lamps) = InsertLampPresets.ApplyToTable(preset, tableComponent);

				Assert.That((inserts, lamps), Is.EqualTo((1, 1)));
				Assert.That(InsertLampPresets.IsRgb(rgb), Is.True);
				Assert.That(rgb.Lamp, Is.Empty, "an RGB lamp is the game's");
				Assert.That(rgb.Color, Is.EqualTo(Color.white));
				Assert.That(insert.Lamp, Is.EqualTo("Test Lamp"));
				Assert.That(insert.Color, Is.EqualTo(preset.Color));
				Assert.That((lamp.FadeSpeedUp, lamp.FadeSpeedDown), Is.EqualTo((0.1f, 0.3f)), "a lamp takes the fades");
				Assert.That((flasher.FadeSpeedUp, flasher.FadeSpeedDown), Is.EqualTo((0f, 0f)), "a flasher keeps its own");
				// The preset is not an asset, so the table's lamp resolves to none; it is at least not mixed.
				Assert.That(InsertLampPresets.TableLamp(tableComponent, out var mixed), Is.Null);
				Assert.That(mixed, Is.False);
				insert.Lamp = "Another";
				var second = new GameObject("Insert 2").AddComponent<InsertComponent>().gameObject.AddComponent<InsertLightComponent>();
				second.transform.SetParent(table.transform);
				second.Lamp = "Test Lamp";
				InsertLampPresets.TableLamp(tableComponent, out mixed);
				Assert.That(mixed, Is.True, "two inserts with different lamps are mixed");
			} finally {
				foreach (var filter in table.GetComponentsInChildren<MeshFilter>(true)) {
					if (filter.sharedMesh != null) { Object.DestroyImmediate(filter.sharedMesh); }
				}
				Object.DestroyImmediate(table);
				Object.DestroyImmediate(preset);
			}
		}

		[Test]
		public void ShouldGiveEveryBulbThePresetAndNoticeEdits()
		{
			var preset = ScriptableObject.CreateInstance<InsertLampPreset>();
			var go = new GameObject("Insert");
			try {
				preset.LampName = "Test Lamp";
				preset.Color = new Color(1f, 0.7f, 0.4f);
				preset.Output = 1.5f;
				preset.FadeSpeedUp = 0.1f;
				preset.FadeSpeedDown = 0.3f;
				go.AddComponent<InsertComponent>();
				var light = go.AddComponent<InsertLightComponent>();
				light.ExtraBulbs[0].Color = Color.blue;

				InsertLampPresets.Apply(preset, new[] { light });

				Assert.That(light.Lamp, Is.EqualTo("Test Lamp"));
				Assert.That(light.Color, Is.EqualTo(preset.Color));
				Assert.That(light.ExtraBulbs[0].Color, Is.EqualTo(preset.Color));
				Assert.That(light.Output, Is.EqualTo(1.5f));
				Assert.That(light.FadeSpeedUp, Is.EqualTo(0.1f));
				Assert.That(light.FadeSpeedDown, Is.EqualTo(0.3f));
				Assert.That(InsertLampPresets.Differs(light, preset), Is.False);

				light.Color = Color.white;
				Assert.That(InsertLampPresets.Differs(light, preset), Is.True);
				light.Color = preset.Color;
				light.Output = 1f;
				Assert.That(InsertLampPresets.Differs(light, preset), Is.True, "a dimmer bulb differs");
				light.Color = Color.white;

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

		[Test]
		public void ShouldLetGoOfThePresetOnceTheBulbIsEdited()
		{
			var preset = ScriptableObject.CreateInstance<InsertLampPreset>();
			var go = new GameObject("Insert");
			try {
				preset.LampName = "Test Lamp";
				preset.Color = new Color(1f, 0.7f, 0.4f);
				preset.Output = 1.5f;
				go.AddComponent<InsertComponent>();
				var light = go.AddComponent<InsertLightComponent>();
				InsertLampPresets.Apply(preset, new[] { light });
				var matched = new[] { (light, preset) };

				InsertLampPresets.UnfollowEdited(matched);
				Assert.That(light.Lamp, Is.EqualTo("Test Lamp"), "an untouched bulb keeps its preset");

				light.Output = 2f;
				InsertLampPresets.UnfollowEdited(matched);
				Assert.That(light.Lamp, Is.Empty, "an edited bulb is a custom one");
				Assert.That(light.Output, Is.EqualTo(2f), "and keeps the edit");
			} finally {
				var filter = go.GetComponent<MeshFilter>();
				if (filter != null && filter.sharedMesh != null) { Object.DestroyImmediate(filter.sharedMesh); }
				Object.DestroyImmediate(go);
				Object.DestroyImmediate(preset);
			}
		}
	}
}
