// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace VisualPinball.Unity
{
	[Serializable]
	public struct DmdGlyph
	{
		public int Codepoint;
		public int X;
		public int Y;
		public int W;
		public int H;
		public int OffsetX;
		public int OffsetY;
		public int Advance;
	}

	[Serializable]
	public struct DmdKerningPair
	{
		public int LeftCodepoint;
		public int RightCodepoint;
		public int Adjustment;
	}

	[CreateAssetMenu(fileName = "DmdFont", menuName = "Pinball/DMD/Font", order = 313)]
	public class DmdFontAsset : ScriptableObject
	{
		public string Notes;
		public DmdBitmapData Atlas;
		public List<DmdGlyph> Glyphs = new List<DmdGlyph>();
		public int LineHeight;
		public int Baseline;
		public int Tracking;
		public List<DmdKerningPair> Kerning = new List<DmdKerningPair>();
		public int DigitWidth;

		public DmdValidationResult Validate() => DmdValidation.Validate(this);
	}
}
