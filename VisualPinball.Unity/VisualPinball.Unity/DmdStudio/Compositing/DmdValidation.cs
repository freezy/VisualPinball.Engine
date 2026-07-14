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
using Unity.Mathematics;

namespace VisualPinball.Unity
{
	public enum DmdValidationSeverity
	{
		Warning,
		Error,
	}

	public readonly struct DmdValidationDiagnostic
	{
		public DmdValidationSeverity Severity { get; }
		public string Code { get; }
		public string Message { get; }

		public DmdValidationDiagnostic(DmdValidationSeverity severity, string code, string message)
		{
			Severity = severity;
			Code = code;
			Message = message;
		}

		public override string ToString() => $"{Severity} {Code}: {Message}";
	}

	public sealed class DmdValidationResult
	{
		private readonly List<DmdValidationDiagnostic> _diagnostics = new List<DmdValidationDiagnostic>();

		public IReadOnlyList<DmdValidationDiagnostic> Diagnostics => _diagnostics;
		public bool IsValid => _diagnostics.All(diagnostic => diagnostic.Severity != DmdValidationSeverity.Error);

		internal void Error(string code, string message)
		{
			Add(DmdValidationSeverity.Error, code, message);
		}

		internal void Warning(string code, string message)
		{
			Add(DmdValidationSeverity.Warning, code, message);
		}

		internal void Include(DmdValidationResult other, string path)
		{
			foreach (var diagnostic in other.Diagnostics) {
				Add(diagnostic.Severity, diagnostic.Code,
					string.IsNullOrEmpty(path) ? diagnostic.Message : $"{path}: {diagnostic.Message}");
			}
		}

		private void Add(DmdValidationSeverity severity, string code, string message)
		{
			_diagnostics.Add(new DmdValidationDiagnostic(severity, code, message));
		}
	}

	public sealed class DmdValidationException : Exception
	{
		public IReadOnlyList<DmdValidationDiagnostic> Diagnostics { get; }

		public DmdValidationException(IReadOnlyList<DmdValidationDiagnostic> diagnostics)
			: base(BuildMessage(diagnostics))
		{
			Diagnostics = diagnostics ?? Array.Empty<DmdValidationDiagnostic>();
		}

		private static string BuildMessage(IReadOnlyList<DmdValidationDiagnostic> diagnostics)
		{
			if (diagnostics == null || diagnostics.Count == 0) {
				return "Invalid DMD data.";
			}
			return string.Join(Environment.NewLine, diagnostics.Select(diagnostic => diagnostic.ToString()));
		}
	}

	/// <summary>
	/// Shared validation and normalization rules for DMD assets.
	/// </summary>
	public static class DmdValidation
	{
		public const int MaxWidth = 1024;
		public const int MaxHeight = 512;
		public const int MaxSpriteFrames = 4096;
		public const int MaxResolvedTextLength = 1024;
		public const int MaxBoundParams = 256;
		public const int MinFrameRate = 1;
		public const int MaxFrameRate = 120;
		public const int MaxTransitionFrames = 600;
		public const int MaxParameterNameLength = 128;

		public static DmdValidationResult Validate(DmdBitmapData bitmap)
		{
			if (bitmap == null) {
				throw new ArgumentNullException(nameof(bitmap));
			}

			var result = new DmdValidationResult();
			if (!ValidateDimensions(bitmap.Width, bitmap.Height, result, "bitmap")) {
				return result;
			}

			long channelCount;
			switch (bitmap.Format) {
				case DmdPixelFormat.I8:
					channelCount = 1;
					break;
				case DmdPixelFormat.Rgb24:
					channelCount = 3;
					break;
				default:
					result.Error("bitmap.format", $"Unsupported pixel format value {(int)bitmap.Format}.");
					return result;
			}

			var pixelCount = (long)bitmap.Width * bitmap.Height;
			var expectedPixels = pixelCount * channelCount;
			if (bitmap.Pixels == null || bitmap.Pixels.LongLength != expectedPixels) {
				result.Error("bitmap.pixels",
					$"Pixel buffer has {bitmap.Pixels?.LongLength ?? 0} bytes; expected {expectedPixels}.");
			}
			if (bitmap.Alpha != null && bitmap.Alpha.Length != 0 && bitmap.Alpha.LongLength != pixelCount) {
				result.Error("bitmap.alpha",
					$"Alpha buffer has {bitmap.Alpha.LongLength} bytes; expected 0 or {pixelCount}.");
			}
			return result;
		}

