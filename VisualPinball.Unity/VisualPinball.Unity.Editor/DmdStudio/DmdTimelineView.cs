// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Linq;
using UnityEditor;
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
		private DragKind _dragKind;
		private int _dragLayerIndex = -1;
		private int _dragTrackIndex = -1;
		private int _dragKeyIndex = -1;
		private int _dragOriginFrame;
		private int _dragOriginStart;
		private int _dragOriginEnd;
		private int _selectedLayerIndex = -1;
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
		public event Action<int> LayerSelected;
		public event Action AssetChanged;

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

		public void SetCue(DmdCueAsset cue, int frameRate, int selectedLayerIndex = -1)
		{
			_cue = cue;
			_frameRate = System.Math.Max(1, frameRate);
			_selectedLayerIndex = selectedLayerIndex;
			Frame = _frame;
			style.height = HeaderHeight + System.Math.Max(1, cue?.Layers?.Count ?? 0) * RowHeight;
			RefreshLabels();
			MarkDirtyRepaint();
		}

		public bool AddKeyframe(int layerIndex, DmdAnimatableProperty property, int frame, float value)
		{
			if (!TryLayer(layerIndex, out var layer)) {
				return false;
			}
			Undo.RecordObject(_cue, "Add DMD keyframe");
			layer.Tracks ??= new System.Collections.Generic.List<DmdPropertyTrack>();
			var track = layer.Tracks.LastOrDefault(candidate => candidate != null && candidate.Property == property);
			if (track == null) {
				track = new DmdPropertyTrack { Property = property };
				layer.Tracks.Add(track);
			}
			track.Keys ??= new System.Collections.Generic.List<DmdKeyframe>();
			frame = Mathf.Clamp(frame, 0, MaxFrame);
			var index = track.Keys.FindIndex(key => key.Frame == frame);
			var keyframe = new DmdKeyframe { Frame = frame, Value = value, Interp = DmdInterpolation.Linear };
			if (index >= 0) {
				track.Keys[index] = keyframe;
			} else {
				track.Keys.Add(keyframe);
				track.Keys.Sort((left, right) => left.Frame.CompareTo(right.Frame));
			}
			CommitEdit();
			return true;
		}

		public bool SetLayerSpan(int layerIndex, int startFrame, int endFrame)
		{
			if (!TryLayer(layerIndex, out var layer)) return false;
			Undo.RecordObject(_cue, "Set DMD layer span");
			layer.StartFrame = Mathf.Clamp(startFrame, 0, MaxFrame);
			layer.EndFrame = Mathf.Clamp(endFrame, layer.StartFrame, MaxFrame);
			CommitEdit();
			return true;
		}

		public bool SetTransitionDuration(bool enter, int duration)
		{
			if (_cue == null) return false;
			Undo.RecordObject(_cue, "Set DMD transition duration");
			if (enter) {
				var transition = _cue.EnterTransition;
				transition.DurationFrames = ClampTransition(duration, _cue.ExitTransition.DurationFrames);
				_cue.EnterTransition = transition;
			} else {
				var transition = _cue.ExitTransition;
				transition.DurationFrames = ClampTransition(duration, _cue.EnterTransition.DurationFrames);
				_cue.ExitTransition = transition;
			}
			CommitEdit();
			return true;
		}

		public bool SetSpriteFrameDuration(DmdSpriteAsset sprite, int frameIndex, int duration)
		{
			if (sprite?.Frames == null || frameIndex < 0 || frameIndex >= sprite.Frames.Count) {
				return false;
			}
			Undo.RecordObject(sprite, "Set DMD sprite frame duration");
			sprite.FrameDurations ??= new System.Collections.Generic.List<int>();
			while (sprite.FrameDurations.Count < sprite.Frames.Count) {
				sprite.FrameDurations.Add(1);
			}
			if (sprite.FrameDurations.Count > sprite.Frames.Count) {
				sprite.FrameDurations.RemoveRange(sprite.Frames.Count,
					sprite.FrameDurations.Count - sprite.Frames.Count);
			}
			sprite.FrameDurations[frameIndex] = System.Math.Max(1, duration);
			EditorUtility.SetDirty(sprite);
			AssetChanged?.Invoke();
			MarkDirtyRepaint();
			return true;
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
						index == _selectedLayerIndex
							? new Color(0.2f, 0.68f, 0.95f, layer.Visible ? 0.88f : 0.35f)
							: new Color(0.15f, 0.55f, 0.8f, layer.Visible ? 0.72f : 0.25f));
					DrawSpanHandles(painter, left, right, top);
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

		private static void DrawSpanHandles(Painter2D painter, float left, float right, float top)
		{
			FillRect(painter, new Rect(left - 2f, top + 3f, 4f, RowHeight - 6f),
				new Color(0.8f, 0.92f, 1f, 0.9f));
			FillRect(painter, new Rect(right - 2f, top + 3f, 4f, RowHeight - 6f),
				new Color(0.8f, 0.92f, 1f, 0.9f));
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
			var hitFrame = FrameAt(evt.localPosition.x);
			_dragKind = HitTest(evt.localPosition, hitFrame);
			_dragOriginFrame = hitFrame;
			if (_dragKind != DragKind.Scrub && _cue != null) {
				Undo.RecordObject(_cue, DragUndoName(_dragKind));
			}
			this.CapturePointer(evt.pointerId);
			ApplyDrag(hitFrame);
		}

		private void OnPointerMove(PointerMoveEvent evt)
		{
			if (_dragKind != DragKind.None && this.HasPointerCapture(evt.pointerId)) {
				ApplyDrag(FrameAt(evt.localPosition.x));
			}
		}

		private void OnPointerUp(PointerUpEvent evt)
		{
			if (_dragKind == DragKind.None || evt.button != 0) {
				return;
			}
			var edited = _dragKind != DragKind.Scrub;
			_dragKind = DragKind.None;
			if (this.HasPointerCapture(evt.pointerId)) {
				this.ReleasePointer(evt.pointerId);
			}
			if (edited) {
				CommitEdit();
			}
		}

		private int FrameAt(float x)
		{
			var width = System.Math.Max(1f, contentRect.width - LabelWidth - 8f);
			return Mathf.RoundToInt(Mathf.Clamp01((x - LabelWidth) / width) * MaxFrame);
		}

		private DragKind HitTest(Vector2 position, int hitFrame)
		{
			_dragLayerIndex = -1;
			_dragTrackIndex = -1;
			_dragKeyIndex = -1;
			if (_cue == null) {
				return DragKind.Scrub;
			}
			var threshold = System.Math.Max(1, Mathf.CeilToInt(MaxFrame * 6f /
				System.Math.Max(1f, contentRect.width - LabelWidth - 8f)));
			if (position.y < HeaderHeight) {
				if (System.Math.Abs(hitFrame - _cue.EnterTransition.DurationFrames) <= threshold) {
					return DragKind.EnterTransition;
				}
				var exitStart = MaxFrame - _cue.ExitTransition.DurationFrames;
				if (System.Math.Abs(hitFrame - exitStart) <= threshold) {
					return DragKind.ExitTransition;
				}
				return DragKind.Scrub;
			}
			var layerIndex = Mathf.FloorToInt((position.y - HeaderHeight) / RowHeight);
			if (!TryLayer(layerIndex, out var layer)) {
				return DragKind.Scrub;
			}
			_dragLayerIndex = layerIndex;
			_selectedLayerIndex = layerIndex;
			LayerSelected?.Invoke(layerIndex);
			if (TryHitKeyframe(layer, hitFrame, threshold, out _dragTrackIndex, out _dragKeyIndex)) {
				return DragKind.Keyframe;
			}
			var start = Mathf.Clamp(layer.StartFrame, 0, MaxFrame);
			var end = layer.EndFrame <= 0 ? MaxFrame : Mathf.Clamp(layer.EndFrame, start, MaxFrame);
			_dragOriginStart = start;
			_dragOriginEnd = end;
			if (System.Math.Abs(hitFrame - start) <= threshold) return DragKind.SpanStart;
			if (System.Math.Abs(hitFrame - end) <= threshold) return DragKind.SpanEnd;
			return hitFrame >= start && hitFrame <= end ? DragKind.SpanMove : DragKind.Scrub;
		}

		private void ApplyDrag(int hitFrame)
		{
			switch (_dragKind) {
				case DragKind.Scrub:
					Frame = hitFrame;
					FrameChanged?.Invoke(Frame);
					break;
				case DragKind.SpanStart:
					if (TryLayer(_dragLayerIndex, out var startLayer)) {
						startLayer.StartFrame = Mathf.Clamp(hitFrame, 0,
							startLayer.EndFrame > 0 ? startLayer.EndFrame : MaxFrame);
					}
					break;
				case DragKind.SpanEnd:
					if (TryLayer(_dragLayerIndex, out var endLayer)) {
						endLayer.EndFrame = Mathf.Clamp(hitFrame, endLayer.StartFrame, MaxFrame);
					}
					break;
				case DragKind.SpanMove:
					if (TryLayer(_dragLayerIndex, out var moveLayer)) {
						var length = _dragOriginEnd - _dragOriginStart;
						var start = Mathf.Clamp(_dragOriginStart + hitFrame - _dragOriginFrame, 0,
							System.Math.Max(0, MaxFrame - length));
						moveLayer.StartFrame = start;
						moveLayer.EndFrame = start + length;
					}
					break;
				case DragKind.EnterTransition:
					var enter = _cue.EnterTransition;
					enter.DurationFrames = ClampTransition(hitFrame, _cue.ExitTransition.DurationFrames);
					_cue.EnterTransition = enter;
					break;
				case DragKind.ExitTransition:
					var exit = _cue.ExitTransition;
					exit.DurationFrames = ClampTransition(MaxFrame - hitFrame,
						_cue.EnterTransition.DurationFrames);
					_cue.ExitTransition = exit;
					break;
				case DragKind.Keyframe:
					MoveKeyframe(hitFrame);
					break;
			}
			if (_dragKind != DragKind.Scrub) {
				EditorUtility.SetDirty(_cue);
				RefreshLabels();
				MarkDirtyRepaint();
			}
		}

		private int ClampTransition(int duration, int otherDuration)
		{
			var max = DmdValidation.MaxTransitionFrames;
			if (_cue.DurationFrames > 0 && !_cue.Loop) {
				max = System.Math.Min(max, System.Math.Max(0, _cue.DurationFrames - otherDuration));
			}
			return Mathf.Clamp(duration, 0, max);
		}

		private void MoveKeyframe(int frame)
		{
			if (!TryLayer(_dragLayerIndex, out var layer) || layer.Tracks == null ||
			    _dragTrackIndex < 0 || _dragTrackIndex >= layer.Tracks.Count) {
				return;
			}
			var track = layer.Tracks[_dragTrackIndex];
			if (track?.Keys == null || _dragKeyIndex < 0 || _dragKeyIndex >= track.Keys.Count) {
				return;
			}
			var key = track.Keys[_dragKeyIndex];
			key.Frame = Mathf.Clamp(frame, 0, MaxFrame);
			track.Keys[_dragKeyIndex] = key;
			track.Keys.Sort((left, right) => left.Frame.CompareTo(right.Frame));
			_dragKeyIndex = track.Keys.FindIndex(candidate => candidate.Frame == key.Frame &&
				Mathf.Approximately(candidate.Value, key.Value));
		}

		private static bool TryHitKeyframe(DmdLayer layer, int frame, int threshold, out int trackIndex,
			out int keyIndex)
		{
			if (layer.Tracks != null) {
				for (trackIndex = layer.Tracks.Count - 1; trackIndex >= 0; trackIndex--) {
					var keys = layer.Tracks[trackIndex]?.Keys;
					if (keys == null) continue;
					for (keyIndex = keys.Count - 1; keyIndex >= 0; keyIndex--) {
						if (System.Math.Abs(keys[keyIndex].Frame - frame) <= threshold) return true;
					}
				}
			}
			trackIndex = keyIndex = -1;
			return false;
		}

		private bool TryLayer(int index, out DmdLayer layer)
		{
			layer = null;
			if (_cue?.Layers == null || index < 0 || index >= _cue.Layers.Count) return false;
			layer = _cue.Layers[index];
			return layer != null;
		}

		private void CommitEdit()
		{
			EditorUtility.SetDirty(_cue);
			RefreshLabels();
			MarkDirtyRepaint();
			AssetChanged?.Invoke();
		}

		private static string DragUndoName(DragKind kind)
		{
			return kind switch {
				DragKind.Keyframe => "Move DMD keyframe",
				DragKind.EnterTransition or DragKind.ExitTransition => "Set DMD transition duration",
				_ => "Set DMD layer span"
			};
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

		private enum DragKind
		{
			None,
			Scrub,
			SpanStart,
			SpanEnd,
			SpanMove,
			EnterTransition,
			ExitTransition,
			Keyframe,
		}
	}
}
