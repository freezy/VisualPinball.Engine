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
	public static class DmdQuantizer
	{
		public static void I8ToDmd2(byte[] luminance, byte[] destination)
		{
			ValidateBuffers(luminance, destination);
			for (var index = 0; index < luminance.Length; index++) {
				destination[index] = (byte)(luminance[index] >> 6);
			}
		}

		public static void I8ToDmd4(byte[] luminance, byte[] destination)
		{
			ValidateBuffers(luminance, destination);
			for (var index = 0; index < luminance.Length; index++) {
				destination[index] = (byte)(luminance[index] >> 4);
			}
		}

		private static void ValidateBuffers(byte[] source, byte[] destination)
		{
			if (source == null) {
				throw new ArgumentNullException(nameof(source));
			}
			if (destination == null) {
				throw new ArgumentNullException(nameof(destination));
			}
			if (source.Length != destination.Length) {
				throw new ArgumentException("Quantizer buffers must have matching lengths.", nameof(destination));
			}
		}
	}
}
