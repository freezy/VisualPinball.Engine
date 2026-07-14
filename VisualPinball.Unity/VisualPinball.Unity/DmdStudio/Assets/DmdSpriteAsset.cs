// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System.Collections.Generic;
using UnityEngine;

namespace VisualPinball.Unity
{
	[CreateAssetMenu(fileName = "DmdSprite", menuName = "Pinball/DMD/Sprite", order = 312)]
	public class DmdSpriteAsset : ScriptableObject
	{
		public List<DmdBitmapData> Frames = new List<DmdBitmapData>();
		public List<int> FrameDurations = new List<int>();

		public DmdValidationResult Validate() => DmdValidation.Validate(this);
		public DmdValidationResult Normalize() => DmdValidation.Normalize(this);
	}
}