		public static DmdValidationResult Validate(DmdSpriteAsset sprite)
		{
			if (sprite == null) {
				throw new ArgumentNullException(nameof(sprite));
			}

			var result = new DmdValidationResult();
			if (sprite.Frames == null || sprite.Frames.Count == 0) {
				result.Error("sprite.frames.empty", "A sprite must contain at least one frame.");
				return result;
			}
			if (sprite.Frames.Count > MaxSpriteFrames) {
				result.Error("sprite.frames.cap", $"A sprite cannot exceed {MaxSpriteFrames} frames.");
			}

			DmdBitmapData first = null;
			for (var index = 0; index < sprite.Frames.Count; index++) {
				var frame = sprite.Frames[index];
				if (frame == null) {
					result.Error("sprite.frame.null", $"Frame {index} is null.");
					continue;
				}
				result.Include(Validate(frame), $"Frame {index}");
				if (first == null) {
					first = frame;
				} else if (frame.Width != first.Width || frame.Height != first.Height || frame.Format != first.Format) {
					result.Error("sprite.frame.shape", $"Frame {index} does not match the first frame's size and format.");
				}
			}

			ValidateFrameDurations(sprite, result);
			return result;
		}

		public static DmdValidationResult Validate(DmdFontAsset font)
		{
			if (font == null) {
				throw new ArgumentNullException(nameof(font));
			}

			var result = new DmdValidationResult();
			if (font.Atlas == null) {
				result.Error("font.atlas.null", "Font atlas is missing.");
				return result;
			}
			result.Include(Validate(font.Atlas), "Atlas");
			if (font.Atlas.Format != DmdPixelFormat.I8) {
				result.Error("font.atlas.format", "A font atlas must use the I8 format.");
			}
			if (font.LineHeight <= 0) {
				result.Error("font.lineHeight", "LineHeight must be greater than zero.");
			}
			if (font.Baseline < 0 || font.Baseline > font.LineHeight) {
				result.Error("font.baseline", "Baseline must be between zero and LineHeight.");
			}
			if (font.DigitWidth < 0) {
				result.Error("font.digitWidth", "DigitWidth cannot be negative.");
			}

			if (font.Glyphs == null) {
				result.Error("font.glyphs.null", "Glyph collection is null.");
				return result;
			}
			var codepoints = new HashSet<int>();
			for (var index = 0; index < font.Glyphs.Count; index++) {
				var glyph = font.Glyphs[index];
				if (glyph.Codepoint < 0 || !codepoints.Add(glyph.Codepoint)) {
					result.Error("font.glyph.codepoint", $"Glyph {index} has an invalid or duplicate codepoint.");
				}
				if (glyph.X < 0 || glyph.Y < 0 || glyph.W < 0 || glyph.H < 0 ||
				    (long)glyph.X + glyph.W > font.Atlas.Width || (long)glyph.Y + glyph.H > font.Atlas.Height) {
					result.Error("font.glyph.rect", $"Glyph {index} lies outside the atlas.");
				}
				if (glyph.Advance < 0) {
					result.Error("font.glyph.advance", $"Glyph {index} has a negative advance.");
				}
			}
			if (font.Kerning == null) {
				result.Error("font.kerning.null", "Kerning collection is null.");
			} else {
				for (var index = 0; index < font.Kerning.Count; index++) {
					var pair = font.Kerning[index];
					if (pair.LeftCodepoint < 0 || pair.RightCodepoint < 0) {
						result.Error("font.kerning.codepoint", $"Kerning pair {index} has a negative codepoint.");
					}
				}
			}
			return result;
		}

		public static DmdValidationResult Validate(DmdPaletteAsset palette)
		{
			if (palette == null) {
				throw new ArgumentNullException(nameof(palette));
			}
			var result = new DmdValidationResult();
			if (palette.Colors == null || palette.Colors.Length != 16) {
				result.Error("palette.colors", "A v1 DMD palette must contain exactly 16 colors.");
			}
			return result;
		}

