// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using UnityEngine;

namespace VisualPinball.Unity
{
	[CreateAssetMenu(fileName = "DmdPalette", menuName = "Pinball/DMD/Palette", order = 314)]
	public class DmdPaletteAsset : ScriptableObject
	{
		public Color32[] Colors = new Color32[16];

		public DmdValidationResult Validate() => DmdValidation.Validate(this);
	}
}
