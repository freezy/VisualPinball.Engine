// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>
	/// A layer color carrying both monochrome intensity and RGB color.
	/// </summary>
	[Serializable]
	public struct DmdShade
	{
		public byte Intensity;
		public Color32 Color;

		public static readonly DmdShade White = new DmdShade {
			Intensity = byte.MaxValue,
			Color = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue)
		};

		public static readonly DmdShade Black = new DmdShade {
			Intensity = 0,
			Color = new Color32(0, 0, 0, byte.MaxValue)
		};
	}
}