		public static DmdValidationResult Validate(DmdCueAsset cue)
		{
			if (cue == null) {
				throw new ArgumentNullException(nameof(cue));
			}

			var result = new DmdValidationResult();
			ValidateCue(cue, result, new HashSet<DmdSpriteAsset>(), new HashSet<DmdFontAsset>());
			return result;
		}

		public static DmdValidationResult Validate(DmdProjectAsset project)
		{
			if (project == null) {
				throw new ArgumentNullException(nameof(project));
			}

			var result = new DmdValidationResult();
			ValidateDimensions(project.Width, project.Height, result, "project");
			if (!Enum.IsDefined(typeof(DmdColorMode), project.ColorMode)) {
				result.Error("project.colorMode", "ColorMode is invalid.");
			}
			if (string.IsNullOrWhiteSpace(project.DisplayId)) {
				result.Error("project.displayId", "DisplayId cannot be empty.");
			}
			var clampedFrameRate = math.clamp(project.FrameRate, MinFrameRate, MaxFrameRate);
			if (clampedFrameRate != project.FrameRate) {
				result.Warning("project.frameRate",
					$"FrameRate {project.FrameRate} will normalize to {clampedFrameRate}.");
			}

			if (project.Cues == null || project.Sprites == null || project.Fonts == null || project.Palettes == null) {
				result.Error("project.inventory.null", "Project inventory collections cannot be null.");
				return result;
			}

			var cueIds = new HashSet<string>(StringComparer.Ordinal);
			var validatedCues = new HashSet<DmdCueAsset>();
			var validatedSprites = new HashSet<DmdSpriteAsset>();
			var validatedFonts = new HashSet<DmdFontAsset>();
			var validatedPalettes = new HashSet<DmdPaletteAsset>();
			for (var index = 0; index < project.Cues.Count; index++) {
				var cue = project.Cues[index];
				if (cue == null) {
					result.Error("project.cue.null", $"Cue inventory entry {index} is null.");
					continue;
				}
				if (validatedCues.Add(cue)) {
					var cueResult = new DmdValidationResult();
					ValidateCue(cue, cueResult, validatedSprites, validatedFonts);
					result.Include(cueResult, $"Cue {cue.name}");
				}
				if (!string.IsNullOrWhiteSpace(cue.EffectiveId) && !cueIds.Add(cue.EffectiveId)) {
					result.Error("project.cue.duplicate", $"Cue id '{cue.EffectiveId}' is not unique.");
				}
				ValidateLayerInventoryReferences(project, cue, result);
			}

			for (var index = 0; index < project.Sprites.Count; index++) {
				var sprite = project.Sprites[index];
				if (sprite == null) {
					result.Error("project.sprite.null", $"Sprite inventory entry {index} is null.");
				} else if (validatedSprites.Add(sprite)) {
					result.Include(Validate(sprite), $"Sprite {sprite.name}");
				}
			}
			for (var index = 0; index < project.Fonts.Count; index++) {
				var font = project.Fonts[index];
				if (font == null) {
					result.Error("project.font.null", $"Font inventory entry {index} is null.");
				} else if (validatedFonts.Add(font)) {
					result.Include(Validate(font), $"Font {font.name}");
				}
			}
			for (var index = 0; index < project.Palettes.Count; index++) {
				var palette = project.Palettes[index];
				if (palette == null) {
					result.Error("project.palette.null", $"Palette inventory entry {index} is null.");
				} else if (validatedPalettes.Add(palette)) {
					result.Include(Validate(palette), $"Palette {palette.name}");
				}
			}

			ValidateSampleStates(project, result);
			return result;
		}

