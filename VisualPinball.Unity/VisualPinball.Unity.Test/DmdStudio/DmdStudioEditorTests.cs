// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VisualPinball.Unity.Editor;

namespace VisualPinball.Unity.Test
{
	public class DmdStudioEditorTests
	{
		private const string TestFolder = "Assets/DmdStudioPhase4Tests";
		private readonly List<string> _temporaryFiles = new List<string>();

		[SetUp]
		public void SetUp()
		{
			if (!AssetDatabase.IsValidFolder(TestFolder)) {
				AssetDatabase.CreateFolder("Assets", "DmdStudioPhase4Tests");
			}
		}

		[TearDown]
		public void TearDown()
		{
			Undo.ClearAll();
			AssetDatabase.DeleteAsset(TestFolder);
			foreach (var file in _temporaryFiles) {
				if (File.Exists(file)) {
					File.Delete(file);
				}
			}
			foreach (var folder in _temporaryFiles.Select(Path.GetDirectoryName).Where(Directory.Exists).Distinct()) {
				Directory.Delete(folder, true);
			}
			_temporaryFiles.Clear();
		}

		[Test]
		public void SpriteImportFlipsRowsConvertsLumaPreservesAlphaAndAddsToProject()
		{
			var project = CreateProject(1, 2, DmdColorMode.Mono16);
			var png = WritePng(1, 2, new[] {
				new Color32(0, 0, 255, 20),
				new Color32(255, 0, 0, 200),
			});

			var result = DmdSpriteImporter.Import(project, new[] { png }, $"{TestFolder}/Sprite.asset",
				DmdSpriteImportOptions.Default);

			Assert.That(result.Sprite.Frames, Has.Count.EqualTo(1));
			Assert.That(result.Sprite.Frames[0].Pixels, Is.EqualTo(new byte[] { 76, 29 }));
			Assert.That(result.Sprite.Frames[0].Alpha, Is.EqualTo(new byte[] { 200, 20 }));
			Assert.That(result.ShadeHistogram[76], Is.EqualTo(1));
			Assert.That(result.ShadeHistogram[29], Is.EqualTo(1));
			Assert.That(result.Warnings.Any(warning => warning.Contains("Rec.601")), Is.True);
			Assert.That(project.Sprites, Does.Contain(result.Sprite));
		}

		[Test]
		public void SpriteSheetSlicesTopToBottomThenLeftToRight()
		{
			var project = CreateProject(1, 1, DmdColorMode.Rgb24);
			var png = WritePng(2, 2, new[] {
				new Color32(0, 0, 255, 255), new Color32(255, 255, 255, 255),
				new Color32(255, 0, 0, 255), new Color32(0, 255, 0, 255),
			});

			var result = DmdSpriteImporter.Import(project, new[] { png }, $"{TestFolder}/Sheet.asset",
				new DmdSpriteImportOptions { CellWidth = 1, CellHeight = 1, DefaultFrameDuration = 2 });

			Assert.That(result.Sprite.Frames, Has.Count.EqualTo(4));
			Assert.That(result.Sprite.Frames.Select(frame => frame.Pixels[0]), Is.EqualTo(new byte[] { 255, 0, 0, 255 }));
			Assert.That(result.Sprite.Frames.Select(frame => frame.Pixels[1]), Is.EqualTo(new byte[] { 0, 255, 0, 255 }));
			Assert.That(result.Sprite.Frames.Select(frame => frame.Pixels[2]), Is.EqualTo(new byte[] { 0, 0, 255, 255 }));
			Assert.That(result.Sprite.FrameDurations, Is.EqualTo(new[] { 2, 2, 2, 2 }));
		}

		[Test]
		public void SpriteSequencePreservesCallerOrderAndReportsCanvasAndShadeWarnings()
		{
			var project = CreateProject(2, 2, DmdColorMode.Mono4);
			var first = WritePng(1, 5, Enumerable.Repeat(new Color32(10, 10, 10, 255), 5).ToArray());
			var second = WritePng(1, 5, new[] {
				new Color32(255, 255, 255, 255), new Color32(192, 192, 192, 255),
				new Color32(128, 128, 128, 255), new Color32(64, 64, 64, 255),
				new Color32(0, 0, 0, 255)
			});

			var result = DmdSpriteImporter.Import(project, new[] { second, first }, $"{TestFolder}/Sequence.asset",
				DmdSpriteImportOptions.Default);

			Assert.That(result.Sprite.Frames.Select(frame => frame.Pixels[0]), Is.EqualTo(new byte[] { 0, 10 }));
			Assert.That(result.Warnings.Any(warning => warning.Contains("differs from the project canvas")), Is.True);
			Assert.That(result.Warnings.Any(warning => warning.Contains("5 intensity levels")), Is.True);
		}

