// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System.Text;

namespace VisualPinball.Unity.Test
{
	internal static class DmdSurfaceAssert
	{
		public static readonly bool RegenerateGoldenHashes = false;
		private const string Shades = " .:-=+*#%@";

		public static uint Hash(byte[] data)
		{
			var hash = 2166136261u;
			foreach (var value in data) {
				hash ^= value;
				hash *= 16777619u;
			}
			return hash;
		}

		public static string ToAscii(DmdSurface surface)
		{
			var output = new StringBuilder();
			for (var y = 0; y < surface.Height; y++) {
				if (y > 0) {
					output.Append('\n');
				}
				for (var x = 0; x < surface.Width; x++) {
					var offset = (y * surface.Width + x) * (surface.Format == DmdPixelFormat.I8 ? 1 : 3);
					var value = surface.Format == DmdPixelFormat.I8
						? surface.Data[offset]
						: (byte)((77 * surface.Data[offset] + 150 * surface.Data[offset + 1] +
						          29 * surface.Data[offset + 2] + 128) >> 8);
					output.Append(Shades[value * (Shades.Length - 1) / 255]);
				}
			}
			return output.ToString();
		}
	}
}
