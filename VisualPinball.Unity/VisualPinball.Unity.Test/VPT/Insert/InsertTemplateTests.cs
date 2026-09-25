// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using NUnit.Framework;
using UnityEngine;

namespace VisualPinball.Unity.Test
{
	public class InsertTemplateTests
	{
		[Test]
		public void ShouldCarryTheLookButNotTheBakeNorTheLamps()
		{
			var shader = Shader.Find("Visual Pinball/Insert Hybrid");
			var coverShader = Shader.Find("Visual Pinball/Insert Cover");
			if (shader == null || coverShader == null) { Assert.Ignore("Requires the HDRP insert shaders."); }
			var source = new Material(shader);
			var sourceCover = new Material(coverShader);
			var target = new Material(shader);
			var targetCover = new Material(coverShader);
			var map = new Texture2D(4, 4);
			var template = ScriptableObject.CreateInstance<InsertMaterialTemplate>();
			try {
				source.SetFloat("_InsertLuminosity", 1.58f);
				source.SetFloat("_InsertClarity", 1f);
				source.SetColor("_BaseColor", Color.white);
				source.SetVector("_OpticalLamp", new Vector4(0.01f, -0.02f, 0.03f, 0.009f));
				source.SetVector("_OpticalStarburst", new Vector4(1f, 2f, 3f, 4f));
				source.SetFloat("_InsertLampLevel", 0.7f);
				source.SetTexture("_NormalMap", map);
				sourceCover.SetColor("_HazeColor", new Color(0.8f, 0.8f, 0.8f));
				sourceCover.SetFloat("_CoverOpacity", 0.365f);

				template.Capture(source, sourceCover);
				Assert.That(template.BodyShader, Is.EqualTo(shader.name));
				Assert.That(template.Body, Has.Some.Matches<InsertMaterialTemplate.Entry>(e => e.Name == "_InsertLuminosity" && Mathf.Approximately(e.Float, 1.58f)));
				Assert.That(template.Body, Has.None.Matches<InsertMaterialTemplate.Entry>(e => e.Name is "_OpticalLamp" or "_OpticalLampB" or "_OpticalLampC" or "_OpticalLampD"), "the virtual lamps are the insert's own");
				Assert.That(template.Body, Has.None.Matches<InsertMaterialTemplate.Entry>(e => e.Name == "_OpticalStarburst" || e.Name == "_InsertLampLevel"), "hidden bake and runtime values stay out");
				Assert.That(template.Cover, Has.Some.Matches<InsertMaterialTemplate.Entry>(e => e.Name == "_HazeColor"));

				target.SetFloat("_InsertLuminosity", 10f);
				target.SetColor("_BaseColor", Color.red);
				target.SetVector("_OpticalLamp", new Vector4(0f, -0.015f, 0f, 0.007f));
				target.SetVector("_OpticalStarburst", new Vector4(9f, 9f, 9f, 9f));
				target.SetTexture("_NormalMap", null);
				targetCover.SetColor("_HazeColor", Color.red);

				var count = template.Apply(target, targetCover);
				Assert.That(count, Is.GreaterThan(10));
				Assert.That(target.GetFloat("_InsertLuminosity"), Is.EqualTo(1.58f).Within(1e-5f));
				Assert.That(target.GetFloat("_InsertClarity"), Is.EqualTo(1f));
				Assert.That(target.GetColor("_BaseColor"), Is.EqualTo(Color.white));
				Assert.That(target.GetVector("_OpticalLamp"), Is.EqualTo(new Vector4(0f, -0.015f, 0f, 0.007f)), "the target keeps its own lamp");
				Assert.That(target.GetVector("_OpticalStarburst"), Is.EqualTo(new Vector4(9f, 9f, 9f, 9f)), "and its own bake");
				Assert.That(target.GetTexture("_NormalMap"), Is.Null, "and its own maps");
				Assert.That(targetCover.GetColor("_HazeColor"), Is.EqualTo(new Color(0.8f, 0.8f, 0.8f)));
				Assert.That(targetCover.GetFloat("_CoverOpacity"), Is.EqualTo(0.365f).Within(1e-5f));
				Assert.That(template.Apply(target, null), Is.GreaterThan(0), "a body without a cover takes the body's look alone");
			} finally {
				Object.DestroyImmediate(template);
				Object.DestroyImmediate(map);
				Object.DestroyImmediate(source);
				Object.DestroyImmediate(sourceCover);
				Object.DestroyImmediate(target);
				Object.DestroyImmediate(targetCover);
			}
		}
	}
}