		[Test]
		public void SpriteSheetEnforcesFrameCapBeforeCreatingAsset()
		{
			var project = CreateProject(1, 1, DmdColorMode.Mono16);
			var pixels = Enumerable.Repeat(new Color32(0, 0, 0, 255), 65 * 65).ToArray();
			var png = WritePng(65, 65, pixels);

			Assert.Throws<ArgumentException>(() => DmdSpriteImporter.Import(project, new[] { png },
				$"{TestFolder}/TooMany.asset", new DmdSpriteImportOptions {
					CellWidth = 1, CellHeight = 1, DefaultFrameDuration = 1
				}));
			Assert.That(AssetDatabase.LoadAssetAtPath<DmdSpriteAsset>($"{TestFolder}/TooMany.asset"), Is.Null);
		}

		[Test]
		public void BmFontImportParsesMetricsKerningAndTopOriginAtlas()
		{
			var project = CreateProject(128, 32, DmdColorMode.Mono16);
			var atlas = WritePng(2, 2, new[] {
				new Color32(0, 0, 255, 255), new Color32(255, 255, 255, 255),
				new Color32(255, 0, 0, 255), new Color32(0, 255, 0, 255),
			}, "atlas.png");
			var descriptor = Path.Combine(Path.GetDirectoryName(atlas), "font.fnt");
			File.WriteAllText(descriptor,
				"info face=\"Test Font\" size=8\n" +
				"common lineHeight=9 base=7 scaleW=2 scaleH=2 pages=1 packed=0\n" +
				$"page id=0 file=\"{Path.GetFileName(atlas)}\"\n" +
				"chars count=2\n" +
				"char id=65 x=0 y=0 width=1 height=1 xoffset=-1 yoffset=2 xadvance=3 page=0 chnl=15\n" +
				"char id=66 x=1 y=1 width=1 height=1 xoffset=0 yoffset=1 xadvance=4 page=0 chnl=15\n" +
				"kernings count=1\n" +
				"kerning first=65 second=66 amount=-1\n");
			_temporaryFiles.Add(descriptor);

			var font = DmdBmFontImporter.Import(project, descriptor, $"{TestFolder}/Font.asset");

			Assert.That(font.LineHeight, Is.EqualTo(9));
			Assert.That(font.Baseline, Is.EqualTo(7));
			Assert.That(font.Glyphs, Has.Count.EqualTo(2));
			Assert.That(font.Glyphs[0].OffsetX, Is.EqualTo(-1));
			Assert.That(font.Kerning.Single().Adjustment, Is.EqualTo(-1));
			Assert.That(font.Atlas.Pixels, Is.EqualTo(new byte[] { 76, 150, 29, 255 }));
			Assert.That(font.Atlas.Alpha, Is.Empty);
			Assert.That(project.Fonts, Does.Contain(font));
		}

		[Test]
		public void OpaqueBmFontAtlasUsesLuminanceAsTheGlyphMask()
		{
			var project = CreateProject(2, 1, DmdColorMode.Mono16);
			var atlas = WritePng(2, 1, new[] {
				new Color32(0, 0, 0, 255), new Color32(255, 255, 255, 255)
			}, "opaque-atlas.png");
			var descriptor = Path.Combine(Path.GetDirectoryName(atlas), "opaque.fnt");
			File.WriteAllText(descriptor,
				"common lineHeight=1 base=1 scaleW=2 scaleH=1 pages=1 packed=0\n" +
				$"page id=0 file=\"{Path.GetFileName(atlas)}\"\n" +
				"chars count=1\n" +
				"char id=65 x=0 y=0 width=2 height=1 xoffset=0 yoffset=0 xadvance=2 page=0 chnl=15\n");
			_temporaryFiles.Add(descriptor);
			var font = DmdBmFontImporter.Import(project, descriptor, $"{TestFolder}/OpaqueFont.asset");
			var surface = new DmdSurface(2, 1, DmdPixelFormat.I8);

			DmdTextRenderer.Draw(surface, font, "A", 0, 0, DmdAnchor.TopLeft, DmdTextEffect.None,
				DmdShade.White, DmdShade.Black, DmdBlendMode.Alpha, byte.MaxValue, new CueDiagnostics());

			Assert.That(font.Atlas.Alpha, Is.Empty);
			Assert.That(surface.Data, Is.EqualTo(new byte[] { 0, 255 }));
		}

