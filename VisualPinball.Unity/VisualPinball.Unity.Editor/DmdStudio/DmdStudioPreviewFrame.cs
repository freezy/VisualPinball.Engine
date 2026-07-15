// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// Converts the compositor surface into the exact default wire format used by a project and
	/// expands mono levels back to I8 solely for the tinted editor canvas.
	/// </summary>
	public sealed class DmdStudioPreviewFrame
	{
		private DmdSurface _canvasSurface;
		private byte[] _displayData = Array.Empty<byte>();

		public DmdSurface CanvasSurface { get; private set; }
		public DisplayFrameFormat Format { get; private set; }
		public byte[] DisplayData { get; private set; }

		public void Prepare(DmdSurface rendered, DmdColorMode colorMode)
		{
			if (rendered == null) {
				throw new ArgumentNullException(nameof(rendered));
			}
			if (colorMode == DmdColorMode.Rgb24) {
				if (rendered.Format != DmdPixelFormat.Rgb24) {
					throw new ArgumentException("An RGB preview requires an RGB24 compositor surface.", nameof(rendered));
				}
				CanvasSurface = rendered;
				Format = DisplayFrameFormat.Dmd24;
				DisplayData = rendered.Data;
				return;
			}
			if (rendered.Format != DmdPixelFormat.I8) {
				throw new ArgumentException("A mono preview requires an I8 compositor surface.", nameof(rendered));
			}

			EnsureMonoBuffers(rendered.Width, rendered.Height);
			var multiplier = 17;
			switch (colorMode) {
				case DmdColorMode.Mono4:
					Format = DisplayFrameFormat.Dmd2;
					multiplier = 85;
					DmdQuantizer.I8ToDmd2(rendered.Data, _displayData);
					break;
				case DmdColorMode.Mono16:
					Format = DisplayFrameFormat.Dmd4;
					DmdQuantizer.I8ToDmd4(rendered.Data, _displayData);
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(colorMode));
			}
			for (var index = 0; index < _displayData.Length; index++) {
				_canvasSurface.Data[index] = (byte)(_displayData[index] * multiplier);
			}
			CanvasSurface = _canvasSurface;
			DisplayData = _displayData;
		}

		private void EnsureMonoBuffers(int width, int height)
		{
			var length = checked(width * height);
			if (_canvasSurface == null || _canvasSurface.Width != width || _canvasSurface.Height != height) {
				_canvasSurface = new DmdSurface(width, height, DmdPixelFormat.I8);
			}
			if (_displayData.Length != length) {
				_displayData = new byte[length];
			}
		}
	}
}
