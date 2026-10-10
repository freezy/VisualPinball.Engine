// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using NUnit.Framework;
using UnityEngine;
using VisualPinball.Unity.Editor;

namespace VisualPinball.Unity.Test
{
	public class InsertPresetTests
	{
		[Test]
		public void ShouldLoadShippedMaterialPresetsAfterRename()
		{
			var presets = InsertMaterialPresets.Find();
			Assert.That(presets, Has.Some.Matches<InsertMaterialPreset>(preset => preset.name == "Clear Plastic" && preset.Find("_InsertLuminosity") == null));
			Assert.That(presets, Has.Some.Matches<InsertMaterialPreset>(preset => preset.name == "Colored Plastic" && preset.Find("_InsertLuminosity") == null));
		}

		[Test]
		public void ShouldApplyBodySettingsWithoutChangingColorsMapsOrCover()
		{
			var shader = Shader.Find("Pinball/Insert Hybrid");
			var coverShader = Shader.Find("Pinball/Insert Cover");
			if (shader == null || coverShader == null) { Assert.Ignore("Requires the HDRP insert shaders."); }
			var source = new Material(shader);
			var sourceCover = new Material(coverShader);
			var target = new Material(shader);
			var targetCover = new Material(coverShader);
			var sourceMap = new Texture2D(4, 4);
			var targetMap = new Texture2D(4, 4);
			var preset = ScriptableObject.CreateInstance<InsertMaterialPreset>();
			try {
				source.SetFloat("_InsertLuminosity", 1.58f);
				source.SetFloat("_InsertClarity", 1f);
				source.SetFloat("_Smoothness", 0.37f);
				source.SetColor("_BaseColor", Color.white);
				source.SetColor("_InsertCavityColor", Color.green);
				source.SetColor("_EmissiveColor", Color.blue);
				source.SetVector("_OpticalLamp", new Vector4(0.01f, -0.02f, 0.03f, 0.009f));
				source.SetVector("_OpticalStarburst", new Vector4(1f, 2f, 3f, 4f));
				source.SetFloat("_InsertLampLevel", 0.7f);
				source.SetTexture("_NormalMap", sourceMap);
				sourceCover.SetColor("_HazeColor", new Color(0.8f, 0.8f, 0.8f));
				sourceCover.SetFloat("_CoverOpacity", 0.365f);

				preset.Capture(source, sourceCover);
				Assert.That(preset.BodyShader, Is.EqualTo(shader.name));
				Assert.That(preset.Body, Has.None.Matches<InsertMaterialPreset.Entry>(e => e.Name == "_InsertLuminosity"), "Max Brightness is the colour's, not the look's");
				Assert.That(preset.Body, Has.Count.EqualTo(25), "the hybrid body's visible numeric settings are captured");
				Assert.That(preset.Edited(source), Is.EqualTo(0));
				source.SetFloat("_Smoothness", 0.5f);
				Assert.That(preset.Edited(source), Is.EqualTo(1), "an edited value is counted");
				source.SetFloat("_Smoothness", 0.37f);
				Assert.That(preset.Body, Has.None.Matches<InsertMaterialPreset.Entry>(e => e.Kind == InsertMaterialPreset.Kind.Color), "new presets exclude all color values");
				Assert.That(preset.Body, Has.None.Matches<InsertMaterialPreset.Entry>(e => e.Name == "_NormalMap"), "new presets exclude texture maps");
				Assert.That(preset.Body, Has.None.Matches<InsertMaterialPreset.Entry>(e => e.Name is "_OpticalLamp" or "_OpticalLampB" or "_OpticalLampC" or "_OpticalLampD"), "the virtual lamps are the insert's own");
				Assert.That(preset.Body, Has.None.Matches<InsertMaterialPreset.Entry>(e => e.Name == "_OpticalStarburst" || e.Name == "_InsertLampLevel"), "hidden bake and runtime values stay out");
				Assert.That(preset.Cover, Is.Empty, "new presets exclude the cover material");
				Assert.That(preset.CoverShader, Is.Empty);

				// Existing preset assets can still contain colors and cover entries.
				preset.Body.Add(new InsertMaterialPreset.Entry { Name = "_BaseColor", Kind = InsertMaterialPreset.Kind.Color, Color = Color.white });
				preset.Body.Add(new InsertMaterialPreset.Entry { Name = "_InsertCavityColor", Kind = InsertMaterialPreset.Kind.Color, Color = Color.green });
				preset.Body.Add(new InsertMaterialPreset.Entry { Name = "_EmissiveColor", Kind = InsertMaterialPreset.Kind.Color, Color = Color.blue });
				preset.Cover.Add(new InsertMaterialPreset.Entry { Name = "_HazeColor", Kind = InsertMaterialPreset.Kind.Color, Color = Color.white });
				preset.Cover.Add(new InsertMaterialPreset.Entry { Name = "_CoverOpacity", Kind = InsertMaterialPreset.Kind.Float, Float = 0.365f });
				preset.Cover.Add(new InsertMaterialPreset.Entry { Name = "_Smoothness", Kind = InsertMaterialPreset.Kind.Float, Float = 0.85f });

				target.SetFloat("_InsertLuminosity", 10f);
				target.SetFloat("_Smoothness", 0.9f);
				target.SetColor("_BaseColor", Color.red);
				target.SetColor("_InsertCavityColor", Color.yellow);
				target.SetColor("_EmissiveColor", Color.magenta);
				target.SetVector("_OpticalLamp", new Vector4(0f, -0.015f, 0f, 0.007f));
				target.SetVector("_OpticalStarburst", new Vector4(9f, 9f, 9f, 9f));
				target.SetTexture("_NormalMap", targetMap);
				target.SetTextureScale("_NormalMap", new Vector2(2f, 3f));
				targetCover.SetColor("_HazeColor", Color.red);
				targetCover.SetFloat("_CoverOpacity", 0.8f);
				targetCover.SetFloat("_Smoothness", 0.73f);

				var count = preset.Apply(target, targetCover);
				Assert.That(count, Is.EqualTo(25));
				Assert.That(target.GetFloat("_InsertLuminosity"), Is.EqualTo(10f).Within(1e-5f), "Max Brightness is the colour's and a look leaves it alone");
				Assert.That(target.GetFloat("_InsertClarity"), Is.EqualTo(1f));
				Assert.That(target.GetFloat("_Smoothness"), Is.EqualTo(0.37f).Within(1e-5f), "legacy cover settings cannot overwrite body settings");
				Assert.That(target.GetColor("_BaseColor"), Is.EqualTo(Color.red));
				Assert.That(target.GetColor("_InsertCavityColor"), Is.EqualTo(Color.yellow));
				Assert.That(target.GetColor("_EmissiveColor"), Is.EqualTo(Color.magenta));
				Assert.That(target.GetVector("_OpticalLamp"), Is.EqualTo(new Vector4(0f, -0.015f, 0f, 0.007f)), "the target keeps its own lamp");
				Assert.That(target.GetVector("_OpticalStarburst"), Is.EqualTo(new Vector4(9f, 9f, 9f, 9f)), "and its own bake");
				Assert.That(target.GetTexture("_NormalMap"), Is.SameAs(targetMap), "and its own maps");
				Assert.That(target.GetTextureScale("_NormalMap"), Is.EqualTo(new Vector2(2f, 3f)));
				Assert.That(targetCover.GetColor("_HazeColor"), Is.EqualTo(Color.red));
				Assert.That(targetCover.GetFloat("_CoverOpacity"), Is.EqualTo(0.8f).Within(1e-5f));
				Assert.That(targetCover.GetFloat("_Smoothness"), Is.EqualTo(0.73f).Within(1e-5f));
				Assert.That(preset.Apply(target, null), Is.GreaterThan(0), "a body without a cover takes the body's look alone");
			} finally {
				Object.DestroyImmediate(preset);
				Object.DestroyImmediate(sourceMap);
				Object.DestroyImmediate(targetMap);
				Object.DestroyImmediate(source);
				Object.DestroyImmediate(sourceCover);
				Object.DestroyImmediate(target);
				Object.DestroyImmediate(targetCover);
			}
		}
	}
}
