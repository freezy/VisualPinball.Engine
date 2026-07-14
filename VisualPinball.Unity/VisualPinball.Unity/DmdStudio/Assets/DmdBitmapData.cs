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
	public enum DmdPixelFormat
	{
		I8,
		Rgb24,
	}

	/// <summary>
	/// Top-origin bitmap data used by DMD sprites and font atlases.
	/// </summary>
	[Serializable]
	public class DmdBitmapData
	{
		public int Width;
		public int Height;
		public DmdPixelFormat Format;
		public byte[] Pixels = Array.Empty<byte>();
		public byte[] Alpha = Array.Empty<byte>();

		/// <summary>
		/// Throws when the dimensions or buffers are inconsistent.
		/// </summary>
		public void Validate()
		{
			var result = DmdValidation.Validate(this);
			if (!result.IsValid) {
				throw new DmdValidationException(result.Diagnostics);
			}
		}
	}
}