		[Test]
		public void BmFontImportRejectsMultiplePagesWithoutCreatingAsset()
		{
			var project = CreateProject(128, 32, DmdColorMode.Mono16);
			var descriptor = WriteText(
				"common lineHeight=9 base=7 scaleW=1 scaleH=1 pages=2 packed=0\n" +
				"page id=0 file=\"atlas.png\"\n", "multi.fnt");

			Assert.Throws<NotSupportedException>(() => DmdBmFontImporter.Import(project, descriptor,
				$"{TestFolder}/Multi.asset"));
			Assert.That(AssetDatabase.LoadAssetAtPath<DmdFontAsset>($"{TestFolder}/Multi.asset"), Is.Null);
		}

		[Test]
		public void DefaultSampleStatesAreIdempotentAndCoverEdgeValues()
		{
			var project = CreateProject(128, 32, DmdColorMode.Mono16);

			Assert.That(DmdStudioDefaults.EnsureSampleStates(project), Is.True);
			Assert.That(DmdStudioDefaults.EnsureSampleStates(project), Is.False);
			Assert.That(project.SampleStates, Has.Count.EqualTo(8));
			var huge = project.SampleStates.Single(state => state.Name == "Huge Score");
			Assert.That(huge.Values.Single().IntValue, Is.EqualTo(9_999_999_990L));
			Assert.That(project.SampleStates.Any(state => state.Name == "Expired Timer" &&
				state.Values.Single().IntValue == 0), Is.True);
			Assert.That(project.SampleStates.Any(state => state.Name == "Missing Text" &&
				state.Values.Count == 0), Is.True);
			Assert.That(project.SampleStates.Any(state => state.Name == "Empty Text" &&
				state.Values.Single().StringValue == string.Empty), Is.True);
		}

		[Test]
		public void CanvasRendersRawTintAndDotModesWithoutChangingOrientation()
		{
			var project = ScriptableObject.CreateInstance<DmdProjectAsset>();
			project.Width = 2;
			project.Height = 1;
			project.PreviewTint = Color.red;
			var surface = new DmdSurface(2, 1, DmdPixelFormat.I8);
			surface.Data[0] = 255;
			surface.Data[1] = 0;
			var canvas = new DmdCanvasView();
			try {
				canvas.SetFrame(surface, project, DmdCanvasMode.Raw, true);
				Assert.That(canvas.PreviewTexture.width, Is.EqualTo(2));
				Assert.That(canvas.PreviewTexture.GetPixel(0, 0).r, Is.EqualTo(1f).Within(0.01f));
				Assert.That(canvas.PreviewTexture.GetPixel(1, 0).r, Is.EqualTo(0f).Within(0.01f));

				canvas.SetFrame(surface, project, DmdCanvasMode.Dots, true);
				Assert.That(canvas.PreviewTexture.width, Is.EqualTo(16));
				Assert.That(canvas.PreviewTexture.height, Is.EqualTo(8));
				Assert.That(canvas.PreviewTexture.GetPixel(3, 4).r, Is.EqualTo(1f).Within(0.01f));
				Assert.That(canvas.PreviewTexture.GetPixel(0, 0), Is.EqualTo(Color.black));
			} finally {
				canvas.Dispose();
				UnityEngine.Object.DestroyImmediate(project);
			}
		}

		[Test]
		public void PreviewQuantizesMonoProjectsBeforeTintingAndMirroring()
		{
			var rendered = new DmdSurface(4, 1, DmdPixelFormat.I8);
			rendered.Data[0] = 0;
			rendered.Data[1] = 63;
			rendered.Data[2] = 64;
			rendered.Data[3] = 255;
			var preview = new DmdStudioPreviewFrame();

			preview.Prepare(rendered, DmdColorMode.Mono4);

			Assert.That(preview.Format, Is.EqualTo(DisplayFrameFormat.Dmd2));
			Assert.That(preview.DisplayData, Is.EqualTo(new byte[] { 0, 0, 1, 3 }));
			Assert.That(preview.CanvasSurface.Data, Is.EqualTo(new byte[] { 0, 0, 85, 255 }));
		}

