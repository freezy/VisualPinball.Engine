// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace VisualPinball.Unity.Test
{
	public class InsertLightTests
	{
		[TestCase(1f)]
		[TestCase(100f)]
		public void ShouldScaleAndColourTheLensAndSpillTogether(float luminosity)
		{
			var shader = Shader.Find("Visual Pinball/Insert Hybrid");
			if (shader == null) {
				Assert.Ignore("Requires the HDRP insert shader.");
			}
			var material = new Material(shader);
			material.SetFloat("_InsertLuminosity", luminosity);
			var root = new GameObject("Insert spill test");
			Mesh mesh = null;
			try {
				var insert = root.AddComponent<InsertComponent>();
				insert.RebuildMesh();
				mesh = root.GetComponent<MeshFilter>().sharedMesh;
				var renderer = root.GetComponent<MeshRenderer>();
				renderer.sharedMaterial = material;
				var control = root.AddComponent<InsertLightComponent>();
				var spillObject = new GameObject("Spill");
				spillObject.transform.SetParent(root.transform);
				var spill = spillObject.AddComponent<Light>();
				RenderPipeline.Current.LightConverter.UpdateLight(spill, new VisualPinball.Engine.VPT.Light.LightData(), true);
				control.SpillLight = spill;
				control.SpillLumens = 4;
				control.SpillRange = 0.12f;
				control.Intensity = 0.25f;
				control.Color = new Color(0.3f, 0.8f, 1f);
				control.Apply();
				var block = new MaterialPropertyBlock();
				renderer.GetPropertyBlock(block);
				Assert.That(block.GetFloat("_InsertLampLevel"), Is.EqualTo(0.25f));
				Assert.That(block.GetVector("_InsertLampColor"), Is.EqualTo((Vector4)control.Color.linear));
				Assert.That(spill.intensity, Is.EqualTo(luminosity).Within(1e-4f));
				Assert.That(spill.range, Is.EqualTo(0.12f).Within(1e-5f));
				Assert.That(spill.color.r, Is.EqualTo(control.Color.r).Within(1e-4f));
				Assert.That(spill.enabled, Is.True);
				Assert.That(block.HasFloat("_InsertLuminosity"), Is.False, "Material brightness must remain editable in the inspector.");
				material.SetFloat("_InsertLuminosity", luminosity * 2f);
				control.Apply();
				Assert.That(spill.intensity, Is.EqualTo(2f * luminosity).Within(1e-4f), "Material edits must immediately scale the spill too.");
				control.Color = Color.green;
				control.Intensity = 0;
				control.Apply();
				renderer.GetPropertyBlock(block);
				Assert.That(block.GetFloat("_InsertLampLevel"), Is.Zero);
				Assert.That(spill.enabled, Is.False);
				Assert.That(spill.intensity, Is.Zero);
				Assert.That(renderer.enabled, Is.True);
				control.Intensity = 1;
				material.SetFloat("_InsertLuminosity", 0);
				control.Apply();
				Assert.That(spill.enabled, Is.False);
				Assert.That(spill.intensity, Is.Zero);
				material.SetFloat("_InsertLuminosity", luminosity);
				control.Apply();
				Assert.That(spill.enabled, Is.True);
				renderer.sharedMaterial = null;
				control.Apply();
				Assert.That(spill.enabled, Is.False, "Removing the material, including Clear Bake to a null original, must stop the spill.");
			} finally {
				Object.DestroyImmediate(root);
				Object.DestroyImmediate(material);
				Object.DestroyImmediate(mesh);
			}
		}

		[Test]
		public void ShouldKeepPreviewAndRomLampEventsAfterTheInsertUpdates()
		{
			var shader = Shader.Find("Visual Pinball/Insert Hybrid");
			if (shader == null) {
				Assert.Ignore("Requires the HDRP insert shader.");
			}
			var material = new Material(shader);
			var root = new GameObject("Managed insert preview");
			Mesh mesh = null;
			try {
				var driver = root.AddComponent<LightComponent>();
				var lens = new GameObject("Lens");
				lens.transform.SetParent(root.transform);
				var insert = lens.AddComponent<InsertComponent>();
				insert.RebuildMesh();
				mesh = lens.GetComponent<MeshFilter>().sharedMesh;
				var renderer = lens.GetComponent<MeshRenderer>();
				renderer.sharedMaterial = material;
				var control = lens.AddComponent<InsertLightComponent>();
				var block = new MaterialPropertyBlock();
				driver.Enabled = false;
				driver.Color = Color.red;
				control.Apply();
				renderer.GetPropertyBlock(block);
				Assert.That(block.GetFloat("_InsertLampLevel"), Is.Zero);
				Assert.That(control.Intensity, Is.Zero);
				Assert.That(control.Color, Is.EqualTo(Color.red));
				Assert.That(block.GetVector("_InsertLampColor"), Is.EqualTo((Vector4)Color.red.linear));
				driver.Enabled = true;
				control.Apply();
				renderer.GetPropertyBlock(block);
				Assert.That(block.GetFloat("_InsertLampLevel"), Is.EqualTo(1));
				Assert.That(block.GetVector("_InsertLampColor"), Is.EqualTo((Vector4)Color.red.linear));

				// Exercise the same normalized intensity and RGB entry points as ROM events.
				typeof(LightComponent).GetField("_hasLights", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(driver, true);
				driver.FadeSpeedUp = driver.FadeSpeedDown = 0;
				var api = driver.GetApi(null);
				((IApi)api).OnInit(null);
				api.OnLamp(VisualPinball.Engine.Game.Engines.LampStatus.On);
				api.OnLamp(0.35f);
				api.OnLamp(Color.blue);
				control.Apply();
				renderer.GetPropertyBlock(block);
				Assert.That(control.Intensity, Is.EqualTo(0.35f));
				Assert.That(control.Color, Is.EqualTo(Color.blue));
				Assert.That(block.GetFloat("_InsertLampLevel"), Is.EqualTo(0.35f));
				Assert.That(block.GetVector("_InsertLampColor"), Is.EqualTo((Vector4)Color.blue.linear));
				api.OnLamp(VisualPinball.Engine.Game.Engines.LampStatus.Off);
				api.OnLamp(Color.green);
				control.Apply();
				renderer.GetPropertyBlock(block);
				Assert.That(control.Intensity, Is.Zero);
				Assert.That(block.GetFloat("_InsertLampLevel"), Is.Zero, "RGB events must leave an off ROM lamp dark.");
				Assert.That(control.Color, Is.EqualTo(Color.green));
			} finally {
				Object.DestroyImmediate(root);
				Object.DestroyImmediate(mesh);
				Object.DestroyImmediate(material);
			}
		}

		[Test]
		public void ShouldKeepRgbAndLevelIndependentWithoutHidingTheInsert()
		{
			var shader = Shader.Find("Visual Pinball/Insert Hybrid");
			if (shader == null) {
				Assert.Ignore("Requires the HDRP insert shader.");
			}
			var material = new Material(shader);
			var root = new GameObject("RGB insert test");
			try {
				var light = root.AddComponent<LightComponent>();
				var lens = new GameObject("Lens");
				lens.transform.SetParent(root.transform);
				var renderer = lens.AddComponent<MeshRenderer>();
				renderer.sharedMaterial = material;
				var block = new MaterialPropertyBlock();
				var initialEmission = material.GetColor("_EmissiveColor");
				light.Enabled = false; // Collect the same renderer/material cache as Awake.
				light.Color = Color.red;
				renderer.GetPropertyBlock(block);
				Assert.That(block.GetFloat("_InsertLampLevel"), Is.Zero, "An RGB event must not light an off lamp.");
				Assert.That(block.GetVector("_InsertLampColor"), Is.EqualTo((Vector4)Color.red.linear));
				Assert.That(renderer.enabled, Is.True, "The plastic must still reflect the table when off.");
				Assert.That(renderer.forceRenderingOff, Is.False);
				light.Enabled = true;
				// Runtime Awake sets this after collecting the materials. Avoid needing
				// a whole Player and game logic engine for an emission regression test.
				typeof(LightComponent).GetField("_hasLights", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(light, true);
				light.FadeSpeedUp = light.FadeSpeedDown = 0;
				light.Color = new Color(0.25f, 0.5f, 1f);
				light.FadeTo(0.25f);
				renderer.GetPropertyBlock(block);
				Assert.That(block.GetFloat("_InsertLampLevel"), Is.EqualTo(0.25f));
				Assert.That(block.GetVector("_InsertLampColor"), Is.EqualTo((Vector4)light.Color.linear), "Fading must keep the latest RGB colour.");
				light.Color = Color.green;
				renderer.GetPropertyBlock(block);
				Assert.That(block.GetFloat("_InsertLampLevel"), Is.EqualTo(0.25f), "An RGB event must keep the current fade level.");
				Assert.That(material.GetColor("_EmissiveColor"), Is.EqualTo(initialEmission), "Lamp events must not mutate shared materials.");
				for (var i = 0; i < 10; i++) {
					light.Enabled = i % 2 == 0;
				}
				var materials = (System.Collections.ICollection)typeof(LightComponent).GetField("_materials", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(light);
				Assert.That(materials.Count, Is.EqualTo(1), "A surface without faux bulbs must not be collected again on every editor preview.");
			} finally {
				Object.DestroyImmediate(root);
				Object.DestroyImmediate(material);
			}
		}
	}
}
