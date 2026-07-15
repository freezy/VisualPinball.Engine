// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace VisualPinball.Unity
{
	public enum DmdColorMode
	{
		Mono4,
		Mono16,
		Rgb24,
	}

	[Serializable]
	public class DmdSampleState
	{
		public string Name;
		public List<DmdParamValue> Values = new List<DmdParamValue>();
	}

	[CreateAssetMenu(fileName = "DmdProject", menuName = "Pinball/DMD/Project", order = 310)]
	public class DmdProjectAsset : ScriptableObject
	{
		public string DisplayId = "dmd0";
		public int Width = 128;
		public int Height = 32;
		public DmdColorMode ColorMode = DmdColorMode.Mono16;
		public int FrameRate = 30;
		[JsonIgnore] public Color PreviewTint = new Color(1f, 0.18f, 0f);

		[JsonIgnore] public List<DmdCueAsset> Cues = new List<DmdCueAsset>();
		[JsonIgnore] public List<DmdSpriteAsset> Sprites = new List<DmdSpriteAsset>();
		[JsonIgnore] public List<DmdFontAsset> Fonts = new List<DmdFontAsset>();
		[JsonIgnore] public List<DmdPaletteAsset> Palettes = new List<DmdPaletteAsset>();

		public List<DmdSampleState> SampleStates = new List<DmdSampleState>();

		public DmdValidationResult Validate() => DmdValidation.Validate(this);
		public DmdValidationResult Normalize() => DmdValidation.Normalize(this);
	}
}
