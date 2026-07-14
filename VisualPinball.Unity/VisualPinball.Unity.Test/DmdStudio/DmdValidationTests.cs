// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Test
{
	public class DmdValidationTests
	{
		[Test]
		public void BitmapRejectsWrongPixelAndAlphaLengths()
		{
			var bitmap = new DmdBitmapData {
				Width = 4,
				Height = 2,
				Format = DmdPixelFormat.Rgb24,
				Pixels = new byte[8],
				Alpha = new byte[7]
			};

			var exception = Assert.Throws<DmdValidationException>(() => bitmap.Validate());
			Assert.That(exception.Diagnostics.Select(diagnostic => diagnostic.Code),
				Is.EquivalentTo(new[] { "bitmap.pixels", "bitmap.alpha" }));
		}

		[TestCase(0, 32)]
		[TestCase(1025, 32)]
		[TestCase(128, 0)]
		[TestCase(128, 513)]
		public void BitmapRejectsDimensionsOutsideCaps(int width, int height)
		{
			var bitmap = new DmdBitmapData {
				Width = width,
				Height = height,
				Pixels = Array.Empty<byte>()
			};
			Assert.Throws<DmdValidationException>(() => bitmap.Validate());
		}

		[Test]
		public void SpriteValidationIsPureAndNormalizationIsExplicit()
		{
			var sprite = ScriptableObject.CreateInstance<DmdSpriteAsset>();
			try {
				sprite.Frames.Add(ValidBitmap(2, 2));
				sprite.Frames.Add(ValidBitmap(2, 2));
				sprite.FrameDurations.Add(0);

				var result = sprite.Validate();

				Assert.That(result.IsValid, Is.True);
				Assert.That(result.Diagnostics.Any(diagnostic => diagnostic.Code == "sprite.durations.count"), Is.True);
				Assert.That(sprite.FrameDurations, Is.EqualTo(new[] { 0 }));

				DmdValidation.Normalize(sprite);

				Assert.That(sprite.FrameDurations, Is.EqualTo(new[] { 1, 1 }));
			} finally {
				Object.DestroyImmediate(sprite);
			}
		}

		[Test]
		public void CueRejectsTransitionsLongerThanItsLifetime()
		{
			var cue = ScriptableObject.CreateInstance<DmdCueAsset>();
			try {
				cue.CueId = "award";
				cue.DurationFrames = 10;
				cue.EnterTransition = new DmdTransitionSpec { DurationFrames = 6 };
				cue.ExitTransition = new DmdTransitionSpec { DurationFrames = 5 };
				cue.Layers.Add(new ShapeLayer { Shape = DmdShapeType.Dot });

				var result = cue.Validate();

				Assert.That(result.IsValid, Is.False);
				Assert.That(result.Diagnostics.Any(diagnostic => diagnostic.Code == "cue.transition.total"), Is.True);
			} finally {
				Object.DestroyImmediate(cue);
			}
		}

		[Test]
		public void ProjectClampsFrameRateAndRejectsDuplicateCueIds()
		{
			var project = ScriptableObject.CreateInstance<DmdProjectAsset>();
			var first = ScriptableObject.CreateInstance<DmdCueAsset>();
			var second = ScriptableObject.CreateInstance<DmdCueAsset>();
			try {
				project.FrameRate = 500;
				first.CueId = "same";
				second.CueId = "same";
				first.Layers.Add(new ShapeLayer { Shape = DmdShapeType.Dot });
				second.Layers.Add(new ShapeLayer { Shape = DmdShapeType.Dot });
				project.Cues.Add(first);
				project.Cues.Add(second);

				var result = project.Validate();

				Assert.That(project.FrameRate, Is.EqualTo(500));
				Assert.That(result.IsValid, Is.False);
				Assert.That(result.Diagnostics.Any(diagnostic => diagnostic.Code == "project.frameRate"), Is.True);
				Assert.That(result.Diagnostics.Any(diagnostic => diagnostic.Code == "project.cue.duplicate"), Is.True);

				DmdValidation.Normalize(project);
				Assert.That(project.FrameRate, Is.EqualTo(DmdValidation.MaxFrameRate));
			} finally {
				Object.DestroyImmediate(first);
				Object.DestroyImmediate(second);
				Object.DestroyImmediate(project);
			}
		}

		[Test]
		public void ProjectReportsASharedInvalidSpriteOnlyOnceWithoutMutatingIt()
		{
			var project = ScriptableObject.CreateInstance<DmdProjectAsset>();
			var cue = ScriptableObject.CreateInstance<DmdCueAsset>();
			var sprite = ScriptableObject.CreateInstance<DmdSpriteAsset>();
			try {
				cue.CueId = "shared";
				sprite.Frames.Add(new DmdBitmapData {
					Width = 2,
					Height = 2,
					Format = DmdPixelFormat.I8,
					Pixels = new byte[3]
				});
				sprite.FrameDurations.Add(0);
				cue.Layers.Add(new BitmapLayer { Sprite = sprite });
				cue.Layers.Add(new BitmapLayer { Sprite = sprite });
				project.Cues.Add(cue);
				project.Sprites.Add(sprite);

				var result = project.Validate();

				Assert.That(result.Diagnostics.Count(diagnostic => diagnostic.Code == "bitmap.pixels"), Is.EqualTo(1));
				Assert.That(result.Diagnostics.Count(diagnostic => diagnostic.Code == "sprite.duration.value"), Is.EqualTo(1));
				Assert.That(sprite.FrameDurations[0], Is.EqualTo(0));
			} finally {
				Object.DestroyImmediate(sprite);
				Object.DestroyImmediate(cue);
				Object.DestroyImmediate(project);
			}
		}

		[Test]
		public void ProjectReportsDistinctSameNamedInvalidSpritesSeparately()
		{
			var project = ScriptableObject.CreateInstance<DmdProjectAsset>();
			var first = ScriptableObject.CreateInstance<DmdSpriteAsset>();
			var second = ScriptableObject.CreateInstance<DmdSpriteAsset>();
			try {
				first.name = "logo";
				second.name = "logo";
				first.Frames.Add(new DmdBitmapData { Width = 2, Height = 2, Pixels = new byte[3] });
				second.Frames.Add(new DmdBitmapData { Width = 2, Height = 2, Pixels = new byte[3] });
				project.Sprites.Add(first);
				project.Sprites.Add(second);

				var result = project.Validate();

				Assert.That(result.Diagnostics.Count(diagnostic => diagnostic.Code == "bitmap.pixels"),
					Is.EqualTo(2));
			} finally {
				Object.DestroyImmediate(second);
				Object.DestroyImmediate(first);
				Object.DestroyImmediate(project);
			}
		}

		[Test]
		public void DimensionCapsAcceptTheirInclusiveBoundaries()
		{
			var minimum = ValidBitmap(1, 1);
			var maximum = ValidBitmap(DmdValidation.MaxWidth, DmdValidation.MaxHeight);

			Assert.That(DmdValidation.Validate(minimum).IsValid, Is.True);
			Assert.That(DmdValidation.Validate(maximum).IsValid, Is.True);
		}

		[Test]
		public void SpriteFrameCapAndPaletteSizeAreValidated()
		{
			var sprite = ScriptableObject.CreateInstance<DmdSpriteAsset>();
			var palette = ScriptableObject.CreateInstance<DmdPaletteAsset>();
			try {
				var frame = ValidBitmap(1, 1);
				for (var index = 0; index <= DmdValidation.MaxSpriteFrames; index++) {
					sprite.Frames.Add(frame);
				}
				palette.Colors = new Color32[15];

				Assert.That(sprite.Validate().Diagnostics.Any(diagnostic => diagnostic.Code == "sprite.frames.cap"), Is.True);
				Assert.That(palette.Validate().Diagnostics.Any(diagnostic => diagnostic.Code == "palette.colors"), Is.True);
			} finally {
				Object.DestroyImmediate(palette);
				Object.DestroyImmediate(sprite);
			}
		}

		[Test]
		public void TransitionCapAndLoopExemptionUseInclusiveLimits()
		{
			var cue = ScriptableObject.CreateInstance<DmdCueAsset>();
			try {
				cue.CueId = "loop";
				cue.DurationFrames = 1;
				cue.Loop = true;
				cue.EnterTransition = new DmdTransitionSpec { DurationFrames = DmdValidation.MaxTransitionFrames };
				cue.ExitTransition = new DmdTransitionSpec { DurationFrames = DmdValidation.MaxTransitionFrames };
				cue.Layers.Add(new ShapeLayer { Shape = DmdShapeType.Dot });

				Assert.That(cue.Validate().IsValid, Is.True);

				cue.ExitTransition = new DmdTransitionSpec { DurationFrames = DmdValidation.MaxTransitionFrames + 1 };
				Assert.That(cue.Validate().Diagnostics.Any(diagnostic => diagnostic.Code == "cue.transition.exit"), Is.True);
			} finally {
				Object.DestroyImmediate(cue);
			}
		}

		[Test]
		public void CueParameterCapAndLayerTracksAreValidated()
		{
			var cue = ScriptableObject.CreateInstance<DmdCueAsset>();
			try {
				cue.CueId = "invalid";
				for (var index = 0; index <= DmdValidation.MaxBoundParams; index++) {
					cue.Parameters.Add(new DmdCueParameter { Name = $"p{index}" });
				}
				var layer = new ShapeLayer {
					Shape = DmdShapeType.Rect,
					Opacity = 2f,
					Width = 0,
					Height = 0
				};
				layer.Tracks.Add(new DmdPropertyTrack {
					Property = DmdAnimatableProperty.Opacity,
					Keys = {
						new DmdKeyframe { Frame = 2, Value = 1.5f },
						new DmdKeyframe { Frame = 1, Value = 0.5f }
					}
				});
				layer.Tracks.Add(new DmdPropertyTrack {
					Property = DmdAnimatableProperty.Opacity,
					Keys = { new DmdKeyframe { Frame = 0, Value = 1f } }
				});
				cue.Layers.Add(layer);

				var codes = cue.Validate().Diagnostics.Select(diagnostic => diagnostic.Code).ToArray();

				Assert.That(codes, Does.Contain("cue.parameters.cap"));
				Assert.That(codes, Does.Contain("layer.opacity"));
				Assert.That(codes, Does.Contain("layer.shape.size"));
				Assert.That(codes, Does.Contain("layer.track.order"));
				Assert.That(codes, Does.Contain("layer.track.opacity"));
				Assert.That(codes, Does.Contain("layer.track.duplicate"));
			} finally {
				Object.DestroyImmediate(cue);
			}
		}

		[Test]
		public void ProjectValidatesBoundsSamplesAndOrphanReferences()
		{
			var project = ScriptableObject.CreateInstance<DmdProjectAsset>();
			var cue = ScriptableObject.CreateInstance<DmdCueAsset>();
			var sprite = ScriptableObject.CreateInstance<DmdSpriteAsset>();
			try {
				project.Width = DmdValidation.MaxWidth + 1;
				project.FrameRate = 0;
				cue.CueId = "orphan";
				sprite.Frames.Add(ValidBitmap(1, 1));
				cue.Layers.Add(new BitmapLayer { Sprite = sprite });
				project.Cues.Add(cue);
				project.SampleStates.Add(new DmdSampleState {
					Name = "Bad",
					Values = { DmdParamValue.From("bad name", 1L) }
				});

				var codes = project.Validate().Diagnostics.Select(diagnostic => diagnostic.Code).ToArray();

				Assert.That(project.FrameRate, Is.EqualTo(0));
				Assert.That(codes, Does.Contain("project.width"));
				Assert.That(codes, Does.Contain("project.frameRate"));
				Assert.That(codes, Does.Contain("project.sprite.orphan"));
				Assert.That(codes, Does.Contain("project.sample.name"));
				DmdValidation.Normalize(project);
				Assert.That(project.FrameRate, Is.EqualTo(DmdValidation.MinFrameRate));
			} finally {
				Object.DestroyImmediate(sprite);
				Object.DestroyImmediate(cue);
				Object.DestroyImmediate(project);
			}
		}

		[Test]
		public void FontRejectsGlyphRectOutsideAtlas()
		{
			var font = ScriptableObject.CreateInstance<DmdFontAsset>();
			try {
				font.Atlas = ValidBitmap(4, 4);
				font.LineHeight = 5;
				font.Baseline = 4;
				font.Glyphs.Add(new DmdGlyph { Codepoint = 'A', X = 3, Y = 0, W = 2, H = 4, Advance = 3 });

				var result = font.Validate();

				Assert.That(result.IsValid, Is.False);
				Assert.That(result.Diagnostics.Any(diagnostic => diagnostic.Code == "font.glyph.rect"), Is.True);
			} finally {
				Object.DestroyImmediate(font);
			}
		}

		private static DmdBitmapData ValidBitmap(int width, int height)
		{
			return new DmdBitmapData {
				Width = width,
				Height = height,
				Format = DmdPixelFormat.I8,
				Pixels = new byte[width * height]
			};
		}
	}
}
