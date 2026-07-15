// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	public sealed class DmdTimelineView : VisualElement
	{
		private const float HeaderHeight = 24f;
		private const float RowHeight = 22f;
		private const float LabelWidth = 100f;

		private DmdCueAsset _cue;
		private int _frameRate = 30;
		private int _frame;
		private bool _scrubbing;
		private readonly VisualElement _labels;

		public int Frame {
			get => _frame;
			set {
				var clamped = Mathf.Clamp(value, 0, MaxFrame);
				if (_frame == clamped) {
					return;
				}
				_frame = clamped;
				MarkDirtyRepaint();
			}
		}

		public int MaxFrame => ResolveMaxFrame();
		public event Action<int> FrameChanged;

		public DmdTimelineView()
		{
			name = "dmd-timeline";
			focusable = true;
			style.minHeight = 90;
			style.flexGrow = 1;
			_labels = new VisualElement {
				pickingMode = PickingMode.Ignore
			};
			_labels.style.position = Position.Absolute;
			_labels.style.left = 0f;
			_labels.style.top = 0f;
			_labels.style.right = 0f;
			_labels.style.bottom = 0f;
			Add(_labels);
			generateVisualContent += Draw;
			RegisterCallback<PointerDownEvent>(OnPointerDown);
			RegisterCallback<PointerMoveEvent>(OnPointerMove);
			RegisterCallback<PointerUpEvent>(OnPointerUp);
		}

		public void SetCue(DmdCueAsset cue, int frameRate)
		{
			_cue = cue;
			_frameRate = System.Math.Max(1, frameRate);
			Frame = _frame;
			style.height = HeaderHeight + System.Math.Max(1, cue?.Layers?.Count ?? 0) * RowHeight;
			RefreshLabels();
			MarkDirtyRepaint();
		}

		private void Draw(MeshGenerationContext context)
		{
			var painter = context.painter2D;
			FillRect(painter, contentRect, new Color(0.12f, 0.12f, 0.12f));

			var maxFrame = System.Math.Max(1, MaxFrame);
			var trackWidth = System.Math.Max(1f, contentRect.width - LabelWidth - 8f);
			var rulerLeft = LabelWidth;
			DrawTransitionRegions(painter, rulerLeft, trackWidth, maxFrame);
			var tickStep = System.Math.Max(1, _frameRate / 2);
			painter.strokeColor = new Color(1f, 1f, 1f, 0.24f);
			painter.lineWidth = 1f;
			for (var frame = 0; frame <= maxFrame; frame += tickStep) {
				var x = rulerLeft + trackWidth * frame / maxFrame;
				painter.BeginPath();
				painter.MoveTo(new Vector2(x, HeaderHeight - (frame % _frameRate == 0 ? 10f : 5f)));
				painter.LineTo(new Vector2(x, HeaderHeight));
				painter.Stroke();
			}

			if (_cue?.Layers != null) {
				for (var index = 0; index < _cue.Layers.Count; index++) {
					var layer = _cue.Layers[index];
					var top = HeaderHeight + index * RowHeight;
					painter.strokeColor = new Color(1f, 1f, 1f, 0.08f);
					painter.BeginPath();
					painter.MoveTo(new Vector2(0f, top + RowHeight));
					painter.LineTo(new Vector2(contentRect.width, top + RowHeight));
					painter.Stroke();
					if (layer == null) {
						continue;
					}
					var start = Mathf.Clamp(layer.StartFrame, 0, maxFrame);
					var end = layer.EndFrame <= 0 ? maxFrame : Mathf.Clamp(layer.EndFrame, start, maxFrame);
					var left = rulerLeft + trackWidth * start / maxFrame;
					var right = rulerLeft + trackWidth * end / maxFrame;
					FillRect(painter,
						new Rect(left, top + 3f, System.Math.Max(2f, right - left), RowHeight - 6f),
						new Color(0.15f, 0.55f, 0.8f, layer.Visible ? 0.72f : 0.25f));
					DrawSpriteBoundaries(painter, layer, top, start, end, rulerLeft, trackWidth, maxFrame);
					DrawKeyframes(painter, layer, top, rulerLeft, trackWidth, maxFrame);
				}
			}

			var playheadX = rulerLeft + trackWidth * Frame / maxFrame;
			painter.strokeColor = new Color(1f, 0.35f, 0.1f);
			painter.lineWidth = 2f;
			painter.BeginPath();
			painter.MoveTo(new Vector2(playheadX, 0f));
			painter.LineTo(new Vector2(playheadX, contentRect.height));
			painter.Stroke();
		}

		private void DrawTransitionRegions(Painter2D painter, float rulerLeft, float trackWidth, int maxFrame)
		{
			if (_cue == null) {
				return;
			}
			var enter = Mathf.Clamp(_cue.EnterTransition.DurationFrames, 0, maxFrame);
			if (enter > 0) {
				FillRect(painter, new Rect(rulerLeft, HeaderHeight, trackWidth * enter / maxFrame,
					contentRect.height - HeaderHeight), new Color(0.2f, 0.8f, 0.4f, 0.12f));
			}
			var exit = Mathf.Clamp(_cue.ExitTransition.DurationFrames, 0, maxFrame);
			if (exit > 0) {
				var width = trackWidth * exit / maxFrame;
				FillRect(painter, new Rect(rulerLeft + trackWidth - width, HeaderHeight, width,
					contentRect.height - HeaderHeight), new Color(0.95f, 0.35f, 0.2f, 0.12f));
			}
		}

		private static void DrawSpriteBoundaries(Painter2D painter, DmdLayer layer, float rowTop,
			int start, int end, float rulerLeft, float trackWidth, int maxFrame)
		{
			if (layer is not BitmapLayer bitmap || bitmap.Sprite?.FrameDurations == null ||
			    bitmap.Sprite.FrameDurations.Count < 2) {
				return;
			}
			var frame = start;
			for (var index = 0; index < bitmap.Sprite.FrameDurations.Count - 1 && frame < end; index++) {
				frame += System.Math.Max(1, bitmap.Sprite.FrameDurations[index]);
				if (frame >= end) {
					break;
				}
				var x = rulerLeft + trackWidth * frame / maxFrame;
				painter.strokeColor = new Color(1f, 1f, 1f, 0.38f);
				painter.lineWidth = 1f;
				painter.BeginPath();
				painter.MoveTo(new Vector2(x, rowTop + 4f));
				painter.LineTo(new Vector2(x, rowTop + RowHeight - 4f));
				painter.Stroke();
			}
		}

		private static void DrawKeyframes(Painter2D painter, DmdLayer layer, float rowTop,
			float rulerLeft, float trackWidth, int maxFrame)
		{
			if (layer.Tracks == null) {
				return;
			}
			foreach (var track in layer.Tracks) {
				if (track?.Keys == null) {
					continue;
				}
				foreach (var key in track.Keys) {
					var x = rulerLeft + trackWidth * Mathf.Clamp(key.Frame, 0, maxFrame) / maxFrame;
					var y = rowTop + RowHeight * 0.5f;
					painter.fillColor = new Color(1f, 0.8f, 0.15f, 0.95f);
					painter.BeginPath();
					painter.MoveTo(new Vector2(x, y - 4f));
					painter.LineTo(new Vector2(x + 4f, y));
					painter.LineTo(new Vector2(x, y + 4f));
					painter.LineTo(new Vector2(x - 4f, y));
					painter.ClosePath();
					painter.Fill();
				}
			}
		}

		private void RefreshLabels()
		{
			_labels.Clear();
			if (_cue?.Layers == null) {
				return;
			}
			for (var index = 0; index < _cue.Layers.Count; index++) {
				var layer = _cue.Layers[index];
				var label = new Label(string.IsNullOrWhiteSpace(layer?.Name)
					? layer?.GetType().Name ?? "Missing"
					: layer.Name) {
					pickingMode = PickingMode.Ignore
				};
				label.style.position = Position.Absolute;
				label.style.left = 4f;
				label.style.top = HeaderHeight + index * RowHeight + 2f;
				label.style.width = LabelWidth - 8f;
				label.style.height = RowHeight - 4f;
				label.style.unityTextAlign = TextAnchor.MiddleLeft;
				label.style.textOverflow = TextOverflow.Ellipsis;
				_labels.Add(label);
			}
		}

		private int ResolveMaxFrame()
		{
			var max = System.Math.Max(1, _cue?.DurationFrames ?? 0);
			if (_cue?.Layers == null) {
				return max;
			}
			foreach (var layer in _cue.Layers) {
				if (layer == null) {
					continue;
				}
				max = System.Math.Max(max, layer.EndFrame > 0 ? layer.EndFrame : layer.StartFrame + 1);
			}
			return max;
		}

		private void OnPointerDown(PointerDownEvent evt)
		{
			if (evt.button != 0 || evt.localPosition.x < LabelWidth) {
				return;
			}
			_scrubbing = true;
			this.CapturePointer(evt.pointerId);
			Scrub(evt.localPosition.x);
		}

		private void OnPointerMove(PointerMoveEvent evt)
		{
			if (_scrubbing && this.HasPointerCapture(evt.pointerId)) {
				Scrub(evt.localPosition.x);
			}
		}

		private void OnPointerUp(PointerUpEvent evt)
		{
			if (!_scrubbing || evt.button != 0) {
				return;
			}
			_scrubbing = false;
			if (this.HasPointerCapture(evt.pointerId)) {
				this.ReleasePointer(evt.pointerId);
			}
		}

		private void Scrub(float x)
		{
			var width = System.Math.Max(1f, contentRect.width - LabelWidth - 8f);
			var frame = Mathf.RoundToInt(Mathf.Clamp01((x - LabelWidth) / width) * MaxFrame);
			Frame = frame;
			FrameChanged?.Invoke(Frame);
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
	}
}
