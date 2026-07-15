// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	public enum DmdCanvasMode
	{
		Raw,
		Dots,
	}

	public sealed class DmdCanvasView : VisualElement, IDisposable
	{
		private readonly Image _image;
		private Texture2D _texture;
		private DmdProjectAsset _project;
		private DmdCueAsset _selectedCue;
		private DmdLayer _selectedLayer;
		private DmdCanvasMode _mode;
		private int _sourceWidth;
		private int _sourceHeight;
		private Vector2 _dragStart;
		private int _dragX;
		private int _dragY;
		private bool _dragging;
		private float _zoom = 1f;
		private Vector2 _pan;
		private bool _panning;
		private Vector2 _panStart;
		private Vector2 _panOrigin;

		public Texture2D PreviewTexture => _texture;
		public event Action LayerPositionChanged;

		public DmdCanvasView()
		{
			name = "dmd-canvas";
			focusable = true;
			style.flexGrow = 1;
			style.minHeight = 120;
			style.backgroundColor = new Color(0.035f, 0.035f, 0.035f);
			style.overflow = Overflow.Hidden;
			_image = new Image {
				scaleMode = ScaleMode.ScaleToFit,
				pickingMode = PickingMode.Ignore
			};
			_image.style.position = Position.Absolute;
			Add(_image);
			generateVisualContent += DrawOverlay;
			RegisterCallback<PointerDownEvent>(OnPointerDown);
			RegisterCallback<PointerMoveEvent>(OnPointerMove);
			RegisterCallback<PointerUpEvent>(OnPointerUp);
			RegisterCallback<WheelEvent>(OnWheel);
			RegisterCallback<GeometryChangedEvent>(_ => UpdateImageLayout());
			RegisterCallback<DetachFromPanelEvent>(_ => ReleaseTexture());
		}

		public void SetSelection(DmdCueAsset cue, DmdLayer layer)
		{
			_selectedCue = cue;
			_selectedLayer = layer;
			MarkDirtyRepaint();
		}

		public void Dispose()
		{
			ReleaseTexture();
		}

		public void SetFrame(DmdSurface surface, DmdProjectAsset project, DmdCanvasMode mode, bool tint)
		{
			if (surface == null) {
				throw new ArgumentNullException(nameof(surface));
			}
			_project = project ?? throw new ArgumentNullException(nameof(project));
			_mode = mode;
			_sourceWidth = surface.Width;
			_sourceHeight = surface.Height;
			var scale = mode == DmdCanvasMode.Dots ? 8 : 1;
			var width = checked(surface.Width * scale);
			var height = checked(surface.Height * scale);
			EnsureTexture(width, height);
			var colors = new Color32[checked(width * height)];
			if (mode == DmdCanvasMode.Dots) {
				for (var index = 0; index < colors.Length; index++) {
					colors[index] = new Color32(0, 0, 0, 255);
				}
			}
			for (var sourceY = 0; sourceY < surface.Height; sourceY++) {
				for (var sourceX = 0; sourceX < surface.Width; sourceX++) {
					var color = ReadColor(surface, sourceX, sourceY, project, tint);
					for (var localY = 0; localY < scale; localY++) {
						for (var localX = 0; localX < scale; localX++) {
							if (mode == DmdCanvasMode.Dots && !InsideDot(localX, localY)) {
								continue;
							}
							var topY = sourceY * scale + localY;
							var textureY = height - 1 - topY;
							colors[textureY * width + sourceX * scale + localX] = color;
						}
					}
				}
			}
			_texture.SetPixels32(colors);
			_texture.Apply(false, false);
			_image.image = _texture;
			UpdateImageLayout();
			MarkDirtyRepaint();
		}

		private static Color32 ReadColor(DmdSurface surface, int x, int y, DmdProjectAsset project, bool tint)
		{
			var index = y * surface.Width + x;
			if (surface.Format == DmdPixelFormat.Rgb24) {
				var offset = index * 3;
				return new Color32(surface.Data[offset], surface.Data[offset + 1], surface.Data[offset + 2], 255);
			}
			var value = surface.Data[index];
			if (!tint) {
				return new Color32(value, value, value, 255);
			}
			var color = Color.Lerp(Color.black, project.PreviewTint, value / 255f);
			color.a = 1f;
			return color;
		}

		private static bool InsideDot(int x, int y)
		{
			var dx = x - 3.5f;
			var dy = y - 3.5f;
			return dx * dx + dy * dy <= 10.6f;
		}

		private void EnsureTexture(int width, int height)
		{
			if (_texture != null && _texture.width == width && _texture.height == height) {
				return;
			}
			ReleaseTexture();
			_texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true) {
				name = "DMD Studio Preview",
				filterMode = FilterMode.Point,
				wrapMode = TextureWrapMode.Clamp,
				hideFlags = HideFlags.HideAndDontSave
			};
		}

		private void ReleaseTexture()
		{
			if (_texture == null) {
				return;
			}
			UnityEngine.Object.DestroyImmediate(_texture);
			_texture = null;
			_image.image = null;
		}

		private void DrawOverlay(MeshGenerationContext context)
		{
			if (_texture == null || _sourceWidth == 0 || _sourceHeight == 0) {
				return;
			}
			var rect = PreviewRect();
			var painter = context.painter2D;
			painter.strokeColor = new Color(1f, 1f, 1f, 0.12f);
			painter.lineWidth = 1f;
			if (_mode == DmdCanvasMode.Raw && rect.width / _sourceWidth >= 4f) {
				for (var x = 1; x < _sourceWidth; x++) {
					var px = rect.x + rect.width * x / _sourceWidth;
					painter.BeginPath();
					painter.MoveTo(new Vector2(px, rect.y));
					painter.LineTo(new Vector2(px, rect.yMax));
					painter.Stroke();
				}
				for (var y = 1; y < _sourceHeight; y++) {
					var py = rect.y + rect.height * y / _sourceHeight;
					painter.BeginPath();
					painter.MoveTo(new Vector2(rect.x, py));
					painter.LineTo(new Vector2(rect.xMax, py));
					painter.Stroke();
				}
			}
			if (_selectedLayer != null) {
				var bounds = LayerBounds(_selectedLayer);
				var left = rect.x + rect.width * bounds.x / _sourceWidth;
				var top = rect.y + rect.height * bounds.y / _sourceHeight;
				var right = rect.x + rect.width * (bounds.x + bounds.width) / _sourceWidth;
				var bottom = rect.y + rect.height * (bounds.y + bounds.height) / _sourceHeight;
				painter.strokeColor = new Color(0.15f, 0.75f, 1f, 0.95f);
				painter.lineWidth = 2f;
				painter.BeginPath();
				painter.MoveTo(new Vector2(left, top));
				painter.LineTo(new Vector2(right, top));
				painter.LineTo(new Vector2(right, bottom));
				painter.LineTo(new Vector2(left, bottom));
				painter.ClosePath();
				painter.Stroke();
			}
		}

		private Rect PreviewRect()
		{
			var available = contentRect;
			available.xMin += 8;
			available.xMax -= 8;
			available.yMin += 8;
			available.yMax -= 8;
			var aspect = (float)_sourceWidth / _sourceHeight;
			var width = System.Math.Max(1f, available.width);
			var height = width / aspect;
			if (height > available.height) {
				height = System.Math.Max(1f, available.height);
				width = height * aspect;
			}
			width *= _zoom;
			height *= _zoom;
			return new Rect(available.center.x - width * 0.5f + _pan.x,
				available.center.y - height * 0.5f + _pan.y, width, height);
		}

		private void UpdateImageLayout()
		{
			if (_sourceWidth == 0 || _sourceHeight == 0) {
				return;
			}
			var rect = PreviewRect();
			_image.style.left = rect.x;
			_image.style.top = rect.y;
			_image.style.width = rect.width;
			_image.style.height = rect.height;
		}

		private RectInt LayerBounds(DmdLayer layer)
		{
			var width = 1;
			var height = 1;
			if (layer is BitmapLayer bitmap && bitmap.Sprite?.Frames?.Count > 0 && bitmap.Sprite.Frames[0] != null) {
				width = bitmap.Sprite.Frames[0].Width;
				height = bitmap.Sprite.Frames[0].Height;
			} else if (layer is ShapeLayer shape) {
				width = System.Math.Max(1, shape.Width);
				height = System.Math.Max(1, shape.Height);
			} else if (layer is TextLayer text && text.Font != null) {
				width = System.Math.Max(1, DmdTextRenderer.Measure(text.Font, text.Text ?? string.Empty));
				height = System.Math.Max(1, text.Font.LineHeight);
			}
			return new RectInt(layer.X, layer.Y, width, height);
		}

		private void OnPointerDown(PointerDownEvent evt)
		{
			if (evt.button == 2 || evt.button == 0 && evt.altKey) {
				_panStart = new Vector2(evt.localPosition.x, evt.localPosition.y);
				_panOrigin = _pan;
				_panning = true;
				this.CapturePointer(evt.pointerId);
				evt.StopPropagation();
				return;
			}
			if (evt.button != 0 || _selectedLayer == null || _selectedCue == null || !PreviewRect().Contains(evt.localPosition)) {
				return;
			}
			_dragStart = evt.localPosition;
			_dragX = _selectedLayer.X;
			_dragY = _selectedLayer.Y;
			Undo.RecordObject(_selectedCue, "Move DMD layer");
			_dragging = true;
			this.CapturePointer(evt.pointerId);
			evt.StopPropagation();
		}

		private void OnPointerMove(PointerMoveEvent evt)
		{
			if (_panning && this.HasPointerCapture(evt.pointerId)) {
				var position = new Vector2(evt.localPosition.x, evt.localPosition.y);
				_pan = _panOrigin + position - _panStart;
				UpdateImageLayout();
				MarkDirtyRepaint();
				return;
			}
			if (!_dragging || !this.HasPointerCapture(evt.pointerId)) {
				return;
			}
			var rect = PreviewRect();
			var dx = Mathf.RoundToInt((evt.localPosition.x - _dragStart.x) * _sourceWidth / rect.width);
			var dy = Mathf.RoundToInt((evt.localPosition.y - _dragStart.y) * _sourceHeight / rect.height);
			if (_selectedLayer.X == _dragX + dx && _selectedLayer.Y == _dragY + dy) {
				return;
			}
			_selectedLayer.X = _dragX + dx;
			_selectedLayer.Y = _dragY + dy;
			EditorUtility.SetDirty(_selectedCue);
			LayerPositionChanged?.Invoke();
			MarkDirtyRepaint();
		}

		private void OnPointerUp(PointerUpEvent evt)
		{
			if (_panning && (evt.button == 2 || evt.button == 0)) {
				_panning = false;
				if (this.HasPointerCapture(evt.pointerId)) {
					this.ReleasePointer(evt.pointerId);
				}
				return;
			}
			if (!_dragging || evt.button != 0) {
				return;
			}
			_dragging = false;
			if (this.HasPointerCapture(evt.pointerId)) {
				this.ReleasePointer(evt.pointerId);
			}
		}

		private void OnWheel(WheelEvent evt)
		{
			if (_sourceWidth == 0 || _sourceHeight == 0) {
				return;
			}
			var oldRect = PreviewRect();
			var pointer = new Vector2(evt.localMousePosition.x, evt.localMousePosition.y);
			var normalized = new Vector2((pointer.x - oldRect.x) / oldRect.width,
				(pointer.y - oldRect.y) / oldRect.height);
			var factor = evt.delta.y > 0f ? 0.8f : 1.25f;
			var next = Mathf.Clamp(_zoom * factor, 0.5f, 16f);
			if (Mathf.Approximately(next, _zoom)) {
				return;
			}
			_zoom = next;
			var nextRect = PreviewRect();
			var nextPoint = new Vector2(nextRect.x + normalized.x * nextRect.width,
				nextRect.y + normalized.y * nextRect.height);
			_pan += pointer - nextPoint;
			UpdateImageLayout();
			MarkDirtyRepaint();
			evt.StopPropagation();
		}
	}
}
