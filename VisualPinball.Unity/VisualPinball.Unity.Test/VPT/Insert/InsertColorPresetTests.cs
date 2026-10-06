// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
			Assert.That(InsertColorPresets.NameFor("Insert, Triangle, White, 34", "Clear"), Is.EqualTo("Insert, Triangle, Clear, 34"));
			Assert.That(InsertColorPresets.NameFor("Insert, Triangle", "Purple"), Is.EqualTo("Insert, Triangle, Purple"));
			Assert.That(InsertColorPresets.WritableFolder("Packages/org.visualpinball.unity.assetlibrary/Assets/Library/Inserts/Insert, Red.mat"), Is.EqualTo("Assets/Inserts"), "less the asset library's Library folder");
			Assert.That(InsertColorPresets.WritableFolder("Assets/Inserts/Insert, Red.mat"), Is.EqualTo("Assets/Inserts"));
		}

		[Test]
		public void ShouldLoadGodzillaColorValues()
		{
			var colors = InsertColorPresets.Find();
			Assert.That(colors, Has.Some.Matches<InsertColorPreset>(preset => preset.ColorName == "Clear"));
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
			var bodyShader = Shader.Find("Pinball/Insert Hybrid");
			var coverShader = Shader.Find("Pinball/Insert Cover");
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
				sourceCover.SetColor("_CoverTint", new Color(0.9f, 0.3f, 0.45f));
				sourceCover.SetFloat("_CoverOpacity", 0.42f);
				AssetDatabase.CreateAsset(sourceBody, folder + "/Insert, Circle, Red.mat");
				AssetDatabase.CreateAsset(sourceCover, folder + "/Insert, Cover, Red.mat");
				var insert = go.AddComponent<InsertComponent>();
				var renderer = go.GetComponent<MeshRenderer>();
				renderer.sharedMaterial = sourceBody;
				insert.SetCoverMaterial(sourceCover);
				preset.ColorName = "Blue";
				preset.Tint = Color.blue;
				preset.CavityColor = Color.cyan;
				preset.LampEmission = new Color(0f, 300f, 1024f);
				preset.HazeColor = new Color(0f, 0.3f, 0.8f);
				preset.CoverTint = new Color(0.1f, 0.5f, 0.95f);

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
				Assert.That(blueCover.GetColor("_CoverTint").r, Is.EqualTo(0.1f).Within(1e-5f));
				Assert.That(blueCover.GetColor("_CoverTint").g, Is.EqualTo(0.5f).Within(1e-5f));
				Assert.That(blueCover.GetColor("_CoverTint").b, Is.EqualTo(0.95f).Within(1e-5f));
				Assert.That(sourceBody.GetColor("_BaseColor"), Is.EqualTo(Color.red));
				Assert.That(sourceCover.GetColor("_HazeColor"), Is.EqualTo(Color.red));
				Assert.That(sourceCover.GetColor("_CoverTint").g, Is.EqualTo(0.3f).Within(1e-5f));
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
			var shader = Shader.Find("Pinball/Insert Hybrid");
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
				preset.Tint = Color.blue;

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
			var shader = Shader.Find("Pinball/Insert Hybrid");
			var coverShader = Shader.Find("Pinball/Insert Cover");
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
				preset.Tint = Color.cyan;
				preset.HazeColor = Color.cyan;
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

		[Test]
		public void ShouldTreatALockedAssetLibraryAsReadOnly()
		{
			const string root = "Assets/Locked Library Test";
			const string outside = "Assets/Locked Library Test Materials";
			var forget = typeof(InsertBaker).GetMethod("ForgetLibraries", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
			AssetDatabase.CreateFolder("Assets", "Locked Library Test");
			AssetDatabase.CreateFolder("Assets", "Locked Library Test Materials");
			GameObject instance = null;
			try {
				var library = ScriptableObject.CreateInstance<AssetLibrary>();
				library.LibraryRoot = root;
				library.IsLocked = true;
				AssetDatabase.CreateAsset(library, root + "/Library.asset");
				var inside = new Material(Shader.Find("HDRP/Lit"));
				AssetDatabase.CreateAsset(inside, root + "/Inside.mat");
				var away = new Material(Shader.Find("HDRP/Lit"));
				AssetDatabase.CreateAsset(away, outside + "/Outside.mat");
				var go = new GameObject("Locked Insert");
				go.AddComponent<InsertComponent>().GetComponent<MeshRenderer>().sharedMaterial = away;
				var prefab = PrefabUtility.SaveAsPrefabAsset(go, root + "/Locked Insert.prefab");
				Object.DestroyImmediate(go);
				instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
				var insert = instance.GetComponent<InsertComponent>();
				forget.Invoke(null, null);

				Assert.That(InsertBaker.IsReadOnly(root + "/Inside.mat"), Is.True, "what's in a locked library's folder");
				Assert.That(InsertBaker.IsReadOnly(outside + "/Outside.mat"), Is.False);
				Assert.That(InsertColorPresets.WritableFolder(root + "/Inside.mat"), Is.EqualTo("Assets/Inserts"), "copies don't go back into the library");
				Assert.That(InsertColorPresets.IsReadOnly(insert, away), Is.True, "a material the library's prefab gives the insert, wherever it lives");
				Assert.That(InsertColorPresets.HasReadOnlyMaterial(insert), Is.True);

				library.IsLocked = false;
				Assert.That(InsertBaker.IsReadOnly(root + "/Inside.mat"), Is.False, "an unlocked library can be changed");
				Assert.That(InsertColorPresets.HasReadOnlyMaterial(insert), Is.False);
			} finally {
				if (instance != null) {
					Object.DestroyImmediate(instance);
				}
				AssetDatabase.DeleteAsset(root);
				AssetDatabase.DeleteAsset(outside);
				forget.Invoke(null, null);
			}
		}

		[Test]
		public void ShouldCopyReadOnlyMaterialsToTheProject()
		{
			// Any material of an immutable package stands in for the installed asset library's.
			const string sourcePath = "Packages/com.unity.render-pipelines.high-definition/Editor/SceneTemplates/Materials/Black.mat";
			var source = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
			if (source == null) {
				Assert.Ignore("Requires the HDRP package's scene template materials.");
			}
			var insertsExisted = AssetDatabase.IsValidFolder("Assets/Inserts");
			var first = new GameObject("Read-only insert");
			var second = new GameObject("Read-only insert 2");
			var folders = new List<string>();
			try {
				var a = first.AddComponent<InsertComponent>();
				var b = second.AddComponent<InsertComponent>();
				a.GetComponent<MeshRenderer>().sharedMaterial = source;
				b.GetComponent<MeshRenderer>().sharedMaterial = source;
				Assert.That(InsertColorPresets.IsReadOnly(source), Is.True);
				Assert.That(InsertColorPresets.HasReadOnlyMaterial(a), Is.True);

				Assert.That(InsertColorPresets.SharingReadOnlyMaterials(new[] { a }), Is.EquivalentTo(new[] { a, b }), "a copy for all would go to both");

				Assert.That(InsertColorPresets.CopyToProject(new[] { a }), Is.EqualTo(1));

				var copy = a.GetComponent<MeshRenderer>().sharedMaterial;
				var copyPath = AssetDatabase.GetAssetPath(copy);
				folders.Add(Path.GetDirectoryName(copyPath)?.Replace('\\', '/'));
				Assert.That(copy, Is.Not.SameAs(source));
				Assert.That(copy.parent, Is.SameAs(source), "the copy keeps following the package's material");
				Assert.That(copyPath, Does.StartWith("Assets/Inserts/Read-only insert "), "where a bake of an insert of no prefab goes");
				Assert.That(Path.GetFileNameWithoutExtension(copyPath), Is.EqualTo("Read-only insert Black"), "named after the insert, as a fork names it");
				Assert.That(InsertColorPresets.IsReadOnly(copy), Is.False);
				Assert.That(b.GetComponent<MeshRenderer>().sharedMaterial, Is.SameAs(source), "a copy for one insert leaves the others as they are");
				Assert.That(InsertColorPresets.ProjectCopy(source, folders[0], "Read-only insert Black"), Is.SameAs(copy), "a copy made before is used again");

				Assert.That(InsertColorPresets.CopyToProject(new[] { a, b }), Is.EqualTo(1), "only the insert still wearing the package's material");
				var shared = b.GetComponent<MeshRenderer>().sharedMaterial;
				folders.Add(Path.GetDirectoryName(AssetDatabase.GetAssetPath(shared))?.Replace('\\', '/'));
				Assert.That(shared.parent, Is.SameAs(source));
				Assert.That(shared.name, Is.EqualTo("Black"), "a copy for several inserts is named after the material");
				Assert.That(InsertColorPresets.CopyToProject(new[] { a }), Is.Zero, "nothing left to copy");

				Undo.PerformUndo();
				Assert.That(b.GetComponent<MeshRenderer>().sharedMaterial, Is.SameAs(source), "one undo takes the last copy back");
				Assert.That(a.GetComponent<MeshRenderer>().sharedMaterial, Is.SameAs(copy), "and only that one");
			} finally {
				Object.DestroyImmediate(first);
				Object.DestroyImmediate(second);
				foreach (var folder in folders.Where(folder => !string.IsNullOrEmpty(folder)).Distinct()) {
					AssetDatabase.DeleteAsset(folder);
				}
				if (!insertsExisted) {
					AssetDatabase.DeleteAsset("Assets/Inserts");
				}
			}
		}

		[TestCase("Packages/org.visualpinball.unity.assets/Assets/Library/Inserts/Insert, Triangle, 2in, Starburst", "Assets/Inserts/Insert, Triangle, 2in, Starburst")]
		[TestCase("Assets/Library/Inserts/Insert, Circle", "Assets/Inserts/Insert, Circle")]
		[TestCase("Packages/org.visualpinball.unity.assets/Assets/Library", "Assets")]
		[TestCase("Packages/com.example/Runtime/Materials", "Assets/Runtime/Materials")]
		[TestCase("Assets/Prefabs/My Insert", "Assets/Prefabs/My Insert")]
		[TestCase("C:/Elsewhere", null)]
		public void ShouldMirrorAFolderUnderAssetsWithoutTheLibraryFolder(string folder, string expected)
		{
			Assert.That(InsertBaker.MirroredFolder(folder), Is.EqualTo(expected));
		}

		[Test]
		public void ShouldPutAPrefabsInsertsInItsMirroredFolder()
		{
			const string folder = "Assets/Insert Folder Test";
			AssetDatabase.CreateFolder("Assets", "Insert Folder Test");
			GameObject instance = null;
			try {
				var go = new GameObject("Folder Insert");
				go.AddComponent<InsertComponent>();
				var prefab = PrefabUtility.SaveAsPrefabAsset(go, folder + "/Folder Insert.prefab");
				Object.DestroyImmediate(go);
				instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

				Assert.That(InsertBaker.InsertFolder(instance.GetComponent<InsertComponent>()), Is.EqualTo(folder), "every insert of the prefab shares its folder");

				// Another insert's files go by the name already, so a second one of that name gets a number.
				File.WriteAllText(Path.GetFullPath(folder + "/Folder Insert, Bake.asset"), string.Empty);
				Assert.That(InsertBaker.UniqueName(folder, "Folder Insert"), Is.EqualTo("Folder Insert 2"));
				Assert.That(InsertBaker.UniqueName(folder, "Other Insert"), Is.EqualTo("Other Insert"));
			} finally {
				if (instance != null) {
					Object.DestroyImmediate(instance);
				}
				File.Delete(Path.GetFullPath(folder + "/Folder Insert, Bake.asset"));
				AssetDatabase.DeleteAsset(folder);
			}
		}
	}
}