		[Test]
		public void SerializedLayerEditIsUndoable()
		{
			var cue = ScriptableObject.CreateInstance<DmdCueAsset>();
			cue.Layers.Add(new TextLayer { Name = "Score", X = 3 });
			AssetDatabase.CreateAsset(cue, $"{TestFolder}/Cue.asset");
			var serialized = new SerializedObject(cue);
			var x = serialized.FindProperty(nameof(DmdCueAsset.Layers)).GetArrayElementAtIndex(0)
				.FindPropertyRelative(nameof(DmdLayer.X));

			x.intValue = 42;
			serialized.ApplyModifiedProperties();
			Assert.That(cue.Layers[0].X, Is.EqualTo(42));
			Undo.PerformUndo();
			serialized.Update();

			Assert.That(cue.Layers[0].X, Is.EqualTo(3));
		}

		[Test]
		public void WindowSerializesProjectSelectionForDomainReload()
		{
			var project = CreateProject(128, 32, DmdColorMode.Mono16);
			var original = ScriptableObject.CreateInstance<DmdStudioWindow>();
			var restored = ScriptableObject.CreateInstance<DmdStudioWindow>();
			try {
				var serialized = new SerializedObject(original);
				serialized.FindProperty("_project").objectReferenceValue = project;
				serialized.FindProperty("_frame").intValue = 17;
				serialized.ApplyModifiedPropertiesWithoutUndo();
				EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(original), restored);
				var restoredState = new SerializedObject(restored);

				Assert.That(restoredState.FindProperty("_project").objectReferenceValue, Is.SameAs(project));
				Assert.That(restoredState.FindProperty("_frame").intValue, Is.EqualTo(17));
			} finally {
				UnityEngine.Object.DestroyImmediate(original);
				UnityEngine.Object.DestroyImmediate(restored);
			}
		}

		[Test]
		public void WindowBuildsItsUxmlEditorSurface()
		{
			var window = ScriptableObject.CreateInstance<DmdStudioWindow>();
			try {
				window.CreateGUI();

				Assert.That(window.rootVisualElement.Q<DmdProjectTreeView>(), Is.Not.Null);
				Assert.That(window.rootVisualElement.Q<DmdCanvasView>(), Is.Not.Null);
				Assert.That(window.rootVisualElement.Q<DmdTimelineView>(), Is.Not.Null);
				Assert.That(window.rootVisualElement.Q("inspector-host"), Is.Not.Null);
				Assert.That(window.rootVisualElement.Q("sample-state-host"), Is.Not.Null);
			} finally {
				UnityEngine.Object.DestroyImmediate(window);
			}
		}

		private DmdProjectAsset CreateProject(int width, int height, DmdColorMode colorMode)
		{
			var project = ScriptableObject.CreateInstance<DmdProjectAsset>();
			project.Width = width;
			project.Height = height;
			project.ColorMode = colorMode;
			AssetDatabase.CreateAsset(project, $"{TestFolder}/Project-{Guid.NewGuid():N}.asset");
			return project;
		}

		private string WritePng(int width, int height, Color32[] bottomOriginPixels, string fileName = null)
		{
			var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
			try {
				texture.SetPixels32(bottomOriginPixels);
				texture.Apply();
				var path = TemporaryPath(fileName ?? $"{Guid.NewGuid():N}.png");
				File.WriteAllBytes(path, texture.EncodeToPNG());
				return path;
			} finally {
				UnityEngine.Object.DestroyImmediate(texture);
			}
		}

		private string WriteText(string contents, string fileName)
		{
			var path = TemporaryPath(fileName);
			File.WriteAllText(path, contents);
			return path;
		}

		private string TemporaryPath(string fileName)
		{
			var folder = Path.Combine(Path.GetTempPath(), $"vpe-dmd-{Guid.NewGuid():N}");
			Directory.CreateDirectory(folder);
			var path = Path.Combine(folder, fileName);
			_temporaryFiles.Add(path);
			return path;
		}
	}
}
