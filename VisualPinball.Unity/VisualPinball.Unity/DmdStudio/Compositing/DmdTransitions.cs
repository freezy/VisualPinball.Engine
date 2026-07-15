// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using Unity.Mathematics;

namespace VisualPinball.Unity
{
	public static class DmdTransitions
	{
		private static readonly byte[] Bayer4x4 = {
			0, 8, 2, 10,
			12, 4, 14, 6,
			3, 11, 1, 9,
			15, 7, 13, 5,
		};

		public static void Compose(DmdSurface dst, DmdSurface from, DmdSurface to,
			DmdTransitionType type, DmdDirection direction, float progress)
		{
			ValidateSurfaces(dst, from, to);
			if (type < DmdTransitionType.Cut || type > DmdTransitionType.ScrollOff) {
				throw new ArgumentOutOfRangeException(nameof(type));
			}
			if (direction < DmdDirection.Left || direction > DmdDirection.Down) {
				throw new ArgumentOutOfRangeException(nameof(direction));
			}
			if (float.IsNaN(progress) || float.IsInfinity(progress)) {
				throw new ArgumentOutOfRangeException(nameof(progress));
			}
			progress = math.clamp(progress, 0f, 1f);
			if (type == DmdTransitionType.Cut || progress >= 1f) {
				dst.CopyFrom(to);
				return;
			}
			if (progress <= 0f) {
				dst.CopyFrom(from);
				return;
			}

			switch (type) {
				case DmdTransitionType.Push:
					ComposePush(dst, from, to, direction, progress);
					break;
				case DmdTransitionType.Cover:
					ComposeCover(dst, from, to, direction, progress);
					break;
				case DmdTransitionType.Uncover:
					ComposeUncover(dst, from, to, direction, progress);
					break;
				case DmdTransitionType.ScrollOff:
					ComposeScrollOff(dst, from, direction, progress);
					break;
				case DmdTransitionType.WipeOn:
					ComposeWipe(dst, from, to, direction, progress);
					break;
				case DmdTransitionType.SplitIn:
					ComposeSplit(dst, from, to, direction, progress, true);
					break;
				case DmdTransitionType.SplitOut:
					ComposeSplit(dst, from, to, direction, progress, false);
					break;
				case DmdTransitionType.Dissolve:
					ComposeDissolve(dst, from, to, progress);
					break;
				case DmdTransitionType.FadeThroughBlack:
					ComposeFadeThroughBlack(dst, from, to, progress);
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(type));
			}
		}

		private static void ComposePush(DmdSurface dst, DmdSurface from, DmdSurface to,
			DmdDirection direction, float progress)
		{
			Motion(direction, progress, dst.Width, dst.Height, out var fromX, out var fromY);
			StartOffset(direction, dst.Width, dst.Height, out var startX, out var startY);
			var toX = startX + fromX;
			var toY = startY + fromY;
			for (var y = 0; y < dst.Height; y++) {
				for (var x = 0; x < dst.Width; x++) {
					if (!TryCopyTranslatedPixel(dst, to, x, y, toX, toY)) {
						TryCopyTranslatedPixel(dst, from, x, y, fromX, fromY);
					}
				}
			}
		}

		private static void ComposeCover(DmdSurface dst, DmdSurface from, DmdSurface to,
			DmdDirection direction, float progress)
		{
			Motion(direction, progress, dst.Width, dst.Height, out var motionX, out var motionY);
			StartOffset(direction, dst.Width, dst.Height, out var startX, out var startY);
			var toX = startX + motionX;
			var toY = startY + motionY;
			dst.CopyFrom(from);
			for (var y = 0; y < dst.Height; y++) {
				for (var x = 0; x < dst.Width; x++) {
					TryCopyTranslatedPixel(dst, to, x, y, toX, toY);
				}
			}
		}

		private static void ComposeUncover(DmdSurface dst, DmdSurface from, DmdSurface to,
			DmdDirection direction, float progress)
		{
			Motion(direction, progress, dst.Width, dst.Height, out var fromX, out var fromY);
			dst.CopyFrom(to);
			for (var y = 0; y < dst.Height; y++) {
				for (var x = 0; x < dst.Width; x++) {
					TryCopyTranslatedPixel(dst, from, x, y, fromX, fromY);
				}
			}
		}

		private static void ComposeScrollOff(DmdSurface dst, DmdSurface from,
			DmdDirection direction, float progress)
		{
			Motion(direction, progress, dst.Width, dst.Height, out var fromX, out var fromY);
			dst.Clear();
			for (var y = 0; y < dst.Height; y++) {
				for (var x = 0; x < dst.Width; x++) {
					TryCopyTranslatedPixel(dst, from, x, y, fromX, fromY);
				}
			}
		}

