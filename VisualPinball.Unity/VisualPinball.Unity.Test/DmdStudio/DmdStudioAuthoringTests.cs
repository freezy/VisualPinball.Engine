// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using VisualPinball.Unity.Editor;

namespace VisualPinball.Unity.Test
{
	public class DmdStudioAuthoringTests
	{
		private readonly List<UnityEngine.Object> _owned = new List<UnityEngine.Object>();

		[TearDown]
		public void TearDown()
		{
			foreach (var item in _owned) {
				if (item != null) UnityEngine.Object.DestroyImmediate(item);
			}
			_owned.Clear();
		}

		[Test]
		public void TimelineEditsKeysAndSpriteDurationsWithRuntimeConstraints()
		{
			var cue = Own<DmdCueAsset>();
			cue.DurationFrames = 30;
			cue.Layers.Add(new BitmapLayer { StartFrame = 2, EndFrame = 20 });
			var sprite = Own<DmdSpriteAsset>();
			sprite.Frames.Add(Bitmap(2, 1));
			sprite.Frames.Add(Bitmap(2, 1));
			var timeline = new DmdTimelineView();
			timeline.SetCue(cue, 30, 0);

			Assert.That(timeline.AddKeyframe(0, DmdAnimatableProperty.X, 7, 12f), Is.True);
			Assert.That(timeline.AddKeyframe(0, DmdAnimatableProperty.X, 7, 18f), Is.True);
			Assert.That(cue.Layers[0].Tracks, Has.Count.EqualTo(1));
			Assert.That(cue.Layers[0].Tracks[0].Keys, Has.Count.EqualTo(1));
			Assert.That(cue.Layers[0].Tracks[0].Keys[0].Value, Is.EqualTo(18f));
			Assert.That(timeline.SetLayerSpan(0, 5, 24), Is.True);
			Assert.That((cue.Layers[0].StartFrame, cue.Layers[0].EndFrame), Is.EqualTo((5, 24)));
			cue.ExitTransition = new DmdTransitionSpec { DurationFrames = 8 };
			Assert.That(timeline.SetTransitionDuration(true, 28), Is.True);
			Assert.That(cue.EnterTransition.DurationFrames, Is.EqualTo(22),
				"finite cue transition durations may not overlap");
			Assert.That(timeline.SetSpriteFrameDuration(sprite, 1, 0), Is.True);
			Assert.That(sprite.FrameDurations, Is.EqualTo(new[] { 1, 1 }));
		}

		[Test]
		public void PixelEditorToolsRespectTopOriginRegionAndTransparency()
		{
			var project = Own<DmdProjectAsset>();
			project.ColorMode = DmdColorMode.Mono16;
			var sprite = Own<DmdSpriteAsset>();
			var bitmap = Bitmap(4, 3);
			sprite.Frames.Add(bitmap);
			var editor = new DmdPixelEditorView();
			editor.SetTarget(sprite, bitmap, new RectInt(1, 1, 2, 2), project, "region");

			editor.SetBrush(15, false);
			Assert.That(editor.ApplyTool(DmdPixelTool.Pencil, new Vector2Int(0, 0), new Vector2Int(0, 0)), Is.True);
			Assert.That(bitmap.Pixels[1 + bitmap.Width], Is.EqualTo(255));
			editor.SetBrush(0, false);
			editor.ApplyTool(DmdPixelTool.Rectangle, Vector2Int.zero, Vector2Int.one);
			Assert.That(bitmap.Pixels[1 + bitmap.Width], Is.Zero);
			Assert.That(bitmap.Pixels[2 + bitmap.Width * 2], Is.Zero);
			editor.SetBrush(15, true);
			editor.ApplyTool(DmdPixelTool.Fill, Vector2Int.zero, Vector2Int.zero);
			Assert.That(bitmap.Alpha[1 + bitmap.Width], Is.Zero);
			Assert.That(bitmap.Alpha[0], Is.EqualTo(255), "fill must stay inside the glyph region");
		}

		[Test]
		public void AuthoringValidationCombinesShapeBindingAndColorWarnings()
		{
			var project = Own<DmdProjectAsset>();
			project.Width = 8;
			project.Height = 4;
			project.ColorMode = DmdColorMode.Mono4;
			var sprite = Own<DmdSpriteAsset>();
			sprite.name = "large";
			sprite.Frames.Add(new DmdBitmapData {
				Width = 9, Height = 4, Format = DmdPixelFormat.I8,
				Pixels = Enumerable.Range(0, 36).Select(index => (byte)(index * 7)).ToArray(),
				Alpha = Enumerable.Repeat((byte)255, 36).ToArray()
			});
			project.Sprites.Add(sprite);
			var cue = Own<DmdCueAsset>();
			cue.name = "binding";
			cue.CueId = "binding";
			cue.DurationFrames = 10;
			cue.Layers.Add(new TextLayer { Text = "{score:Q9}" });
			project.Cues.Add(cue);

			var diagnostics = DmdStudioValidation.Validate(project);
			Assert.That(diagnostics.Select(item => item.Code), Does.Contain("sprite.canvas.size"));
			Assert.That(diagnostics.Select(item => item.Code), Does.Contain("sprite.shades.overflow"));
			Assert.That(diagnostics.Select(item => item.Code), Does.Contain("binding.undeclared"));
		}