		/// <summary>
		/// Returns the pre-normalization diagnostics, then applies the load-time project defaults explicitly.
		/// </summary>
		public static DmdValidationResult Normalize(DmdProjectAsset project)
		{
			if (project == null) {
				throw new ArgumentNullException(nameof(project));
			}

			var result = Validate(project);
			project.FrameRate = math.clamp(project.FrameRate, MinFrameRate, MaxFrameRate);
			var normalizedSprites = new HashSet<DmdSpriteAsset>();
			if (project.Sprites != null) {
				foreach (var sprite in project.Sprites) {
					NormalizeSprite(sprite, normalizedSprites);
				}
			}
			if (project.Cues != null) {
				foreach (var cue in project.Cues) {
					if (cue?.Layers == null) {
						continue;
					}
					foreach (var layer in cue.Layers) {
						switch (layer) {
							case BitmapLayer bitmap:
								NormalizeSprite(bitmap.Sprite, normalizedSprites);
								break;
							case MaskLayer mask:
								NormalizeSprite(mask.Mask, normalizedSprites);
								break;
						}
					}
				}
			}
			return result;
		}

		/// <summary>
		/// Returns the pre-normalization diagnostics, then applies load-time frame-duration defaults explicitly.
		/// </summary>
		public static DmdValidationResult Normalize(DmdSpriteAsset sprite)
		{
			if (sprite == null) {
				throw new ArgumentNullException(nameof(sprite));
			}
			var result = Validate(sprite);
			NormalizeFrameDurations(sprite);
			return result;
		}

		internal static void ValidateParameterName(string name, string argumentName)
		{
			if (name == null) {
				throw new ArgumentNullException(argumentName);
			}
			if (!IsValidParameterName(name)) {
				throw new ArgumentException(
					$"Parameter names must be 1-{MaxParameterNameLength} characters, use identifier segments separated by dots, and contain only letters, digits, or underscores.",
					argumentName);
			}
		}

		public static bool IsValidParameterName(string name)
		{
			if (string.IsNullOrWhiteSpace(name) || name.Length > MaxParameterNameLength) {
				return false;
			}
			var segmentStart = true;
			foreach (var character in name) {
				if (character == '.') {
					if (segmentStart) {
						return false;
					}
					segmentStart = true;
					continue;
				}
				if (segmentStart) {
					if (character != '_' && !char.IsLetter(character)) {
						return false;
					}
					segmentStart = false;
				} else if (character != '_' && !char.IsLetterOrDigit(character)) {
					return false;
				}
			}
			return !segmentStart;
		}

		private static bool ValidateDimensions(int width, int height, DmdValidationResult result, string kind)
		{
			var valid = true;
			if (width < 1 || width > MaxWidth) {
				result.Error($"{kind}.width", $"Width must be between 1 and {MaxWidth}; got {width}.");
				valid = false;
			}
			if (height < 1 || height > MaxHeight) {
				result.Error($"{kind}.height", $"Height must be between 1 and {MaxHeight}; got {height}.");
				valid = false;
			}
			return valid;
		}

		private static void ValidateFrameDurations(DmdSpriteAsset sprite, DmdValidationResult result)
		{
			if (sprite.FrameDurations == null) {
				result.Warning("sprite.durations.null", "Null frame durations will normalize to the default duration.");
				return;
			}
			if (sprite.FrameDurations.Count == 0) {
				return;
			}
			if (sprite.FrameDurations.Count != sprite.Frames.Count) {
				result.Warning("sprite.durations.count", "Frame durations will normalize because their count does not match the frames.");
				return;
			}
			for (var index = 0; index < sprite.FrameDurations.Count; index++) {
				if (sprite.FrameDurations[index] < 1) {
					result.Warning("sprite.duration.value", $"Frame duration {index} will normalize to one tick.");
				}
			}
		}

		private static void NormalizeFrameDurations(DmdSpriteAsset sprite)
		{
			if (sprite.FrameDurations == null) {
				sprite.FrameDurations = new List<int>();
				return;
			}
			if (sprite.FrameDurations.Count == 0 || sprite.Frames == null) {
				return;
			}

			var normalized = new List<int>(sprite.Frames.Count);
			for (var index = 0; index < sprite.Frames.Count; index++) {
				normalized.Add(index < sprite.FrameDurations.Count ? math.max(1, sprite.FrameDurations[index]) : 1);
			}
			sprite.FrameDurations = normalized;
		}

		private static void NormalizeSprite(DmdSpriteAsset sprite, HashSet<DmdSpriteAsset> normalizedSprites)
		{
			if (sprite != null && normalizedSprites.Add(sprite)) {
				NormalizeFrameDurations(sprite);
			}
		}

