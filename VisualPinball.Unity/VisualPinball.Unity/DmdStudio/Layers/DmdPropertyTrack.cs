// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Collections.Generic;

namespace VisualPinball.Unity
{
	public enum DmdAnimatableProperty
	{
		X,
		Y,
		Opacity,
		SpriteFrame,
	}

	public enum DmdInterpolation
	{
		Step,
		Linear,
	}

	[Serializable]
	public struct DmdKeyframe
	{
		public int Frame;
		public float Value;
		public DmdInterpolation Interp;
	}

	[Serializable]
	public class DmdPropertyTrack
	{
		public DmdAnimatableProperty Property;
		public List<DmdKeyframe> Keys = new List<DmdKeyframe>();
	}
}
