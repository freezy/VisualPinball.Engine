// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Globalization;
using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity
{
	public sealed class CueRenderer
	{
		private readonly DmdProjectAsset _project;

		public CueRenderer(DmdProjectAsset project)
		{
			_project = project ?? throw new ArgumentNullException(nameof(project));
		}

		public void Render(DmdSurface destination, DmdCueAsset cue, int frame, DmdParams parameters,
			CueInstanceState state, CueDiagnostics diagnostics)
		{
			if (destination == null) {
				throw new ArgumentNullException(nameof(destination));
			}
			if (cue == null) {
				throw new ArgumentNullException(nameof(cue));
			}
			if (state == null) {
				throw new ArgumentNullException(nameof(state));
			}
			if (frame < 0) {
				throw new ArgumentOutOfRangeException(nameof(frame));
			}
			var expectedFormat = _project.ColorMode == DmdColorMode.Rgb24
				? DmdPixelFormat.Rgb24
				: DmdPixelFormat.I8;
			if (destination.Width != _project.Width || destination.Height != _project.Height ||
			    destination.Format != expectedFormat) {
				throw new ArgumentException("The destination surface does not match the DMD project.", nameof(destination));
			}

			destination.Clear();
			if (cue.Layers == null) {
				diagnostics?.AuthoredContent("cue.layers.null", "Cue layer collection is missing.");
				return;
			}
			state.EnsureLayerCount(cue.Layers.Count);
			for (var layerIndex = 0; layerIndex < cue.Layers.Count; layerIndex++) {
				var layer = cue.Layers[layerIndex];
				if (layer == null) {
					diagnostics?.AuthoredContent("cue.layer.null", $"Layer {layerIndex} is missing.");
					continue;
				}
				try {
					RenderLayer(destination, layer, layerIndex, frame, parameters, state, diagnostics);
				} catch (ArgumentException exception) {
					diagnostics?.AuthoredContent("layer.malformed", $"Layer {layerIndex}: {exception.Message}");
				} catch (IndexOutOfRangeException exception) {
					diagnostics?.AuthoredContent("layer.malformed", $"Layer {layerIndex}: {exception.Message}");
				} catch (OverflowException exception) {
					diagnostics?.AuthoredContent("layer.malformed", $"Layer {layerIndex}: {exception.Message}");
				}
			}
		}

		internal bool IsAnimated(DmdCueAsset cue, int frame, CueInstanceState state)
		{
			if (cue?.Layers == null) {
				return false;
			}
			for (var layerIndex = 0; layerIndex < cue.Layers.Count; layerIndex++) {
				var layer = cue.Layers[layerIndex];
				if (layer == null || !layer.Visible) {
					continue;
				}
				if (cue.Loop && cue.DurationFrames > 0 && LayerChangesOverTime(layer)) {
					return true;
				}
				if (frame == layer.StartFrame || layer.EndFrame > 0 && frame == layer.EndFrame) {
					return true;
				}
				if (layer.Tracks != null) {
					foreach (var track in layer.Tracks) {
						if (track?.Keys != null && track.Keys.Count > 1 &&
						    frame <= track.Keys[track.Keys.Count - 1].Frame) {
							return true;
						}
					}
				}
				if (layer is NumberLayer number && NumberIsAnimating(number, layerIndex, frame, state) ||
				    layer is TextLayer text && text.Overflow == DmdOverflow.Marquee && text.MarqueeSpeed > 0 ||
				    layer is BitmapLayer bitmap && SpriteIsAnimating(bitmap.Sprite, bitmap.Loop,
					    frame - layer.StartFrame) ||
				    layer is MaskLayer mask && SpriteIsAnimating(mask.Mask, mask.Loop,
					    frame - layer.StartFrame)) {
					return true;
				}
			}
			return false;
		}

		private void RenderLayer(DmdSurface destination, DmdLayer layer, int layerIndex, int frame,
			DmdParams parameters, CueInstanceState state, CueDiagnostics diagnostics)
		{
			if (!layer.Visible || frame < layer.StartFrame || layer.EndFrame != 0 && frame >= layer.EndFrame) {
				return;
			}

			var x = EvaluateInt(layer, DmdAnimatableProperty.X, layer.X, frame, diagnostics);
			var y = EvaluateInt(layer, DmdAnimatableProperty.Y, layer.Y, frame, diagnostics);
			var opacity = ToOpacity(Evaluate(layer, DmdAnimatableProperty.Opacity, layer.Opacity, frame, diagnostics));
			if (opacity == 0) {
				return;
			}

			switch (layer) {
				case NumberLayer number:
					RenderNumber(destination, number, layerIndex, x, y, opacity, frame, parameters, state, diagnostics);
					break;
				case TextLayer text:
					RenderText(destination, text, layerIndex, x, y, opacity, frame, parameters, state, diagnostics);
					break;
				case BitmapLayer bitmap:
					RenderBitmap(destination, bitmap, x, y, opacity, frame, diagnostics);
					break;
				case MaskLayer mask:
					RenderMask(destination, mask, x, y, opacity, frame, diagnostics);
					break;
				case ShapeLayer shape:
					RenderShape(destination, shape, x, y, opacity);
					break;
				default:
					diagnostics?.AuthoredContent("layer.type", $"Unsupported layer type {layer.GetType().FullName}.");
					break;
			}
		}

		private static void RenderBitmap(DmdSurface destination, BitmapLayer layer, int x, int y,
			byte opacity, int frame, CueDiagnostics diagnostics)
		{
			if (!TryGetSpriteFrame(layer.Sprite, layer, frame, layer.Loop, layer.SpriteStartFrame,
				out var bitmap, diagnostics)) {
				return;
			}
			DmdBlitter.Blit(destination, bitmap, x, y, layer.Blend, opacity, layer.Tint);
		}

		private static void RenderMask(DmdSurface destination, MaskLayer layer, int x, int y,
			byte opacity, int frame, CueDiagnostics diagnostics)
		{
			if (!TryGetSpriteFrame(layer.Mask, layer, frame, layer.Loop, layer.SpriteStartFrame,
				out var bitmap, diagnostics)) {
				return;
			}
			DmdBlitter.ApplyAlphaMask(destination, bitmap, x, y, opacity);
		}

		private static bool TryGetSpriteFrame(DmdSpriteAsset sprite, DmdLayer layer, int frame,
			DmdLoopMode loop, int startFrame, out DmdBitmapData bitmap, CueDiagnostics diagnostics)
		{
			bitmap = null;
			if (sprite?.Frames == null || sprite.Frames.Count == 0) {
				diagnostics?.AuthoredContent("sprite.missing", "Layer sprite is missing or has no frames.");
				return false;
			}
			int frameIndex;
			if (TryEvaluate(layer, DmdAnimatableProperty.SpriteFrame, frame, diagnostics, out var trackedFrame)) {
				frameIndex = math.clamp((int)math.round(trackedFrame), 0, sprite.Frames.Count - 1);
			} else if (!SpriteFrameClock.TryGetFrame(sprite, frame - layer.StartFrame, loop, startFrame,
				out frameIndex)) {
				return false;
			}
			bitmap = sprite.Frames[frameIndex];
			if (!IsReadable(bitmap)) {
				diagnostics?.AuthoredContent("sprite.frame.malformed", $"Sprite frame {frameIndex} is malformed.");
				bitmap = null;
				return false;
			}
			return true;
		}

		private void RenderText(DmdSurface destination, TextLayer layer, int layerIndex, int x, int y,
			byte opacity, int frame, DmdParams parameters, CueInstanceState state, CueDiagnostics diagnostics)
		{
			if (!ReferenceEquals(state.TextTemplates[layerIndex], layer.Text) &&
			    !string.Equals(state.TextTemplates[layerIndex], layer.Text, StringComparison.Ordinal)) {
				state.TextTemplates[layerIndex] = layer.Text;
				state.BoundTexts[layerIndex] = BoundText.Parse(layer.Text);
				state.TextInitialized[layerIndex] = false;
			}
			var bound = state.BoundTexts[layerIndex];
			var version = bound.Version(parameters);
			if (!state.TextInitialized[layerIndex] || state.TextVersions[layerIndex] != version) {
				state.ResolvedTexts[layerIndex] = bound.Resolve(parameters, diagnostics);
				state.TextVersions[layerIndex] = version;
				state.TextInitialized[layerIndex] = true;
			}
			DrawText(destination, layer, state.ResolvedTexts[layerIndex], x, y, opacity, frame, diagnostics);
		}

		private void RenderNumber(DmdSurface destination, NumberLayer layer, int layerIndex, int x, int y,
			byte opacity, int frame, DmdParams parameters, CueInstanceState state, CueDiagnostics diagnostics)
		{
			if (parameters == null || string.IsNullOrEmpty(layer.ParamName) ||
			    !parameters.TryGet(layer.ParamName, out var parameter) ||
			    parameter.Type != DmdParamType.Integer && parameter.Type != DmdParamType.Float) {
				diagnostics?.MissingParameter(layer.ParamName ?? string.Empty);
				return;
			}
			var target = parameter.Type == DmdParamType.Integer ? parameter.IntValue : parameter.FloatValue;
			var tweenTicks = math.max(0, (int)math.round(layer.CountUpSeconds * _project.FrameRate));
			ref var tween = ref state.NumberTweens[layerIndex];
			if (!tween.Initialized) {
				tween.StartValue = tweenTicks == 0 ? target : 0d;
				tween.TargetValue = target;
				tween.StartTick = frame;
				tween.Initialized = true;
			} else if (!target.Equals(tween.TargetValue)) {
				tween.StartValue = CurrentValue(tween, tweenTicks, frame);
				tween.TargetValue = target;
				tween.StartTick = frame;
			}
			var value = CurrentValue(tween, tweenTicks, frame);
			var buffer = state.NumberBuffers[layerIndex] ??
			             (state.NumberBuffers[layerIndex] = new char[DmdValidation.MaxResolvedTextLength]);
			int length;
			try {
				if (!value.TryFormat(buffer.AsSpan(), out length, layer.Format, CultureInfo.InvariantCulture)) {
					diagnostics?.TextTruncated();
					if (!value.TryFormat(buffer.AsSpan(), out length, default, CultureInfo.InvariantCulture)) {
						length = 0;
					}
				}
			} catch (FormatException) {
				diagnostics?.InvalidFormat(layer.ParamName, layer.Format);
				value.TryFormat(buffer.AsSpan(), out length, default, CultureInfo.InvariantCulture);
			}
			DmdTextRenderer.Draw(destination, layer.Font, buffer, length, x, y, layer.Anchor, layer.Effect,
				layer.Shade, layer.OutlineShade, layer.Blend, opacity, diagnostics);
		}

		private void DrawText(DmdSurface destination, TextLayer layer, string text, int x, int y,
			byte opacity, int frame, CueDiagnostics diagnostics)
		{
			if (layer.Overflow != DmdOverflow.Marquee || layer.MarqueeSpeed <= 0 || layer.Font == null ||
			    DmdTextRenderer.Measure(layer.Font, text) <= destination.Width) {
				DmdTextRenderer.Draw(destination, layer.Font, text, x, y, layer.Anchor, layer.Effect,
					layer.Shade, layer.OutlineShade, layer.Blend, opacity, diagnostics);
				return;
			}
			var textWidth = DmdTextRenderer.Measure(layer.Font, text);
			var period = math.max(1, textWidth + destination.Width / 4);
			var offset = (int)((long)frame * layer.MarqueeSpeed / math.max(1, _project.FrameRate) % period);
			var originX = ResolveTextLeft(x, textWidth, layer.Anchor);
			var leftAnchor = ToLeftAnchor(layer.Anchor);
			DmdTextRenderer.Draw(destination, layer.Font, text, originX - offset, y, leftAnchor, layer.Effect,
				layer.Shade, layer.OutlineShade, layer.Blend, opacity, diagnostics);
			DmdTextRenderer.Draw(destination, layer.Font, text, originX - offset + period, y, leftAnchor,
				layer.Effect, layer.Shade, layer.OutlineShade, layer.Blend, opacity, diagnostics);
		}

		private static int ResolveTextLeft(int x, int width, DmdAnchor anchor)
		{
			switch (anchor) {
				case DmdAnchor.TopCenter:
				case DmdAnchor.MiddleCenter:
				case DmdAnchor.BottomCenter:
				case DmdAnchor.BaselineCenter:
					return x - width / 2;
				case DmdAnchor.TopRight:
				case DmdAnchor.MiddleRight:
				case DmdAnchor.BottomRight:
				case DmdAnchor.BaselineRight:
					return x - width;
				default:
					return x;
			}
		}

		private static DmdAnchor ToLeftAnchor(DmdAnchor anchor)
		{
			switch (anchor) {
				case DmdAnchor.MiddleLeft:
				case DmdAnchor.MiddleCenter:
				case DmdAnchor.MiddleRight:
					return DmdAnchor.MiddleLeft;
				case DmdAnchor.BottomLeft:
				case DmdAnchor.BottomCenter:
				case DmdAnchor.BottomRight:
					return DmdAnchor.BottomLeft;
				case DmdAnchor.BaselineLeft:
				case DmdAnchor.BaselineCenter:
				case DmdAnchor.BaselineRight:
					return DmdAnchor.BaselineLeft;
				default:
					return DmdAnchor.TopLeft;
			}
		}

		private static void RenderShape(DmdSurface destination, ShapeLayer layer, int x, int y, byte opacity)
		{
			switch (layer.Shape) {
				case DmdShapeType.Dot:
					DmdBlitter.FillRect(destination, x, y, 1, 1, layer.Shade, layer.Blend, opacity);
					break;
				case DmdShapeType.HLine:
					DmdBlitter.FillRect(destination, x, y, layer.Width, math.max(1, layer.Height), layer.Shade,
						layer.Blend, opacity);
					break;
				case DmdShapeType.VLine:
					DmdBlitter.FillRect(destination, x, y, math.max(1, layer.Width), layer.Height, layer.Shade,
						layer.Blend, opacity);
					break;
				case DmdShapeType.Rect:
					if (layer.Filled) {
						DmdBlitter.FillRect(destination, x, y, layer.Width, layer.Height, layer.Shade, layer.Blend,
							opacity);
					} else {
						DmdBlitter.FillRect(destination, x, y, layer.Width, 1, layer.Shade, layer.Blend, opacity);
						DmdBlitter.FillRect(destination, x, y + layer.Height - 1, layer.Width, 1, layer.Shade,
							layer.Blend, opacity);
						DmdBlitter.FillRect(destination, x, y + 1, 1, layer.Height - 2, layer.Shade, layer.Blend,
							opacity);
						DmdBlitter.FillRect(destination, x + layer.Width - 1, y + 1, 1, layer.Height - 2,
							layer.Shade, layer.Blend, opacity);
					}
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(layer.Shape));
			}
		}

		private static double CurrentValue(CueInstanceState.NumberTweenState tween, int tweenTicks, int frame)
		{
			if (tweenTicks <= 0) {
				return tween.TargetValue;
			}
			var progress = math.saturate((double)(frame - tween.StartTick) / tweenTicks);
			return tween.StartValue + (tween.TargetValue - tween.StartValue) * progress;
		}

		private bool NumberIsAnimating(NumberLayer layer, int layerIndex, int frame, CueInstanceState state)
		{
			var ticks = math.max(0, (int)math.round(layer.CountUpSeconds * _project.FrameRate));
			if (ticks <= 0 || state == null || layerIndex >= state.NumberTweens.Length) {
				return false;
			}
			var tween = state.NumberTweens[layerIndex];
			return !tween.Initialized || !tween.StartValue.Equals(tween.TargetValue) &&
			       frame - tween.StartTick <= ticks;
		}

		private static bool SpriteIsAnimating(DmdSpriteAsset sprite, DmdLoopMode loop, int localFrame)
		{
			if (sprite?.Frames == null || sprite.Frames.Count <= 1) {
				return false;
			}
			if (loop == DmdLoopMode.Loop || loop == DmdLoopMode.PingPong) {
				return true;
			}
			var duration = SpriteFrameClock.TotalDuration(sprite);
			return loop == DmdLoopMode.Once ? localFrame <= duration : localFrame < duration;
		}

		private static bool LayerChangesOverTime(DmdLayer layer)
		{
			if (layer.StartFrame > 0 || layer.EndFrame > 0 || layer.Tracks != null && layer.Tracks.Count > 0) {
				return true;
			}
			if (layer is NumberLayer number && number.CountUpSeconds > 0f ||
			    layer is TextLayer text && text.Overflow == DmdOverflow.Marquee && text.MarqueeSpeed > 0) {
				return true;
			}
			return layer is BitmapLayer bitmap && bitmap.Sprite?.Frames != null && bitmap.Sprite.Frames.Count > 1 ||
			       layer is MaskLayer mask && mask.Mask?.Frames != null && mask.Mask.Frames.Count > 1;
		}

		private static bool IsReadable(DmdBitmapData bitmap)
		{
			if (bitmap == null || bitmap.Width < 1 || bitmap.Height < 1) {
				return false;
			}
			var channels = bitmap.Format == DmdPixelFormat.I8 ? 1 : bitmap.Format == DmdPixelFormat.Rgb24 ? 3 : 0;
			var pixels = (long)bitmap.Width * bitmap.Height;
			return channels != 0 && bitmap.Pixels != null && bitmap.Pixels.LongLength == pixels * channels &&
			       (bitmap.Alpha == null || bitmap.Alpha.Length == 0 || bitmap.Alpha.LongLength == pixels);
		}

		private static int EvaluateInt(DmdLayer layer, DmdAnimatableProperty property, int fallback, int frame,
			CueDiagnostics diagnostics)
		{
			var value = Evaluate(layer, property, fallback, frame, diagnostics);
			return float.IsNaN(value) || float.IsInfinity(value) ? fallback : (int)math.round(value);
		}

		private static float Evaluate(DmdLayer layer, DmdAnimatableProperty property, float fallback, int frame,
			CueDiagnostics diagnostics)
		{
			return TryEvaluate(layer, property, frame, diagnostics, out var value) ? value : fallback;
		}

		private static bool TryEvaluate(DmdLayer layer, DmdAnimatableProperty property, int frame,
			CueDiagnostics diagnostics, out float value)
		{
			value = 0f;
			DmdPropertyTrack selected = null;
			var count = 0;
			if (layer.Tracks != null) {
				for (var index = 0; index < layer.Tracks.Count; index++) {
					var candidate = layer.Tracks[index];
					if (candidate != null && candidate.Property == property) {
						selected = candidate;
						count++;
					}
				}
			}
			if (count > 1) {
				diagnostics?.AuthoredContent("layer.track.duplicate", $"Duplicate {property} tracks found; the last wins.");
			}
			if (selected?.Keys == null || selected.Keys.Count == 0) {
				return false;
			}
			var keys = selected.Keys;
			if (frame <= keys[0].Frame) {
				value = keys[0].Value;
				return true;
			}
			for (var index = 1; index < keys.Count; index++) {
				var next = keys[index];
				if (frame > next.Frame) {
					continue;
				}
				var previous = keys[index - 1];
				if (previous.Interp == DmdInterpolation.Step || next.Frame <= previous.Frame) {
					value = previous.Value;
				} else {
					var progress = (float)(frame - previous.Frame) / (next.Frame - previous.Frame);
					value = math.lerp(previous.Value, next.Value, progress);
				}
				return true;
			}
			value = keys[keys.Count - 1].Value;
			return true;
		}

		private static byte ToOpacity(float opacity)
		{
			return float.IsNaN(opacity) || float.IsInfinity(opacity)
				? (byte)0
				: (byte)math.round(math.saturate(opacity) * byte.MaxValue);
		}
	}
}
