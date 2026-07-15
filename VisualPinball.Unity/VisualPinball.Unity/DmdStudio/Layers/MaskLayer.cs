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
	[Serializable]
	public class MaskLayer : DmdLayer
	{
		[JsonIgnore] public DmdSpriteAsset Mask;
		public DmdLoopMode Loop;
		public int SpriteStartFrame;
	}
}
