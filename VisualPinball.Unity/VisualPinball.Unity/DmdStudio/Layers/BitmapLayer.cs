// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using Newtonsoft.Json;
using UnityEngine;

namespace VisualPinball.Unity
{
	public enum DmdLoopMode
	{
		Once,
		Loop,
		PingPong,
		HoldLast,
	}

	[Serializable]
	public class BitmapLayer : DmdLayer
	{
		[JsonIgnore] public DmdSpriteAsset Sprite;
		public DmdLoopMode Loop;
		public int SpriteStartFrame;
		public Color32 Tint = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
	}
}
