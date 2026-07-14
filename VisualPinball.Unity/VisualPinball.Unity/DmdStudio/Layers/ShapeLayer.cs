// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;

namespace VisualPinball.Unity
{
	public enum DmdShapeType
	{
		Rect,
		HLine,
		VLine,
		Dot,
	}

	[Serializable]
	public class ShapeLayer : DmdLayer
	{
		public DmdShapeType Shape;
		public int Width;
		public int Height;
		public DmdShade Shade = DmdShade.White;
		public bool Filled;
	}
}