		private static void ValidateCue(DmdCueAsset cue, DmdValidationResult result,
			HashSet<DmdSpriteAsset> validatedSprites, HashSet<DmdFontAsset> validatedFonts)
		{
			if (string.IsNullOrWhiteSpace(cue.EffectiveId)) {
				result.Error("cue.id", "CueId is empty and the asset has no name.");
			}
			if (!Enum.IsDefined(typeof(CuePriority), cue.Priority)) {
				result.Error("cue.priority", "Cue priority is invalid.");
			}
			if (!Enum.IsDefined(typeof(CueInterruptPolicy), cue.Interrupt)) {
				result.Error("cue.interrupt", "Cue interrupt policy is invalid.");
			}
			if (!Enum.IsDefined(typeof(CueReturnPolicy), cue.Return)) {
				result.Error("cue.return", "Cue return policy is invalid.");
			}
			if (cue.DurationFrames < 0) {
				result.Error("cue.duration", "DurationFrames cannot be negative.");
			}
			ValidateTransition(cue.EnterTransition, "enter", result);
			ValidateTransition(cue.ExitTransition, "exit", result);
			if (cue.DurationFrames > 0 && !cue.Loop &&
			    (long)cue.EnterTransition.DurationFrames + cue.ExitTransition.DurationFrames > cue.DurationFrames) {
				result.Error("cue.transition.total", "Enter and exit transitions exceed the finite cue duration.");
			}

			ValidateCueParameters(cue, result);
			if (cue.Layers == null) {
				result.Error("cue.layers.null", "Layer collection is null.");
				return;
			}
			for (var index = 0; index < cue.Layers.Count; index++) {
				var layer = cue.Layers[index];
				if (layer == null) {
					result.Error("cue.layer.null", $"Layer {index} is null.");
					continue;
				}
				ValidateLayer(layer, index, result, validatedSprites, validatedFonts);
			}
		}

		private static void ValidateTransition(DmdTransitionSpec transition, string label, DmdValidationResult result)
		{
			if (!Enum.IsDefined(typeof(DmdTransitionType), transition.Type)) {
				result.Error($"cue.transition.{label}.type", $"The {label} transition type is invalid.");
			}
			if (!Enum.IsDefined(typeof(DmdDirection), transition.Direction)) {
				result.Error($"cue.transition.{label}.direction", $"The {label} transition direction is invalid.");
			}
			if (transition.DurationFrames < 0 || transition.DurationFrames > MaxTransitionFrames) {
				result.Error($"cue.transition.{label}",
					$"The {label} transition duration must be between 0 and {MaxTransitionFrames}.");
			}
		}

		private static void ValidateCueParameters(DmdCueAsset cue, DmdValidationResult result)
		{
			if (cue.Parameters == null) {
				result.Error("cue.parameters.null", "Parameter declarations are null.");
				return;
			}
			if (cue.Parameters.Count > MaxBoundParams) {
				result.Error("cue.parameters.cap", $"A cue cannot declare more than {MaxBoundParams} parameters.");
			}
			var names = new HashSet<string>(StringComparer.Ordinal);
			for (var index = 0; index < cue.Parameters.Count; index++) {
				var parameter = cue.Parameters[index];
				if (!Enum.IsDefined(typeof(DmdParamType), parameter.Type)) {
					result.Error("cue.parameter.type", $"Parameter {index} has an invalid type.");
				}
				if (!IsValidParameterName(parameter.Name)) {
					result.Error("cue.parameter.name", $"Parameter {index} has an invalid name.");
				} else if (!names.Add(parameter.Name)) {
					result.Error("cue.parameter.duplicate", $"Parameter '{parameter.Name}' is declared more than once.");
				}
			}
		}