		private static void ComposeWipe(DmdSurface dst, DmdSurface from, DmdSurface to,
			DmdDirection direction, float progress)
		{
			var extent = direction == DmdDirection.Left || direction == DmdDirection.Right ? dst.Width : dst.Height;
			var reveal = (int)math.round(progress * extent);
			for (var y = 0; y < dst.Height; y++) {
				for (var x = 0; x < dst.Width; x++) {
					bool showTo;
					switch (direction) {
						case DmdDirection.Left:
							showTo = x >= dst.Width - reveal;
							break;
						case DmdDirection.Right:
							showTo = x < reveal;
							break;
						case DmdDirection.Up:
							showTo = y >= dst.Height - reveal;
							break;
						case DmdDirection.Down:
							showTo = y < reveal;
							break;
						default:
							throw new ArgumentOutOfRangeException(nameof(direction));
					}
					CopyPixel(showTo ? to : from, dst, x, y);
				}
			}
		}

		private static void ComposeSplit(DmdSurface dst, DmdSurface from, DmdSurface to,
			DmdDirection direction, float progress, bool inward)
		{
			var horizontal = direction == DmdDirection.Left || direction == DmdDirection.Right;
			var extent = horizontal ? dst.Width : dst.Height;
			var reveal = (int)math.round(progress * extent);
			for (var y = 0; y < dst.Height; y++) {
				for (var x = 0; x < dst.Width; x++) {
					var coordinate = horizontal ? x : y;
					var doubledDistanceFromCenter = math.abs(2 * coordinate + 1 - extent);
					var showTo = inward
						? doubledDistanceFromCenter >= extent - reveal
						: doubledDistanceFromCenter < reveal;
					CopyPixel(showTo ? to : from, dst, x, y);
				}
			}
		}

		private static void ComposeDissolve(DmdSurface dst, DmdSurface from, DmdSurface to, float progress)
		{
			var threshold = progress * 16f;
			for (var y = 0; y < dst.Height; y++) {
				for (var x = 0; x < dst.Width; x++) {
					var showTo = Bayer4x4[(y & 3) * 4 + (x & 3)] < threshold;
					CopyPixel(showTo ? to : from, dst, x, y);
				}
			}
		}

		private static void ComposeFadeThroughBlack(DmdSurface dst, DmdSurface from, DmdSurface to,
			float progress)
		{
			var source = progress < 0.5f ? from : to;
			var scale = progress < 0.5f ? 1f - progress * 2f : progress * 2f - 1f;
			for (var index = 0; index < dst.Data.Length; index++) {
				dst.Data[index] = (byte)math.clamp((int)math.round(source.Data[index] * scale), 0, 255);
			}
		}

		private static bool TryCopyTranslatedPixel(DmdSurface dst, DmdSurface source,
			int destinationX, int destinationY, int translationX, int translationY)
		{
			var sourceX = destinationX - translationX;
			var sourceY = destinationY - translationY;
			if (sourceX < 0 || sourceX >= source.Width || sourceY < 0 || sourceY >= source.Height) {
				return false;
			}
			CopyPixel(source, dst, sourceX, sourceY, destinationX, destinationY);
			return true;
		}

		private static void CopyPixel(DmdSurface source, DmdSurface destination, int x, int y)
		{
			CopyPixel(source, destination, x, y, x, y);
		}

		private static void CopyPixel(DmdSurface source, DmdSurface destination,
			int sourceX, int sourceY, int destinationX, int destinationY)
		{
			var channels = source.Format == DmdPixelFormat.I8 ? 1 : 3;
			var sourceOffset = (sourceY * source.Width + sourceX) * channels;
			var destinationOffset = (destinationY * destination.Width + destinationX) * channels;
			for (var channel = 0; channel < channels; channel++) {
				destination.Data[destinationOffset + channel] = source.Data[sourceOffset + channel];
			}
		}

		private static void Motion(DmdDirection direction, float progress, int width, int height,
			out int x, out int y)
		{
			x = 0;
			y = 0;
			switch (direction) {
				case DmdDirection.Left:
					x = -(int)math.round(progress * width);
					break;
				case DmdDirection.Right:
					x = (int)math.round(progress * width);
					break;
				case DmdDirection.Up:
					y = -(int)math.round(progress * height);
					break;
				case DmdDirection.Down:
					y = (int)math.round(progress * height);
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(direction));
			}
		}

		private static void StartOffset(DmdDirection direction, int width, int height, out int x, out int y)
		{
			x = 0;
			y = 0;
			switch (direction) {
				case DmdDirection.Left:
					x = width;
					break;
				case DmdDirection.Right:
					x = -width;
					break;
				case DmdDirection.Up:
					y = height;
					break;
				case DmdDirection.Down:
					y = -height;
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(direction));
			}
		}

		private static void ValidateSurfaces(DmdSurface destination, DmdSurface from, DmdSurface to)
		{
			if (destination == null) {
				throw new ArgumentNullException(nameof(destination));
			}
			if (from == null) {
				throw new ArgumentNullException(nameof(from));
			}
			if (to == null) {
				throw new ArgumentNullException(nameof(to));
			}
			if (ReferenceEquals(destination, from) || ReferenceEquals(destination, to)) {
				throw new ArgumentException("Transition output must use a distinct destination surface.");
			}
			if (destination.Width != from.Width || destination.Height != from.Height ||
			    destination.Format != from.Format || destination.Width != to.Width ||
			    destination.Height != to.Height || destination.Format != to.Format) {
				throw new ArgumentException("Transition surfaces must have matching dimensions and formats.");
			}
		}
	}
}
