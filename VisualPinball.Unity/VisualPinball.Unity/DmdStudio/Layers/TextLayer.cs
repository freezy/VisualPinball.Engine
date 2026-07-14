// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using Newtonsoft.Json;

namespace VisualPinball.Unity
{
	public enum DmdAnchor
	{
		TopLeft,
		TopCenter,
		TopRight,
		MiddleLeft,
		MiddleCenter,
		MiddleRight,
		BottomLeft,
		BottomCenter,
		BottomRight,
		BaselineLeft,
		BaselineCenter,
		BaselineRight,
	}

	public enum DmdTextEffect
	{
		None,
		Outline,
		Shadow,
		Inverse,
	}

	public enum DmdOverflow
	{
		Clip,
		Marquee,
	}

	[Serializable]
	public class TextLayer : DmdLayer
	{
		[JsonIgnore] public DmdFontAsset Font;
		public string Text;
		public DmdAnchor Anchor;
		public DmdTextEffect Effect;
		public DmdShade Shade = DmdShade.White;
		public DmdShade OutlineShade = DmdShade.Black;
		public DmdOverflow Overflow;
		public int MarqueeSpeed;
	}
}