		private static void ValidateLayer(DmdLayer layer, int index, DmdValidationResult result,
			HashSet<DmdSpriteAsset> validatedSprites, HashSet<DmdFontAsset> validatedFonts)
		{
			var path = $"Layer {index}";
			if (float.IsNaN(layer.Opacity) || float.IsInfinity(layer.Opacity) ||
			    layer.Opacity < 0f || layer.Opacity > 1f) {
				result.Error("layer.opacity", $"{path} opacity must be between zero and one.");
			}
			if (!Enum.IsDefined(typeof(DmdBlendMode), layer.Blend)) {
				result.Error("layer.blend", $"{path} blend mode is invalid.");
			}
			if (layer.StartFrame < 0 || layer.EndFrame < 0 || layer.EndFrame != 0 && layer.EndFrame < layer.StartFrame) {
				result.Error("layer.span", $"{path} has an invalid frame span.");
			}
			ValidateTracks(layer, path, result);

			switch (layer) {
				case NumberLayer number:
					if (!IsValidParameterName(number.ParamName)) {
						result.Error("layer.number.parameter", $"{path} has an invalid parameter name.");
					}
					if (float.IsNaN(number.CountUpSeconds) || float.IsInfinity(number.CountUpSeconds) ||
					    number.CountUpSeconds < 0f) {
						result.Error("layer.number.countUp", $"{path} count-up time cannot be negative.");
					}
					ValidateTextLayer(number, path, result, validatedFonts);
					break;
				case TextLayer text:
					ValidateTextLayer(text, path, result, validatedFonts);
					break;
				case BitmapLayer bitmap:
					if (!Enum.IsDefined(typeof(DmdLoopMode), bitmap.Loop)) {
						result.Error("layer.bitmap.loop", $"{path} has an invalid loop mode.");
					}
					if (bitmap.Sprite == null) {
						result.Error("layer.bitmap.sprite", $"{path} has no sprite.");
					} else if (validatedSprites.Add(bitmap.Sprite)) {
						result.Include(Validate(bitmap.Sprite), $"{path} sprite");
					}
					if (bitmap.SpriteStartFrame < 0) {
						result.Error("layer.bitmap.start", $"{path} SpriteStartFrame cannot be negative.");
					}
					break;
				case MaskLayer mask:
					if (!Enum.IsDefined(typeof(DmdLoopMode), mask.Loop)) {
						result.Error("layer.mask.loop", $"{path} has an invalid loop mode.");
					}
					if (mask.Mask == null) {
						result.Error("layer.mask.sprite", $"{path} has no mask sprite.");
					} else if (validatedSprites.Add(mask.Mask)) {
						result.Include(Validate(mask.Mask), $"{path} mask");
					}
					if (mask.SpriteStartFrame < 0) {
						result.Error("layer.mask.start", $"{path} SpriteStartFrame cannot be negative.");
					}
					break;
				case ShapeLayer shape:
					if (!Enum.IsDefined(typeof(DmdShapeType), shape.Shape)) {
						result.Error("layer.shape.type", $"{path} has an invalid shape type.");
					}
					if (shape.Shape != DmdShapeType.Dot && (shape.Width < 1 || shape.Height < 1)) {
						result.Error("layer.shape.size", $"{path} shape dimensions must be positive.");
					}
					break;
				default:
					result.Error("layer.type", $"{path} has unsupported type {layer.GetType().FullName}.");
					break;
			}
		}

		private static void ValidateTextLayer(TextLayer text, string path, DmdValidationResult result,
			HashSet<DmdFontAsset> validatedFonts)
		{
			if (!Enum.IsDefined(typeof(DmdAnchor), text.Anchor)) {
				result.Error("layer.text.anchor", $"{path} has an invalid anchor.");
			}
			if (!Enum.IsDefined(typeof(DmdTextEffect), text.Effect)) {
				result.Error("layer.text.effect", $"{path} has an invalid text effect.");
			}
			if (!Enum.IsDefined(typeof(DmdOverflow), text.Overflow)) {
				result.Error("layer.text.overflow", $"{path} has an invalid overflow mode.");
			}
			if (text.Font == null) {
				result.Error("layer.text.font", $"{path} has no font.");
			} else if (validatedFonts.Add(text.Font)) {
				result.Include(Validate(text.Font), $"{path} font");
			}
			if (text.MarqueeSpeed < 0) {
				result.Error("layer.text.marquee", $"{path} marquee speed cannot be negative.");
			}
		}

