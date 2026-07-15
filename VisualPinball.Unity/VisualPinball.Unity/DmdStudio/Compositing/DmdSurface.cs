// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;

namespace VisualPinball.Unity
{
	/// <summary>
	/// A top-origin compositor surface.
	/// </summary>
	public sealed class DmdSurface
	{
		public readonly int Width;
		public readonly int Height;
		public readonly DmdPixelFormat Format;
		public readonly byte[] Data;

		public DmdSurface(int width, int height, DmdPixelFormat format)
		{
			if (width < 1 || width > DmdValidation.MaxWidth) {
				throw new ArgumentOutOfRangeException(nameof(width));
			}
			if (height < 1 || height > DmdValidation.MaxHeight) {
				throw new ArgumentOutOfRangeException(nameof(height));
			}

			int bytesPerPixel;
			switch (format) {
				case DmdPixelFormat.I8:
					bytesPerPixel = 1;
					break;
				case DmdPixelFormat.Rgb24:
					bytesPerPixel = 3;
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(format));
			}

			Width = width;
			Height = height;
			Format = format;
			Data = new byte[checked(width * height * bytesPerPixel)];
		}

		public void Clear(byte value = 0)
		{
			if (value == 0) {
				Array.Clear(Data, 0, Data.Length);
				return;
			}
			for (var index = 0; index < Data.Length; index++) {
				Data[index] = value;
			}
		}

		public void CopyFrom(DmdSurface other)
		{
			if (other == null) {
				throw new ArgumentNullException(nameof(other));
			}
			if (Width != other.Width || Height != other.Height || Format != other.Format) {
				throw new ArgumentException("DMD surfaces must have matching dimensions and formats.", nameof(other));
			}
			Buffer.BlockCopy(other.Data, 0, Data, 0, Data.Length);
		}
	}
}
