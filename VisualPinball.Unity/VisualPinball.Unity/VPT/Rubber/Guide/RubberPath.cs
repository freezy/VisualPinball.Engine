// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using Unity.Mathematics;

namespace VisualPinball.Unity
{
	public enum RubberPathSource
	{
		Spline,
		Guides,
	}

	public enum RubberColliderMode
	{
		Legacy,
		Physical,
	}

	public enum RubberPathElementType
	{
		FreeSpan,
		SupportedArc,
	}

	[Serializable]
	public struct RubberPathElement
	{
		public RubberPathElementType Type;
		public float2 Start;
		public float2 End;
		public float2 Center;
		public float Radius;
		public float StartAngleRad;
		public float SweepAngleRad;
		public int StartBindingIndex;
		public int EndBindingIndex;
		public float StartDistance;
		public float Length;
	}
}