		private static void ValidateTracks(DmdLayer layer, string path, DmdValidationResult result)
		{
			if (layer.Tracks == null) {
				result.Error("layer.tracks.null", $"{path} track collection is null.");
				return;
			}
			var properties = new HashSet<DmdAnimatableProperty>();
			for (var trackIndex = 0; trackIndex < layer.Tracks.Count; trackIndex++) {
				var track = layer.Tracks[trackIndex];
				if (track == null || track.Keys == null || track.Keys.Count == 0) {
					result.Error("layer.track.empty", $"{path} track {trackIndex} has no keys.");
					continue;
				}
				if (!Enum.IsDefined(typeof(DmdAnimatableProperty), track.Property)) {
					result.Error("layer.track.property", $"{path} track {trackIndex} has an invalid property.");
				}
				if (!properties.Add(track.Property)) {
					result.Warning("layer.track.duplicate", $"{path} contains duplicate {track.Property} tracks; the last wins.");
				}
				var previousFrame = -1;
				for (var keyIndex = 0; keyIndex < track.Keys.Count; keyIndex++) {
					var key = track.Keys[keyIndex];
					if (float.IsNaN(key.Value) || float.IsInfinity(key.Value)) {
						result.Error("layer.track.value", $"{path} track {trackIndex} has a non-finite value.");
					}
					if (!Enum.IsDefined(typeof(DmdInterpolation), key.Interp)) {
						result.Error("layer.track.interpolation",
							$"{path} track {trackIndex} key {keyIndex} has an invalid interpolation mode.");
					}
					if (key.Frame < 0 || key.Frame < previousFrame) {
						result.Error("layer.track.order", $"{path} track {trackIndex} keys must be ordered at non-negative frames.");
					}
					if (track.Property == DmdAnimatableProperty.Opacity && (key.Value < 0f || key.Value > 1f)) {
						result.Error("layer.track.opacity", $"{path} opacity track values must be between zero and one.");
					}
					previousFrame = key.Frame;
				}
			}
		}

		private static void ValidateLayerInventoryReferences(DmdProjectAsset project, DmdCueAsset cue,
			DmdValidationResult result)
		{
			if (cue.Layers == null) {
				return;
			}
			foreach (var layer in cue.Layers) {
				switch (layer) {
					case BitmapLayer bitmap when bitmap.Sprite != null && !project.Sprites.Contains(bitmap.Sprite):
						result.Warning("project.sprite.orphan", $"Cue '{cue.EffectiveId}' references sprite '{bitmap.Sprite.name}' outside the project inventory.");
						break;
					case MaskLayer mask when mask.Mask != null && !project.Sprites.Contains(mask.Mask):
						result.Warning("project.mask.orphan", $"Cue '{cue.EffectiveId}' references mask '{mask.Mask.name}' outside the project inventory.");
						break;
				}
				if (layer is TextLayer text && text.Font != null && !project.Fonts.Contains(text.Font)) {
					result.Warning("project.font.orphan", $"Cue '{cue.EffectiveId}' references font '{text.Font.name}' outside the project inventory.");
				}
			}
		}

		private static void ValidateSampleStates(DmdProjectAsset project, DmdValidationResult result)
		{
			if (project.SampleStates == null) {
				result.Error("project.samples.null", "Sample state collection cannot be null.");
				return;
			}
			for (var stateIndex = 0; stateIndex < project.SampleStates.Count; stateIndex++) {
				var state = project.SampleStates[stateIndex];
				if (state == null || state.Values == null) {
					result.Error("project.sample.null", $"Sample state {stateIndex} is null or has no values.");
					continue;
				}
				if (state.Values.Count > MaxBoundParams) {
					result.Error("project.sample.cap", $"Sample state {stateIndex} exceeds {MaxBoundParams} values.");
				}
				foreach (var value in state.Values) {
					if (!Enum.IsDefined(typeof(DmdParamType), value.Type)) {
						result.Error("project.sample.type", $"Sample state {stateIndex} contains an invalid parameter type.");
					}
					if (!IsValidParameterName(value.Name)) {
						result.Error("project.sample.name", $"Sample state {stateIndex} contains an invalid parameter name.");
					}
				}
			}
		}
	}
}
