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
	public enum DmdBlendMode
	{
		Opaque,
		Alpha,
		Add,
		Invert,
	}

	[Serializable]
	public abstract class DmdLayer
	{
		public string Name;
		public bool Visible = true;
		public int X;
		public int Y;
		public DmdBlendMode Blend = DmdBlendMode.Alpha;
		public float Opacity = 1f;
		public int StartFrame;
		public int EndFrame;
		public List<DmdPropertyTrack> Tracks = new List<DmdPropertyTrack>();
	}
}
