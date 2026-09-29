// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VisualPinball.Unity.Editor;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Test
{
	public class InsertColorPresetTests
	{
		[Test]
		public void ShouldNameColorsAndMirrorPackageFolders()
		{
			Assert.That(InsertColorPresets.NameFor("Insert, Circle, 1in, Red", "Blue"), Is.EqualTo("Insert, Circle, 1in, Blue"));
			Assert.That(InsertColorPresets.NameFor("Insert, Blue Angel, Red", "Green"), Is.EqualTo("Insert, Blue Angel, Green"));
			Assert.That(InsertColorPresets.NameFor("Insert, Triangle, Clear, 34", "White"), Is.EqualTo("Insert, Triangle, White, 34"));
			Assert.That(InsertColorPresets.NameFor("Insert, Triangle", "Purple"), Is.EqualTo("Insert, Triangle, Purple"));
			Assert.That(InsertColorPresets.WritableFolder("Packages/org.visualpinball.unity.assetlibrary/Assets/Library/Inserts/Insert, Red.mat"), Is.EqualTo("Assets/Library/Inserts"));
			Assert.That(InsertColorPresets.WritableFolder("Assets/Inserts/Insert, Red.mat"), Is.EqualTo("Assets/Inserts"));
		}

		[Test]
		public void ShouldLoadGodzillaColorValues()
		{
			var colors = InsertColorPresets.Find();
			Assert.That(colors, Has.Some.Matches<InsertColorPreset>(preset => preset.ColorName == "White"));
			Assert.That(colors, Has.Some.Matches<InsertColorPreset>(preset => preset.ColorName == "Blue" && preset.LampEmission.b > 1000f));
			Assert.That(colors, Has.Some.Matches<InsertColorPreset>(preset => preset.ColorName == "Green"));
			Assert.That(colors, Has.Some.Matches<InsertColorPreset>(preset => preset.ColorName == "Red"));
			Assert.That(colors, Has.Some.Matches<InsertColorPreset>(preset => preset.ColorName == "Yellow"));
			Assert.That(colors, Has.Some.Matches<InsertColorPreset>(preset => preset.ColorName == "Teal"));
			Assert.That(colors, Has.Some.Matches<InsertColorPreset>(preset => preset.ColorName == "Purple"));
		}

		[Test]
		public void ShouldIgnoreAPlainLitInsert()
		{
			var shader = Shader.Find("HDRP/Lit");
			if (shader == null) { Assert.Ignore("Requires HDRP/Lit."); }
			var body = new Material(shader);
			var preset = ScriptableObject.CreateInstance<InsertColorPreset>();
			var go = new GameObject("Insert");
			try {
				var insert = go.AddComponent<InsertComponent>();
				var renderer = go.GetComponent<MeshRenderer>();
				renderer.sharedMaterial = body;
				preset.ColorName = "Blue";
				preset.LampEmission = new Color(0f, 300f, 1024f);
				Assert.That(body.HasProperty("_BaseColor") && body.HasProperty("_EmissiveColor"), Is.True);
				Assert.That(InsertColorPresets.CanApply(insert), Is.False);
				Assert.That(InsertColorPresets.Apply(preset, insert), Is.False);
				Assert.That(body.GetColor("_EmissiveColor"), Is.EqualTo(Color.black));
			} finally {
				var filter = go.GetComponent<MeshFilter>();
				if (filter != null && filter.sharedMesh != null) { Object.DestroyImmediate(filter.sharedMesh); }
				Object.DestroyImmediate(go);
				Object.DestroyImmediate(body);
				Object.DestroyImmediate(preset);
			}
		}

		[Test]
		public void ShouldCreateAndReuseBodyAndCoverColorMaterials()
		{
			var bodyShader = Shader.Find("Visual Pinball/Insert Hybrid");
			var coverShader = Shader.Find("Visual Pinball/Insert Cover");
			if (bodyShader == null || coverShader == null) { Assert.Ignore("Requires the HDRP insert shaders."); }
			var folder = "Assets/InsertColorPresetTests" + Guid.NewGuid().ToString("N");
			AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
			var sourceBody = new Material(bodyShader) { name = "Insert, Circle, Red" };
			var sourceCover = new Material(coverShader) { name = "Insert, Cover, Red" };
			var preset = ScriptableObject.CreateInstance<InsertColorPreset>();
			var go = new GameObject("Insert");
			try {
				sourceBody.SetColor("_BaseColor", Color.red);
				sourceBody.SetFloat("_Smoothness", 0.37f);
				sourceCover.SetColor("_HazeColor", Color.red);
				sourceCover.SetFloat("_CoverOpacity", 0.42f);
				AssetDatabase.CreateAsset(sourceBody, folder + "/Insert, Circle, Red.mat");
				AssetDatabase.CreateAsset(sourceCover, folder + "/Insert, Cover, Red.mat");
				var insert = go.AddComponent<InsertComponent>();
				var renderer = go.GetComponent<MeshRenderer>();
				renderer.sharedMaterial = sourceBody;
				insert.SetCoverMaterial(sourceCover);
				preset.ColorName = "Blue";
				preset.BodyColor = Color.blue;
				preset.CavityColor = Color.cyan;
				preset.LampEmission = new Color(0f, 300f, 1024f);
				preset.CoverColor = new Color(0f, 0.3f, 0.8f);

				Assert.That(InsertColorPresets.Apply(preset, insert), Is.True);
				var blueBody = renderer.sharedMaterial;
				var blueCover = insert.CoverMaterial;
				Assert.That(AssetDatabase.GetAssetPath(blueBody), Is.EqualTo(folder + "/Insert, Circle, Blue.mat"));
				Assert.That(AssetDatabase.GetAssetPath(blueCover), Is.EqualTo(folder + "/Insert, Cover, Blue.mat"));
				Assert.That(blueBody.parent, Is.Null, "a material already named for a color is copied");
				Assert.That(blueBody.GetColor("_BaseColor"), Is.EqualTo(Color.blue));
				Assert.That(blueBody.GetColor("_InsertCavityColor"), Is.EqualTo(Color.cyan));
				Assert.That(blueBody.GetColor("_EmissiveColor"), Is.EqualTo(new Color(0f, 300f, 1024f)));
				Assert.That(blueBody.GetFloat("_Smoothness"), Is.EqualTo(0.37f).Within(1e-5f));
				Assert.That(blueCover.GetColor("_HazeColor").r, Is.EqualTo(0f).Within(1e-5f));
				Assert.That(blueCover.GetColor("_HazeColor").g, Is.EqualTo(0.3f).Within(1e-5f));
				Assert.That(blueCover.GetColor("_HazeColor").b, Is.EqualTo(0.8f).Within(1e-5f));
				Assert.That(blueCover.GetFloat("_CoverOpacity"), Is.EqualTo(0.42f).Within(1e-5f));
				Assert.That(sourceBody.GetColor("_BaseColor"), Is.EqualTo(Color.red));
				Assert.That(sourceCover.GetColor("_HazeColor"), Is.EqualTo(Color.red));
				Undo.PerformUndo();
				Assert.That(AssetDatabase.GetAssetPath(renderer.sharedMaterial), Is.EqualTo(AssetDatabase.GetAssetPath(sourceBody)));
				Assert.That(AssetDatabase.GetAssetPath(insert.CoverMaterial), Is.EqualTo(AssetDatabase.GetAssetPath(sourceCover)));
				Assert.That(AssetDatabase.LoadAssetAtPath<Material>(folder + "/Insert, Circle, Blue.mat"), Is.Not.Null, "undo keeps created assets for reuse");
				Undo.PerformRedo();
				Assert.That(AssetDatabase.GetAssetPath(renderer.sharedMaterial), Is.EqualTo(AssetDatabase.GetAssetPath(blueBody)));
				Assert.That(AssetDatabase.GetAssetPath(insert.CoverMaterial), Is.EqualTo(AssetDatabase.GetAssetPath(blueCover)));

				preset.ColorName = "Red";
				Assert.That(InsertColorPresets.Apply(preset, insert), Is.True);
				Assert.That(AssetDatabase.GetAssetPath(renderer.sharedMaterial), Is.EqualTo(AssetDatabase.GetAssetPath(sourceBody)));
				Assert.That(AssetDatabase.GetAssetPath(insert.CoverMaterial), Is.EqualTo(AssetDatabase.GetAssetPath(sourceCover)));
				preset.ColorName = "Blue";
				Assert.That(InsertColorPresets.Apply(preset, insert), Is.True);
				Assert.That(AssetDatabase.GetAssetPath(renderer.sharedMaterial), Is.EqualTo(AssetDatabase.GetAssetPath(blueBody)));
				Assert.That(AssetDatabase.GetAssetPath(insert.CoverMaterial), Is.EqualTo(AssetDatabase.GetAssetPath(blueCover)));
				Assert.That(AssetDatabase.FindAssets("t:Material", new[] { folder }), Has.Length.EqualTo(4), "reusing colors must not create another material");
			} finally {
				var filter = go.GetComponent<MeshFilter>();
				if (filter != null && filter.sharedMesh != null) { Object.DestroyImmediate(filter.sharedMesh); }
				Object.DestroyImmediate(go);
				Object.DestroyImmediate(preset);
				AssetDatabase.DeleteAsset(folder);
			}
		}

		[Test]
		public void ShouldKeepTheSourceMapsWhenOnlyAnotherInsertHasTheRequestedColor()
		{
			var shader = Shader.Find("Visual Pinball/Insert Hybrid");
			if (shader == null) { Assert.Ignore("Requires the HDRP insert shader."); }
			var folder = "Assets/InsertColorPresetTests" + Guid.NewGuid().ToString("N");
			AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
			var source = new Material(shader) { name = "Insert, Circle, Red" };
			var unrelated = new Material(shader) { name = "Insert, Triangle, Blue" };
			var sourceMap = new Texture2D(1, 1);
			var unrelatedMap = new Texture2D(1, 1);
			var preset = ScriptableObject.CreateInstance<InsertColorPreset>();
			var go = new GameObject("Insert");
			try {
				AssetDatabase.CreateAsset(sourceMap, folder + "/Circle Map.asset");
				AssetDatabase.CreateAsset(unrelatedMap, folder + "/Triangle Map.asset");
				source.SetTexture("_OpticalNormalMap", sourceMap);
				unrelated.SetTexture("_OpticalNormalMap", unrelatedMap);
				var lamp = new Vector4(0f, -0.02f, 0f, 0.007f);
				source.SetVector("_OpticalLamp", lamp);
				unrelated.SetVector("_OpticalLamp", new Vector4(0.02f, -0.01f, 0.03f, 0.004f));
				AssetDatabase.CreateAsset(source, folder + "/Insert, Circle, Red.mat");
				AssetDatabase.CreateAsset(unrelated, folder + "/Insert, Triangle, Blue.mat");
				var insert = go.AddComponent<InsertComponent>();
				var renderer = go.GetComponent<MeshRenderer>();
				renderer.sharedMaterial = source;
				preset.ColorName = "Blue";
				preset.BodyColor = Color.blue;

				Assert.That(InsertColorPresets.Apply(preset, insert), Is.True);
				var result = renderer.sharedMaterial;
				Assert.That(result, Is.Not.EqualTo(unrelated));
				Assert.That(result.name, Is.EqualTo("Insert, Circle, Blue"));
				Assert.That(AssetDatabase.GetAssetPath(result), Is.EqualTo(folder + "/Insert, Circle, Blue.mat"));
				Assert.That(AssetDatabase.GetAssetPath(result.GetTexture("_OpticalNormalMap")), Is.EqualTo(folder + "/Circle Map.asset"));
				Assert.That(result.GetVector("_OpticalLamp"), Is.EqualTo(lamp));
				Assert.That(result.GetColor("_BaseColor"), Is.EqualTo(Color.blue));
			} finally {
				var filter = go.GetComponent<MeshFilter>();
				if (filter != null && filter.sharedMesh != null) { Object.DestroyImmediate(filter.sharedMesh); }
				Object.DestroyImmediate(go);
				Object.DestroyImmediate(preset);
				AssetDatabase.DeleteAsset(folder);
			}
		}

		[Test]
		public void ShouldMakeVariantWhenSourceHasNoColorName()
		{
			var shader = Shader.Find("Visual Pinball/Insert Hybrid");
			var coverShader = Shader.Find("Visual Pinball/Insert Cover");
			if (shader == null || coverShader == null) { Assert.Ignore("Requires the HDRP insert shaders."); }
			var folder = "Assets/InsertColorPresetTests" + Guid.NewGuid().ToString("N");
			AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
			var source = new Material(shader) { name = "Insert, Circle" };
			var sourceCover = new Material(coverShader) { name = "Insert, Cover" };
			var preset = ScriptableObject.CreateInstance<InsertColorPreset>();
			var go = new GameObject("Insert");
			try {
				AssetDatabase.CreateAsset(source, folder + "/Insert, Circle.mat");
				AssetDatabase.CreateAsset(sourceCover, folder + "/Insert, Cover.mat");
				var insert = go.AddComponent<InsertComponent>();
				var renderer = go.GetComponent<MeshRenderer>();
				renderer.sharedMaterial = source;
				insert.SetCoverMaterial(sourceCover);
				preset.ColorName = "Teal";
				preset.BodyColor = Color.cyan;
				preset.CoverColor = Color.cyan;
				Assert.That(InsertColorPresets.Apply(preset, insert), Is.True);
				Assert.That(renderer.sharedMaterial.name, Is.EqualTo("Insert, Circle, Teal"));
				Assert.That(AssetDatabase.GetAssetPath(renderer.sharedMaterial.parent), Is.EqualTo(AssetDatabase.GetAssetPath(source)));
				Assert.That(AssetDatabase.GetAssetPath(renderer.sharedMaterial), Is.EqualTo(folder + "/Insert, Circle, Teal.mat"));
				Assert.That(insert.CoverMaterial.name, Is.EqualTo("Insert, Cover, Teal"));
				Assert.That(AssetDatabase.GetAssetPath(insert.CoverMaterial.parent), Is.EqualTo(AssetDatabase.GetAssetPath(sourceCover)));
				Assert.That(AssetDatabase.GetAssetPath(insert.CoverMaterial), Is.EqualTo(folder + "/Insert, Cover, Teal.mat"));
			} finally {
				var filter = go.GetComponent<MeshFilter>();
				if (filter != null && filter.sharedMesh != null) { Object.DestroyImmediate(filter.sharedMesh); }
				Object.DestroyImmediate(go);
				Object.DestroyImmediate(preset);
				AssetDatabase.DeleteAsset(folder);
			}
		}
	}
}
