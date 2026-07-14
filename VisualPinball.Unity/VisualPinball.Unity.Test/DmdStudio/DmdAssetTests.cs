// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System.Reflection;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Test
{
	public class DmdAssetTests
	{
		private const string TestFolder = "Assets/__DmdStudioPhase0Tests";

		[Test]
		public void CuePreservesPolymorphicLayersThroughUnityAssetSerialization()
		{
			AssetDatabase.DeleteAsset(TestFolder);
			AssetDatabase.CreateFolder("Assets", "__DmdStudioPhase0Tests");
			try {
				var cue = ScriptableObject.CreateInstance<DmdCueAsset>();
				cue.CueId = "serialization";
				cue.Layers.Add(new ShapeLayer {
					Name = "box",
					Shape = DmdShapeType.Rect,
					Width = 12,
					Height = 6,
					Filled = true
				});
				cue.Layers.Add(new NumberLayer {
					Name = "score",
					ParamName = "player.score",
					Format = "N0",
					CountUpSeconds = 0.5f
				});

				var path = $"{TestFolder}/Cue.asset";
				AssetDatabase.CreateAsset(cue, path);
				AssetDatabase.SaveAssets();
				Resources.UnloadAsset(cue);
				AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

				var loaded = AssetDatabase.LoadAssetAtPath<DmdCueAsset>(path);
				Assert.That(loaded, Is.Not.SameAs(cue));
				Assert.That(loaded.Layers, Has.Count.EqualTo(2));
				Assert.That(loaded.Layers[0], Is.TypeOf<ShapeLayer>());
				Assert.That(((ShapeLayer)loaded.Layers[0]).Width, Is.EqualTo(12));
				Assert.That(loaded.Layers[1], Is.TypeOf<NumberLayer>());
				Assert.That(((NumberLayer)loaded.Layers[1]).ParamName, Is.EqualTo("player.score"));
			} finally {
				AssetDatabase.DeleteAsset(TestFolder);
			}
		}

		[Test]
		public void PackageJsonIgnoresUnityGraphReferences()
		{
			AssertJsonIgnored(typeof(DmdProjectAsset), nameof(DmdProjectAsset.Cues));
			AssertJsonIgnored(typeof(DmdProjectAsset), nameof(DmdProjectAsset.Sprites));
			AssertJsonIgnored(typeof(DmdProjectAsset), nameof(DmdProjectAsset.Fonts));
			AssertJsonIgnored(typeof(DmdProjectAsset), nameof(DmdProjectAsset.Palettes));
			AssertJsonIgnored(typeof(DmdProjectAsset), nameof(DmdProjectAsset.PreviewTint));
			AssertJsonIgnored(typeof(DmdCueAsset), nameof(DmdCueAsset.Layers));
			AssertJsonIgnored(typeof(BitmapLayer), nameof(BitmapLayer.Sprite));
			AssertJsonIgnored(typeof(TextLayer), nameof(TextLayer.Font));
			AssertJsonIgnored(typeof(MaskLayer), nameof(MaskLayer.Mask));
			Assert.That(typeof(DmdCueAsset).GetField(nameof(DmdCueAsset.Layers))
				.GetCustomAttribute<SerializeReference>(), Is.Not.Null);

			var project = ScriptableObject.CreateInstance<DmdProjectAsset>();
			var cue = ScriptableObject.CreateInstance<DmdCueAsset>();
			var sprite = ScriptableObject.CreateInstance<DmdSpriteAsset>();
			try {
				project.Cues.Add(cue);
				project.Sprites.Add(sprite);
				cue.Layers.Add(new BitmapLayer { Sprite = sprite });

				var projectJson = Encoding.UTF8.GetString(PackageApi.Packer.Pack(project));
				var cueJson = Encoding.UTF8.GetString(PackageApi.Packer.Pack(cue));
				var layerJson = Encoding.UTF8.GetString(PackageApi.Packer.Pack((BitmapLayer)cue.Layers[0]));

				Assert.That(projectJson, Does.Not.Contain("\"Cues\""));
				Assert.That(projectJson, Does.Not.Contain("\"Sprites\""));
				Assert.That(projectJson, Does.Not.Contain("\"PreviewTint\""));
				Assert.That(cueJson, Does.Not.Contain("\"Layers\""));
				Assert.That(layerJson, Does.Not.Contain("\"Sprite\""));
			} finally {
				Object.DestroyImmediate(sprite);
				Object.DestroyImmediate(cue);
				Object.DestroyImmediate(project);
			}
		}

		[Test]
		public void DuplicateDisplayConfigsCompareAllVisibleSettings()
		{
			var color = new Color(1f, 0.25f, 0f);
			var original = new DisplayConfig("dmd0", 128, 32, false, color, Color.black);

			Assert.That(DisplayPlayer.SameConfig(original,
				new DisplayConfig("dmd0", 128, 32, false, color, Color.black)), Is.True);
			Assert.That(DisplayPlayer.SameConfig(original,
				new DisplayConfig("dmd1", 128, 32, false, color, Color.black)), Is.False);
			Assert.That(DisplayPlayer.SameConfig(original,
				new DisplayConfig("dmd0", 64, 32, false, color, Color.black)), Is.False);
			Assert.That(DisplayPlayer.SameConfig(original,
				new DisplayConfig("dmd0", 128, 16, false, color, Color.black)), Is.False);
			Assert.That(DisplayPlayer.SameConfig(original,
				new DisplayConfig("dmd0", 128, 32, true, color, Color.black)), Is.False);
			Assert.That(DisplayPlayer.SameConfig(original,
				new DisplayConfig("dmd0", 128, 32, false, Color.white, Color.black)), Is.False);
			Assert.That(DisplayPlayer.SameConfig(original,
				new DisplayConfig("dmd0", 128, 32, false, color, Color.white)), Is.False);
			Assert.That(DisplayPlayer.SameConfig(original,
				new DisplayConfig("dmd0", 128, 32, false, null, Color.black)), Is.False);
		}

		[Test]
		public void DotMatrixDisplaySkipsAnUnchangedAllocatedSize()
		{
			var gameObject = new GameObject("DMD");
			var texture = new Texture2D(128, 32);
			try {
				var display = gameObject.AddComponent<DotMatrixDisplayComponent>();
				var textureField = typeof(DisplayComponent).GetField("_texture",
					BindingFlags.Instance | BindingFlags.NonPublic);
				Assert.That(textureField, Is.Not.Null);
				textureField.SetValue(display, texture);

				display.UpdateDimensions(128, 32, false);

				Assert.That(textureField.GetValue(display), Is.SameAs(texture));
			} finally {
				Object.DestroyImmediate(gameObject);
				Object.DestroyImmediate(texture);
			}
		}

		[Test]
		public void DotMatrixDisplayReleasesOwnedResourcesOnResize()
		{
			var gameObject = new GameObject("DMD");
			Texture2D currentTexture = null;
			Mesh currentMesh = null;
			Material currentMaterial = null;
			try {
				var shader = AssetDatabase.LoadAssetAtPath<Shader>(
					"Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Test/DmdStudio/DmdTestDisplay.shader");
				Assert.That(shader, Is.Not.Null);
				currentMaterial = new Material(shader);
				gameObject.AddComponent<MeshRenderer>().sharedMaterial = currentMaterial;
				var display = gameObject.AddComponent<DotMatrixDisplayComponent>();
				var textureField = typeof(DisplayComponent).GetField("_texture",
					BindingFlags.Instance | BindingFlags.NonPublic);
				Assert.That(textureField, Is.Not.Null);

				display.UpdateDimensions(128, 32, false);
				var previousTexture = (Texture2D)textureField.GetValue(display);
				var previousMesh = gameObject.GetComponent<MeshFilter>().sharedMesh;

				display.UpdateDimensions(64, 16, true);

				currentTexture = (Texture2D)textureField.GetValue(display);
				currentMesh = gameObject.GetComponent<MeshFilter>().sharedMesh;
				Assert.That(previousTexture == null, Is.True);
				Assert.That(previousMesh == null, Is.True);
				Assert.That(currentTexture, Is.Not.Null);
				Assert.That(currentMesh, Is.Not.Null);
				Assert.That(display.Width, Is.EqualTo(64));
				Assert.That(display.Height, Is.EqualTo(16));
			} finally {
				Object.DestroyImmediate(gameObject);
				Object.DestroyImmediate(currentMaterial);
				Object.DestroyImmediate(currentMesh);
				Object.DestroyImmediate(currentTexture);
			}
		}

		private static void AssertJsonIgnored(System.Type type, string fieldName)
		{
			var field = type.GetField(fieldName);
			Assert.That(field, Is.Not.Null);
			Assert.That(field.GetCustomAttributes(false).Any(attribute =>
				attribute.GetType().FullName == "Newtonsoft.Json.JsonIgnoreAttribute"), Is.True,
				$"{type.Name}.{fieldName} must be ignored by package JSON serialization.");
		}
	}
}
