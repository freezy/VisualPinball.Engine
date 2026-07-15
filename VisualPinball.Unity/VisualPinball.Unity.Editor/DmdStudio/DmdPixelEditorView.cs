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
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	public enum DmdPixelTool
	{
		Pencil,
		Eraser,
		Fill,
		Rectangle,
	}

	/// <summary>
	/// Pixel-accurate editor shared by sprite frames and BMFont glyph regions.
	/// </summary>
	public sealed class DmdPixelEditorView : VisualElement
	{
		private readonly VisualElement _canvas;
		private readonly Label _targetLabel;
		private readonly SliderInt _shade;
		private readonly Toggle _transparent;
		private readonly DropdownField _palette;
		private readonly Dictionary<DmdPixelTool, ToolbarToggle> _toolButtons =
			new Dictionary<DmdPixelTool, ToolbarToggle>();

		private UnityEngine.Object _owner;
		private DmdBitmapData _bitmap;
		private DmdProjectAsset _project;
		private RectInt _region;
		private DmdPixelTool _tool;
		private bool _drawing;
		private Vector2Int _rectangleStart;
		private Vector2Int _rectangleEnd;
		private bool _recordedUndo;

		public event Action Changed;
		public bool HasTarget => _owner != null && _bitmap != null && _region.width > 0 && _region.height > 0;

		public DmdPixelEditorView()
		{
			name = "dmd-pixel-editor";
			style.minHeight = 180;
			style.flexGrow = 1;

			var tools = new Toolbar();
			foreach (DmdPixelTool tool in Enum.GetValues(typeof(DmdPixelTool))) {
				var button = new ToolbarToggle { text = tool.ToString() };
				button.RegisterValueChangedCallback(evt => {
					if (evt.newValue) {
						SelectTool(tool);
					} else if (_tool == tool) {
						button.SetValueWithoutNotify(true);
					}
				});
				_toolButtons.Add(tool, button);
				tools.Add(button);
			}
			_toolButtons[DmdPixelTool.Pencil].SetValueWithoutNotify(true);
			_shade = new SliderInt("Shade", 0, 15) { value = 15 };
			_shade.style.width = 150;
			tools.Add(_shade);
			_transparent = new Toggle("Transparent");
			tools.Add(_transparent);
			_palette = new DropdownField(new List<string> { "Project ramp" }, 0);
			_palette.style.width = 130;
			tools.Add(_palette);
			Add(tools);

			_targetLabel = new Label("Select a sprite frame or font glyph.");
			_targetLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
			_targetLabel.style.height = 20;
			Add(_targetLabel);

			_canvas = new VisualElement {
				name = "dmd-pixel-canvas",
				focusable = true
			};
			_canvas.style.flexGrow = 1;
			_canvas.style.minHeight = 140;
			_canvas.style.backgroundColor = new Color(0.045f, 0.045f, 0.045f);
			_canvas.generateVisualContent += Draw;
			_canvas.RegisterCallback<PointerDownEvent>(OnPointerDown);
			_canvas.RegisterCallback<PointerMoveEvent>(OnPointerMove);
			_canvas.RegisterCallback<PointerUpEvent>(OnPointerUp);
			Add(_canvas);
		}

		public void SetTarget(UnityEngine.Object owner, DmdBitmapData bitmap, RectInt region,
			DmdProjectAsset project, string label)
		{
			_owner = owner;
			_bitmap = bitmap;
			_project = project;
			_region = ClampRegion(bitmap, region);
			_targetLabel.text = HasTarget ? label : "Select a sprite frame or font glyph.";
			var choices = new List<string> { "Project ramp" };
			if (project?.Palettes != null) {
				choices.AddRange(project.Palettes.Where(candidate => candidate != null)
					.Select(candidate => candidate.name));
			}
			_palette.choices = choices;
			_palette.index = Mathf.Clamp(_palette.index, 0, choices.Count - 1);
			var shadeCount = project?.ColorMode == DmdColorMode.Mono4 ? 4 : 16;
			_shade.highValue = shadeCount - 1;
			_shade.SetValueWithoutNotify(Mathf.Clamp(_shade.value, 0, shadeCount - 1));
			_canvas.MarkDirtyRepaint();
		}

		public void ClearTarget()
		{
			SetTarget(null, null, default, null, null);
		}

		public void SetBrush(int shade, bool transparent)
		{
			_shade.SetValueWithoutNotify(Mathf.Clamp(shade, 0, _shade.highValue));
			_transparent.SetValueWithoutNotify(transparent);
		}

		/// <summary>
		/// Applies a tool in region-local pixel coordinates. Used by keyboard actions and editor tests.
		/// </summary>
		public bool ApplyTool(DmdPixelTool tool, Vector2Int start, Vector2Int end)
		{
			if (!HasTarget || start.x < 0 || start.y < 0 || start.x >= _region.width ||
			    start.y >= _region.height) {
				return false;
			}
			end.x = Mathf.Clamp(end.x, 0, _region.width - 1);
			end.y = Mathf.Clamp(end.y, 0, _region.height - 1);
			_tool = tool;
			_recordedUndo = false;
			_rectangleStart = start;
			_rectangleEnd = end;
			BeginMutation(tool switch {
				DmdPixelTool.Eraser => "Erase DMD pixels",
				DmdPixelTool.Fill => "Fill DMD pixels",
				DmdPixelTool.Rectangle => "Draw DMD rectangle",
				_ => "Draw DMD pixels"
			});
			if (tool == DmdPixelTool.Fill) {
				FloodFill(start);
			} else if (tool == DmdPixelTool.Rectangle) {
				DrawRectangle();
			} else {
				Paint(start);
			}
			EditorUtility.SetDirty(_owner);
			Changed?.Invoke();
			_canvas.MarkDirtyRepaint();
			return true;
		}

		private void SelectTool(DmdPixelTool tool)
		{
			_tool = tool;
			foreach (var pair in _toolButtons) {
				pair.Value.SetValueWithoutNotify(pair.Key == tool);
			}
		}

		private void Draw(MeshGenerationContext context)
		{
			if (!HasTarget) {
				return;
			}
			var layout = PixelLayout();
			var painter = context.painter2D;
			for (var y = 0; y < _region.height; y++) {
				for (var x = 0; x < _region.width; x++) {
					var rect = new Rect(layout.x + x * layout.width, layout.y + y * layout.height,
						layout.width, layout.height);
					var globalX = _region.x + x;
					var globalY = _region.y + y;
					var alpha = ReadAlpha(globalX, globalY);
					if (alpha < 255) {
						FillRect(painter, rect, (x + y & 1) == 0
							? new Color(0.22f, 0.22f, 0.22f)
							: new Color(0.13f, 0.13f, 0.13f));
					}
					var color = ReadColor(globalX, globalY);
					color.a = alpha / 255f;
					FillRect(painter, rect, color);
				}
			}
			if (layout.width >= 5f && layout.height >= 5f) {
				painter.strokeColor = new Color(1f, 1f, 1f, 0.13f);
				painter.lineWidth = 1f;
				for (var x = 0; x <= _region.width; x++) {
					StrokeLine(painter, new Vector2(layout.x + x * layout.width, layout.y),
						new Vector2(layout.x + x * layout.width, layout.y + _region.height * layout.height));
				}
				for (var y = 0; y <= _region.height; y++) {
					StrokeLine(painter, new Vector2(layout.x, layout.y + y * layout.height),
						new Vector2(layout.x + _region.width * layout.width, layout.y + y * layout.height));
				}
			}
			if (_drawing && _tool == DmdPixelTool.Rectangle) {
				var min = Vector2Int.Min(_rectangleStart, _rectangleEnd);
				var max = Vector2Int.Max(_rectangleStart, _rectangleEnd);
				painter.strokeColor = Color.white;
				painter.lineWidth = 1.5f;
				var rect = new Rect(layout.x + min.x * layout.width, layout.y + min.y * layout.height,
					(max.x - min.x + 1) * layout.width, (max.y - min.y + 1) * layout.height);
				StrokeRect(painter, rect);
			}
		}

		private void OnPointerDown(PointerDownEvent evt)
		{
			if (!HasTarget || evt.button != 0 || !TryPixel(evt.localPosition, out var pixel)) {
				return;
			}
			_drawing = true;
			_recordedUndo = false;
			_rectangleStart = _rectangleEnd = pixel;
			_canvas.CapturePointer(evt.pointerId);
			if (_tool == DmdPixelTool.Fill) {
				BeginMutation("Fill DMD pixels");
				FloodFill(pixel);
				EndGesture(evt.pointerId);
			} else if (_tool != DmdPixelTool.Rectangle) {
				BeginMutation(_tool == DmdPixelTool.Eraser ? "Erase DMD pixels" : "Draw DMD pixels");
				Paint(pixel);
			}
			evt.StopPropagation();
		}

		private void OnPointerMove(PointerMoveEvent evt)
		{
			if (!_drawing || !_canvas.HasPointerCapture(evt.pointerId) ||
			    !TryPixel(evt.localPosition, out var pixel)) {
				return;
			}
			if (_tool == DmdPixelTool.Rectangle) {
				_rectangleEnd = pixel;
				_canvas.MarkDirtyRepaint();
			} else if (_tool != DmdPixelTool.Fill) {
				Paint(pixel);
			}
		}

		private void OnPointerUp(PointerUpEvent evt)
		{
			if (!_drawing || evt.button != 0) {
				return;
			}
			if (_tool == DmdPixelTool.Rectangle) {
				if (TryPixel(evt.localPosition, out var pixel)) {
					_rectangleEnd = pixel;
				}
				BeginMutation("Draw DMD rectangle");
				DrawRectangle();
			}
			EndGesture(evt.pointerId);
		}

		private void EndGesture(int pointerId)
		{
			_drawing = false;
			if (_canvas.HasPointerCapture(pointerId)) {
				_canvas.ReleasePointer(pointerId);
			}
			if (_recordedUndo) {
				EditorUtility.SetDirty(_owner);
				Changed?.Invoke();
			}
			_canvas.MarkDirtyRepaint();
		}

		private void BeginMutation(string undoName)
		{
			if (_recordedUndo) {
				return;
			}
			Undo.RecordObject(_owner, undoName);
			_bitmap.Pixels = _bitmap.Pixels == null ? Array.Empty<byte>() : (byte[])_bitmap.Pixels.Clone();
			_bitmap.Alpha = _bitmap.Alpha == null ? Array.Empty<byte>() : (byte[])_bitmap.Alpha.Clone();
			EnsureBuffers();
			_recordedUndo = true;
		}

		private void Paint(Vector2Int pixel)
		{
			var global = new Vector2Int(_region.x + pixel.x, _region.y + pixel.y);
			Write(global.x, global.y, _tool == DmdPixelTool.Eraser || _transparent.value);
			_canvas.MarkDirtyRepaint();
		}

		private void DrawRectangle()
		{
			var min = Vector2Int.Min(_rectangleStart, _rectangleEnd);
			var max = Vector2Int.Max(_rectangleStart, _rectangleEnd);
			for (var x = min.x; x <= max.x; x++) {
				Write(_region.x + x, _region.y + min.y, _transparent.value);
				Write(_region.x + x, _region.y + max.y, _transparent.value);
			}
			for (var y = min.y + 1; y < max.y; y++) {
				Write(_region.x + min.x, _region.y + y, _transparent.value);
				Write(_region.x + max.x, _region.y + y, _transparent.value);
			}
		}

		private void FloodFill(Vector2Int start)
		{
			var startX = _region.x + start.x;
			var startY = _region.y + start.y;
			var old = PixelKey(startX, startY);
			var transparent = _transparent.value;
			var replacement = SelectedPixelKey(transparent);
			if (old == replacement) {
				return;
			}
			var pending = new Stack<Vector2Int>();
			pending.Push(start);
			while (pending.Count > 0) {
				var pixel = pending.Pop();
				var x = _region.x + pixel.x;
				var y = _region.y + pixel.y;
				if (PixelKey(x, y) != old) {
					continue;
				}
				Write(x, y, transparent);
				if (pixel.x > 0) pending.Push(new Vector2Int(pixel.x - 1, pixel.y));
				if (pixel.x + 1 < _region.width) pending.Push(new Vector2Int(pixel.x + 1, pixel.y));
				if (pixel.y > 0) pending.Push(new Vector2Int(pixel.x, pixel.y - 1));
				if (pixel.y + 1 < _region.height) pending.Push(new Vector2Int(pixel.x, pixel.y + 1));
			}
		}

		private void Write(int x, int y, bool transparent)
		{
			var index = y * _bitmap.Width + x;
			if (_bitmap.Alpha.Length > 0) {
				_bitmap.Alpha[index] = transparent ? (byte)0 : (byte)255;
			}
			if (transparent) {
				return;
			}
			var color = SelectedColor();
			if (_bitmap.Format == DmdPixelFormat.Rgb24) {
				var offset = index * 3;
				_bitmap.Pixels[offset] = color.r;
				_bitmap.Pixels[offset + 1] = color.g;
				_bitmap.Pixels[offset + 2] = color.b;
			} else {
				_bitmap.Pixels[index] = (byte)((color.r * 77 + color.g * 150 + color.b * 29 + 128) >> 8);
			}
		}

		private Color32 SelectedColor()
		{
			var paletteIndex = _palette.index - 1;
			var palette = _project?.Palettes?.Where(candidate => candidate != null).ElementAtOrDefault(paletteIndex);
			if (palette?.Colors != null && _shade.value < palette.Colors.Length) {
				return palette.Colors[_shade.value];
			}
			var shadeCount = System.Math.Max(2, _shade.highValue + 1);
			var value = (byte)Mathf.RoundToInt(_shade.value * 255f / (shadeCount - 1));
			return new Color32(value, value, value, 255);
		}

		private ulong SelectedPixelKey(bool transparent)
		{
			if (transparent) {
				return 0;
			}
			var color = SelectedColor();
			if (_bitmap.Format == DmdPixelFormat.I8) {
				var value = (byte)((color.r * 77 + color.g * 150 + color.b * 29 + 128) >> 8);
				return 0xff000000UL | (ulong)value << 16 | (ulong)value << 8 | value;
			}
			return 0xff000000UL | (ulong)color.r << 16 | (ulong)color.g << 8 | color.b;
		}

		private ulong PixelKey(int x, int y)
		{
			var alpha = ReadAlpha(x, y);
			if (alpha == 0) {
				return 0;
			}
			var color = (Color32)ReadColor(x, y);
			return (ulong)alpha << 24 | (ulong)color.r << 16 | (ulong)color.g << 8 | color.b;
		}

		private Color ReadColor(int x, int y)
		{
			if (_bitmap?.Pixels == null) {
				return Color.clear;
			}
			var index = y * _bitmap.Width + x;
			if (_bitmap.Format == DmdPixelFormat.Rgb24) {
				var offset = index * 3;
				if (offset + 2 >= _bitmap.Pixels.Length) return Color.clear;
				return new Color32(_bitmap.Pixels[offset], _bitmap.Pixels[offset + 1],
					_bitmap.Pixels[offset + 2], 255);
			}
			if (index >= _bitmap.Pixels.Length) return Color.clear;
			var value = _bitmap.Pixels[index];
			return new Color32(value, value, value, 255);
		}

		private byte ReadAlpha(int x, int y)
		{
			var index = y * _bitmap.Width + x;
			return _bitmap.Alpha != null && _bitmap.Alpha.Length == _bitmap.Width * _bitmap.Height
				? _bitmap.Alpha[index]
				: (byte)255;
		}

		private void EnsureBuffers()
		{
			var pixels = checked(_bitmap.Width * _bitmap.Height);
			var expected = _bitmap.Format == DmdPixelFormat.Rgb24 ? checked(pixels * 3) : pixels;
			if (_bitmap.Pixels.Length != expected) {
				Array.Resize(ref _bitmap.Pixels, expected);
			}
			if (_bitmap.Alpha.Length != pixels) {
				var hadAlpha = _bitmap.Alpha.Length > 0;
				Array.Resize(ref _bitmap.Alpha, pixels);
				if (!hadAlpha) {
					for (var index = 0; index < pixels; index++) _bitmap.Alpha[index] = 255;
				}
			}
		}

		private bool TryPixel(Vector2 position, out Vector2Int pixel)
		{
			pixel = default;
			if (!HasTarget) return false;
			var layout = PixelLayout();
			var x = Mathf.FloorToInt((position.x - layout.x) / layout.width);
			var y = Mathf.FloorToInt((position.y - layout.y) / layout.height);
			if (x < 0 || y < 0 || x >= _region.width || y >= _region.height) return false;
			pixel = new Vector2Int(x, y);
			return true;
		}

		private Rect PixelLayout()
		{
			var scale = Mathf.Max(1f, Mathf.Min(_canvas.contentRect.width / System.Math.Max(1, _region.width),
				_canvas.contentRect.height / System.Math.Max(1, _region.height)));
			var width = _region.width * scale;
			var height = _region.height * scale;
			return new Rect((_canvas.contentRect.width - width) * 0.5f,
				(_canvas.contentRect.height - height) * 0.5f, scale, scale);
		}

		private static RectInt ClampRegion(DmdBitmapData bitmap, RectInt region)
		{
			if (bitmap == null || bitmap.Width <= 0 || bitmap.Height <= 0) return default;
			if (region.width <= 0 || region.height <= 0) return new RectInt(0, 0, bitmap.Width, bitmap.Height);
			var x = Mathf.Clamp(region.x, 0, bitmap.Width);
			var y = Mathf.Clamp(region.y, 0, bitmap.Height);
			return new RectInt(x, y, Mathf.Clamp(region.width, 0, bitmap.Width - x),
				Mathf.Clamp(region.height, 0, bitmap.Height - y));
		}

		private static void FillRect(Painter2D painter, Rect rect, Color color)
		{
			painter.fillColor = color;
			painter.BeginPath();
			painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
			painter.LineTo(new Vector2(rect.xMax, rect.yMin));
			painter.LineTo(new Vector2(rect.xMax, rect.yMax));
			painter.LineTo(new Vector2(rect.xMin, rect.yMax));
			painter.ClosePath();
			painter.Fill();
		}

		private static void StrokeRect(Painter2D painter, Rect rect)
		{
			painter.BeginPath();
			painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
			painter.LineTo(new Vector2(rect.xMax, rect.yMin));
			painter.LineTo(new Vector2(rect.xMax, rect.yMax));
			painter.LineTo(new Vector2(rect.xMin, rect.yMax));
			painter.ClosePath();
			painter.Stroke();
		}

		private static void StrokeLine(Painter2D painter, Vector2 from, Vector2 to)
		{
			painter.BeginPath();
			painter.MoveTo(from);
			painter.LineTo(to);
			painter.Stroke();
		}
	}
}
