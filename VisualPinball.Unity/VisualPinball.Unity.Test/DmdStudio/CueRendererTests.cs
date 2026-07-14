// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Test
{
	public class CueRendererTests
	{
		private DmdProjectAsset _project;
		private DmdCueAsset _cue;
		private DmdFontAsset _font;

		[SetUp]
		public void SetUp()
		{
			_project = ScriptableObject.CreateInstance<DmdProjectAsset>();
			_project.Width = 6;
			_project.Height = 5;
			_project.FrameRate = 10;
			_cue = ScriptableObject.CreateInstance<DmdCueAsset>();
			_cue.CueId = "render";
			_font = DmdTestFont.Create();
		}

		[TearDown]
		public void TearDown()
		{
			Object.DestroyImmediate(_font);
			Object.DestroyImmediate(_cue);
			Object.DestroyImmediate(_project);
		}

		[Test]
		public void LayersRenderBottomToTopAndTracksOverrideStaticValues()
		{
			_cue.Layers.Add(new ShapeLayer {
				Shape = DmdShapeType.Rect, Width = 6, Height = 1, Filled = true,
				Shade = new DmdShade { Intensity = 100 }
			});
			var sprite = Sprite(new byte[] { 200, 220 }, 2, 1);
			_cue.Layers.Add(new BitmapLayer {
				Sprite = sprite, X = 5, Blend = DmdBlendMode.Opaque, Loop = DmdLoopMode.HoldLast,
				Tracks = { new DmdPropertyTrack {
					Property = DmdAnimatableProperty.X,
					Keys = { new DmdKeyframe { Frame = 0, Value = 0, Interp = DmdInterpolation.Linear },
						new DmdKeyframe { Frame = 2, Value = 2 } }
				} }
			});

			var surface = Surface();
			new CueRenderer(_project).Render(surface, _cue, 1, null, new CueInstanceState(), new CueDiagnostics());

			Assert.That(surface.Data, Is.EqualTo(new byte[] {
				100, 200, 220, 100, 100, 100,
				0, 0, 0, 0, 0, 0,
				0, 0, 0, 0, 0, 0,
				0, 0, 0, 0, 0, 0,
				0, 0, 0, 0, 0, 0,
			}));
			Object.DestroyImmediate(sprite);
		}

		[Test]
		public void MaskUsesTheSharedClockAndClearsOutsideItsBounds()
		{
			_cue.Layers.Add(new ShapeLayer {
				Shape = DmdShapeType.Rect, Width = 6, Height = 5, Filled = true,
				Shade = DmdShade.White
			});
			var sprite = ScriptableObject.CreateInstance<DmdSpriteAsset>();
			sprite.Frames.Add(new DmdBitmapData { Width = 2, Height = 1, Pixels = new byte[] { 255, 0 } });
			sprite.Frames.Add(new DmdBitmapData { Width = 2, Height = 1, Pixels = new byte[] { 0, 255 } });
			_cue.Layers.Add(new MaskLayer { Mask = sprite, X = 2, Y = 1, Loop = DmdLoopMode.Loop });

			var surface = Surface();
			new CueRenderer(_project).Render(surface, _cue, 1, null, new CueInstanceState(), new CueDiagnostics());

			Assert.That(surface.Data[1 * 6 + 2], Is.Zero);
			Assert.That(surface.Data[1 * 6 + 3], Is.EqualTo(255));
			Assert.That(surface.Data[0], Is.Zero);
			Object.DestroyImmediate(sprite);
		}

		[Test]
		public void TextBindingsAreCachedAndRefreshWhenAParameterChanges()
		{
			_cue.Layers.Add(new TextLayer { Font = _font, Text = "{letter}", Blend = DmdBlendMode.Alpha });
			var state = new CueInstanceState();
			var parameters = new DmdParams().Set("letter", "A");
			var renderer = new CueRenderer(_project);
			var surface = Surface();

			renderer.Render(surface, _cue, 0, parameters, state, new CueDiagnostics());
			var first = DmdSurfaceAssert.Hash(surface.Data);
			var resolved = state.ResolvedTexts[0];
			renderer.Render(surface, _cue, 1, parameters, state, new CueDiagnostics());
			Assert.That(state.ResolvedTexts[0], Is.SameAs(resolved));
			parameters.Set("letter", "B");
			renderer.Render(surface, _cue, 2, parameters, state, new CueDiagnostics());
			Assert.That(DmdSurfaceAssert.Hash(surface.Data), Is.Not.EqualTo(first));
		}

		[Test]
		public void NumberTweenRetargetsFromTheDisplayedValueDeterministically()
		{
			_cue.Layers.Add(new NumberLayer {
				Font = _font, ParamName = "value", Format = "0", CountUpSeconds = 1f
			});
			var state = new CueInstanceState();
			var parameters = new DmdParams().Set("value", 100d);
			var renderer = new CueRenderer(_project);

			renderer.Render(Surface(), _cue, 0, parameters, state, new CueDiagnostics());
			renderer.Render(Surface(), _cue, 5, parameters, state, new CueDiagnostics());
			parameters.Set("value", 200d);
			renderer.Render(Surface(), _cue, 5, parameters, state, new CueDiagnostics());

			Assert.That(state.NumberTweens[0].StartValue, Is.EqualTo(50d));
			Assert.That(state.NumberTweens[0].TargetValue, Is.EqualTo(200d));
			Assert.That(state.NumberTweens[0].StartTick, Is.EqualTo(5));
		}

		[Test]
		public void MalformedAuthoredAssetsNeverEscapeRenderAndReportOnce()
		{
			var sprite = Sprite(new byte[] { 1 }, 2, 1);
			_cue.Layers.Add(new BitmapLayer { Sprite = sprite });
			var diagnostics = new CueDiagnostics();
			var renderer = new CueRenderer(_project);

			Assert.DoesNotThrow(() => renderer.Render(Surface(), _cue, 0, null, new CueInstanceState(), diagnostics));
			Assert.DoesNotThrow(() => renderer.Render(Surface(), _cue, 0, null, new CueInstanceState(), diagnostics));
			Assert.That(diagnostics.Count, Is.EqualTo(1));
			Object.DestroyImmediate(sprite);
		}

		[Test]
		public void LayerSpanAndLastDuplicateTrackAreDeterministic()
		{
			var layer = new ShapeLayer {
				Shape = DmdShapeType.Dot, StartFrame = 2, EndFrame = 4, X = 0, Shade = DmdShade.White
			};
			layer.Tracks.Add(new DmdPropertyTrack {
				Property = DmdAnimatableProperty.X,
				Keys = { new DmdKeyframe { Frame = 0, Value = 1 } }
			});
			layer.Tracks.Add(new DmdPropertyTrack {
				Property = DmdAnimatableProperty.X,
				Keys = { new DmdKeyframe { Frame = 0, Value = 3 } }
			});
			_cue.Layers.Add(layer);
			var renderer = new CueRenderer(_project);
			var diagnostics = new CueDiagnostics();

			var before = Surface();
			renderer.Render(before, _cue, 1, null, new CueInstanceState(), diagnostics);
			var during = Surface();
			renderer.Render(during, _cue, 2, null, new CueInstanceState(), diagnostics);
			var after = Surface();
			renderer.Render(after, _cue, 4, null, new CueInstanceState(), diagnostics);

			Assert.That(before.Data, Is.All.EqualTo(0));
			Assert.That(during.Data[3], Is.EqualTo(255));
			Assert.That(after.Data, Is.All.EqualTo(0));
			Assert.That(diagnostics.Count, Is.EqualTo(1));
			Assert.That(diagnostics.Diagnostics[0].Code, Is.EqualTo("layer.track.duplicate"));
		}

		[Test]
		public void ShapeLayerSupportsFilledOutlineLinesAndDot()
		{
			_cue.Layers.Add(new ShapeLayer { Shape = DmdShapeType.Rect, Width = 4, Height = 4,
				X = 1, Filled = false, Shade = DmdShade.White });
			_cue.Layers.Add(new ShapeLayer { Shape = DmdShapeType.Dot, X = 2, Y = 2,
				Shade = new DmdShade { Intensity = 128 } });
			var surface = Surface();
			new CueRenderer(_project).Render(surface, _cue, 0, null, new CueInstanceState(), new CueDiagnostics());

			Assert.That(surface.Data[0 * 6 + 1], Is.EqualTo(255));
			Assert.That(surface.Data[1 * 6 + 2], Is.Zero);
			Assert.That(surface.Data[2 * 6 + 2], Is.EqualTo(128));
			Assert.That(surface.Data[3 * 6 + 4], Is.EqualTo(255));
		}

		[Test]
		public void StaticRendererHasNoSteadyStateManagedAllocations()
		{
			_cue.Layers.Add(new ShapeLayer {
				Shape = DmdShapeType.Rect, Width = 6, Height = 5, Filled = true, Shade = DmdShade.White
			});
			var surface = Surface();
			var renderer = new CueRenderer(_project);
			var state = new CueInstanceState();
			var diagnostics = new CueDiagnostics();
			renderer.Render(surface, _cue, 0, null, state, diagnostics);

			var before = GC.GetAllocatedBytesForCurrentThread();
			for (var frame = 1; frame <= 100; frame++) {
				renderer.Render(surface, _cue, frame, null, state, diagnostics);
			}
			var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

			Assert.That(allocated, Is.Zero);
		}

		private DmdSurface Surface() => new DmdSurface(_project.Width, _project.Height, DmdPixelFormat.I8);

		private static DmdSpriteAsset Sprite(byte[] pixels, int width, int height)
		{
			var sprite = ScriptableObject.CreateInstance<DmdSpriteAsset>();
			sprite.Frames.Add(new DmdBitmapData { Width = width, Height = height, Pixels = pixels });
			return sprite;
		}
	}
}