		[Test]
		public void SimulatorMatchesMultiballPreemptionAndResumeLanes()
		{
			var project = Own<DmdProjectAsset>();
			project.Width = 4;
			project.Height = 2;
			project.FrameRate = 10;
			var attract = Cue("attract", CuePriority.Base, 0, true);
			var multiball = Cue("multiball", CuePriority.Mode, 20);
			multiball.Return = CueReturnPolicy.Resume;
			var jackpot = Cue("jackpot", CuePriority.Critical, 2);
			jackpot.Parameters.Add(new DmdCueParameter { Name = "value", Type = DmdParamType.Integer });
			project.Cues.AddRange(new[] { attract, multiball, jackpot });

			var result = DmdCueSimulator.Run(project,
				"t=0 SetBase(attract)\nt=0 Play(multiball)\nt=0.4 Play(jackpot, value=100000)", 1.2);

			Assert.That(result.Errors, Is.Empty);
			Assert.That(result.Samples[0].Snapshot.BaseCueId, Is.EqualTo("attract"));
			Assert.That(result.Samples[0].Snapshot.ActiveCueId, Is.EqualTo("multiball"));
			var preempted = result.Samples.First(sample => sample.Time >= 0.4 &&
				sample.Snapshot.ActiveCueId == "jackpot");
			Assert.That(preempted.Snapshot.HoldStackCueIds, Does.Contain("multiball"));
			Assert.That(result.Samples.Any(sample => sample.Time > preempted.Time &&
				sample.Snapshot.ActiveCueId == "multiball" && sample.Snapshot.HoldStackCueIds.Length == 0), Is.True);
		}

		[Test]
		public void StarterFontsAreValidAttributedAndCoverRequiredGlyphs()
		{
			var fonts = DmdStarterFontLibrary.LoadAll();
			var required = Enumerable.Range(0x20, 0x7f - 0x20)
				.Concat(new[] { 0x00a9, 0x00ae, 0x2122, 0x2022, 0x00d7 }).ToArray();
			Assert.That(fonts, Has.Count.EqualTo(4));
			foreach (var font in fonts) {
				Assert.That(font.Validate().IsValid, Is.True, font.name);
				Assert.That(font.Notes, Does.Contain("Open Font License 1.1"));
				Assert.That(font.DigitWidth, Is.GreaterThan(0));
				Assert.That(required.All(codepoint => font.Glyphs.Any(glyph => glyph.Codepoint == codepoint)),
					Is.True, font.name);
				Assert.That(Enumerable.Range('0', 10).Select(codepoint =>
					font.Glyphs.First(glyph => glyph.Codepoint == codepoint).Advance).Distinct().ToArray(),
					Has.Length.EqualTo(1));
				var surface = new DmdSurface(128, 32, DmdPixelFormat.I8);
				DmdTextRenderer.Draw(surface, font, "0123456789", 0, 0, DmdAnchor.TopLeft,
					DmdTextEffect.None, new DmdShade { Intensity = 255 }, new DmdShade(),
					DmdBlendMode.Alpha, new CueDiagnostics());
				Assert.That(surface.Data.Any(value => value != 0), Is.True, $"{font.name} renders blank");
			}
		}

		private DmdCueAsset Cue(string id, CuePriority priority, int duration, bool loop = false)
		{
			var cue = Own<DmdCueAsset>();
			cue.CueId = id;
			cue.Priority = priority;
			cue.DurationFrames = duration;
			cue.Loop = loop;
			cue.Layers.Add(new ShapeLayer {
				Shape = DmdShapeType.Rect, Width = 4, Height = 2, Filled = true,
				Shade = new DmdShade { Intensity = 255 }
			});
			return cue;
		}

		private static DmdBitmapData Bitmap(int width, int height)
		{
			return new DmdBitmapData {
				Width = width, Height = height, Format = DmdPixelFormat.I8,
				Pixels = new byte[width * height], Alpha = Enumerable.Repeat((byte)255, width * height).ToArray()
			};
		}

		private T Own<T>() where T : ScriptableObject
		{
			var item = ScriptableObject.CreateInstance<T>();
			_owned.Add(item);
			return item;
		}
	}
}
