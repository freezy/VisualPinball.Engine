// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace VisualPinball.Unity.Editor
{
	public readonly struct DmdStudioValidationDiagnostic
	{
		public DmdValidationSeverity Severity { get; }
		public string Code { get; }
		public string Message { get; }

		public DmdStudioValidationDiagnostic(DmdValidationSeverity severity, string code, string message)
		{
			Severity = severity;
			Code = code;
			Message = message;
		}
	}

	/// <summary>
	/// Authoring-only checks which need project inventories, samples, and rendered text.
	/// </summary>
	public static class DmdStudioValidation
	{
		public static IReadOnlyList<DmdStudioValidationDiagnostic> Validate(DmdProjectAsset project)
		{
			var result = new List<DmdStudioValidationDiagnostic>();
			if (project == null) {
				return result;
			}

			try {
				foreach (var diagnostic in project.Validate().Diagnostics) {
					result.Add(new DmdStudioValidationDiagnostic(diagnostic.Severity, diagnostic.Code,
						diagnostic.Message));
				}
			} catch (Exception exception) when (!(exception is OutOfMemoryException)) {
				Add(result, DmdValidationSeverity.Error, "project.validation.exception", exception.Message);
			}

			if (project.ColorMode == DmdColorMode.Rgb24) {
				Add(result, DmdValidationSeverity.Warning, "project.rgb.colorization",
					"RGB24 output bypasses external mono DMD colorization formats.");
			}
			ValidateSprites(project, result);
			if (project.Cues != null) {
				foreach (var cue in project.Cues.Where(cue => cue != null)) {
					ValidateCue(project, cue, result);
				}
			}
			return result;
		}

		private static void ValidateSprites(DmdProjectAsset project, List<DmdStudioValidationDiagnostic> result)
		{
			var sprites = new HashSet<DmdSpriteAsset>();
			if (project.Sprites != null) {
				foreach (var sprite in project.Sprites.Where(sprite => sprite != null)) sprites.Add(sprite);
			}
			if (project.Cues != null) {
				foreach (var layer in project.Cues.Where(cue => cue?.Layers != null).SelectMany(cue => cue.Layers)) {
					if (layer is BitmapLayer bitmap && bitmap.Sprite != null) sprites.Add(bitmap.Sprite);
					if (layer is MaskLayer mask && mask.Mask != null) sprites.Add(mask.Mask);
				}
			}
			var shadeLimit = project.ColorMode == DmdColorMode.Mono4 ? 4 : 16;
			foreach (var sprite in sprites) {
				if (sprite.Frames == null) {
					continue;
				}
				for (var frameIndex = 0; frameIndex < sprite.Frames.Count; frameIndex++) {
					var frame = sprite.Frames[frameIndex];
					if (frame == null) {
						continue;
					}
					var path = $"Sprite '{sprite.name}' frame {frameIndex}";
					if (frame.Width != project.Width || frame.Height != project.Height) {
						Add(result, DmdValidationSeverity.Warning, "sprite.canvas.size",
							$"{path} is {frame.Width}x{frame.Height}, not the {project.Width}x{project.Height} canvas.");
					}
					if (project.ColorMode != DmdColorMode.Rgb24 && frame.Format == DmdPixelFormat.Rgb24) {
						Add(result, DmdValidationSeverity.Warning, "sprite.rgb.mono",
							$"{path} is RGB and will be converted for a mono project.");
					}
					if (project.ColorMode != DmdColorMode.Rgb24 && frame.Format == DmdPixelFormat.I8 &&
					    CountShades(frame.Pixels, shadeLimit + 1) > shadeLimit) {
						Add(result, DmdValidationSeverity.Warning, "sprite.shades.overflow",
							$"{path} uses more than {shadeLimit} shades and will be quantized.");
					}
				}
			}
		}

		private static void ValidateCue(DmdProjectAsset project, DmdCueAsset cue,
			List<DmdStudioValidationDiagnostic> result)
		{
			var declarations = (cue.Parameters ?? new List<DmdCueParameter>())
				.Where(parameter => !string.IsNullOrWhiteSpace(parameter.Name))
				.GroupBy(parameter => parameter.Name, StringComparer.Ordinal)
				.ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);
			if (cue.Layers == null) {
				return;
			}
			for (var layerIndex = 0; layerIndex < cue.Layers.Count; layerIndex++) {
				if (!(cue.Layers[layerIndex] is TextLayer text)) {
					continue;
				}
				var bindings = text is NumberLayer number
					? new[] { new TextBinding(number.ParamName, number.Format) }
					: ParseBindings(text.Text);
				foreach (var binding in bindings) {
					if (string.IsNullOrWhiteSpace(binding.Name)) {
						continue;
					}
					if (!declarations.TryGetValue(binding.Name, out var declaration)) {
						Add(result, DmdValidationSeverity.Warning, "binding.undeclared",
							$"Cue '{cue.EffectiveId}' layer {layerIndex} uses undeclared parameter '{binding.Name}'.");
					} else if (!IsValidFormat(declaration.DefaultValue, binding.Format)) {
						Add(result, DmdValidationSeverity.Error, "binding.format",
							$"Cue '{cue.EffectiveId}' layer {layerIndex} has invalid format '{binding.Format}' for '{binding.Name}'.");
					}
					ValidateSampleBinding(project, cue, layerIndex, binding.Name, result);
				}
			}
			ValidateRenderedSamples(project, cue, declarations.Values, result);
		}

		private static void ValidateSampleBinding(DmdProjectAsset project, DmdCueAsset cue, int layerIndex,
			string name, List<DmdStudioValidationDiagnostic> result)
		{
			if (project.SampleStates == null || project.SampleStates.Count == 0) {
				Add(result, DmdValidationSeverity.Warning, "binding.sample.none",
					$"Cue '{cue.EffectiveId}' layer {layerIndex} has no sample state for '{name}'.");
				return;
			}
			foreach (var sample in project.SampleStates.Where(sample => sample != null)) {
				if (sample.Values == null || sample.Values.All(value => !string.Equals(value.Name, name,
					    StringComparison.Ordinal))) {
					Add(result, DmdValidationSeverity.Warning, "binding.sample.unbound",
						$"Sample '{sample.Name}' does not bind '{name}' used by cue '{cue.EffectiveId}'.");
				}
			}
		}

		private static void ValidateRenderedSamples(DmdProjectAsset project, DmdCueAsset cue,
			IEnumerable<DmdCueParameter> declarations, List<DmdStudioValidationDiagnostic> result)
		{
			if (project.Width < 1 || project.Height < 1 || project.Width > DmdValidation.MaxWidth ||
			    project.Height > DmdValidation.MaxHeight) {
				return;
			}
			var renderer = new CueRenderer(project);
			var format = project.ColorMode == DmdColorMode.Rgb24 ? DmdPixelFormat.Rgb24 : DmdPixelFormat.I8;
			var surface = new DmdSurface(project.Width, project.Height, format);
			var samples = project.SampleStates != null && project.SampleStates.Count > 0
				? project.SampleStates.Where(sample => sample != null).ToArray()
				: new[] { new DmdSampleState { Name = "Defaults" } };
			var frames = cue.Layers.Where(layer => layer is TextLayer)
				.Select(layer => System.Math.Max(0, layer.StartFrame)).Distinct().ToArray();
			if (frames.Length == 0) {
				return;
			}
			foreach (var sample in samples) {
				var parameters = new DmdParams();
				foreach (var declaration in declarations) {
					Set(parameters, declaration.DefaultValue);
				}
				if (sample.Values != null) {
					foreach (var value in sample.Values) {
						Set(parameters, value);
					}
				}
				var diagnostics = new CueDiagnostics();
				var state = new CueInstanceState();
				foreach (var frame in frames) {
					renderer.Render(surface, cue, frame, parameters, state, diagnostics);
				}
				foreach (var diagnostic in diagnostics.Diagnostics) {
					Add(result, DmdValidationSeverity.Warning, diagnostic.Code,
						$"Cue '{cue.EffectiveId}', sample '{sample.Name}': {diagnostic.Message}");
				}
			}
		}

		private static TextBinding[] ParseBindings(string template)
		{
			var bindings = new List<TextBinding>();
			template = template ?? string.Empty;
			for (var index = 0; index < template.Length; index++) {
				if (template[index] != '{') {
					continue;
				}
				if (index + 1 < template.Length && template[index + 1] == '{') {
					index++;
					continue;
				}
				var end = template.IndexOf('}', index + 1);
				if (end < 0) {
					break;
				}
				var token = template.Substring(index + 1, end - index - 1);
				var separator = token.IndexOf(':');
				var name = separator < 0 ? token : token.Substring(0, separator);
				if (DmdValidation.IsValidParameterName(name)) {
					bindings.Add(new TextBinding(name, separator < 0 ? null : token.Substring(separator + 1)));
				}
				index = end;
			}
			return bindings.ToArray();
		}

		private static bool IsValidFormat(DmdParamValue value, string format)
		{
			if (string.IsNullOrEmpty(format)) {
				return true;
			}
			try {
				object raw = value.Type switch {
					DmdParamType.Integer => value.IntValue,
					DmdParamType.Float => value.FloatValue,
					DmdParamType.String => value.StringValue ?? string.Empty,
					DmdParamType.Boolean => value.BoolValue,
					_ => value.ToInvariantString()
				};
				string.Format(CultureInfo.InvariantCulture, $"{{0:{format}}}", raw);
				return true;
			} catch (FormatException) {
				return false;
			}
		}

		private static void Set(DmdParams parameters, DmdParamValue value)
		{
			if (!DmdValidation.IsValidParameterName(value.Name)) {
				return;
			}
			switch (value.Type) {
				case DmdParamType.Integer: parameters.Set(value.Name, value.IntValue); break;
				case DmdParamType.Float: parameters.Set(value.Name, value.FloatValue); break;
				case DmdParamType.String: parameters.Set(value.Name, value.StringValue); break;
				case DmdParamType.Boolean: parameters.Set(value.Name, value.BoolValue); break;
			}
		}

		private static int CountShades(byte[] pixels, int stopAt)
		{
			if (pixels == null) {
				return 0;
			}
			var shades = new bool[256];
			var count = 0;
			foreach (var pixel in pixels) {
				if (!shades[pixel]) {
					shades[pixel] = true;
					if (++count >= stopAt) {
						break;
					}
				}
			}
			return count;
		}

		private static void Add(List<DmdStudioValidationDiagnostic> result, DmdValidationSeverity severity,
			string code, string message)
		{
			if (result.Any(existing => existing.Severity == severity && existing.Code == code &&
			    existing.Message == message)) {
				return;
			}
			result.Add(new DmdStudioValidationDiagnostic(severity, code, message));
		}

		private readonly struct TextBinding
		{
			public readonly string Name;
			public readonly string Format;

			public TextBinding(string name, string format)
			{
				Name = name;
				Format = format;
			}
		}
	}
}
