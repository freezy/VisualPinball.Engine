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
	public sealed class CueInstanceState
	{
		public NumberTweenState[] NumberTweens = Array.Empty<NumberTweenState>();

		internal BoundText[] BoundTexts = Array.Empty<BoundText>();
		internal string[] TextTemplates = Array.Empty<string>();
		internal string[] ResolvedTexts = Array.Empty<string>();
		internal int[] TextVersions = Array.Empty<int>();
		internal bool[] TextInitialized = Array.Empty<bool>();
		internal char[][] NumberBuffers = Array.Empty<char[]>();

		internal void EnsureLayerCount(int count)
		{
			if (NumberTweens.Length == count) {
				return;
			}
			NumberTweens = new NumberTweenState[count];
			BoundTexts = new BoundText[count];
			TextTemplates = new string[count];
			ResolvedTexts = new string[count];
			TextVersions = new int[count];
			TextInitialized = new bool[count];
			NumberBuffers = new char[count][];
		}

		public struct NumberTweenState
		{
			public double StartValue;
			public double TargetValue;
			public int StartTick;
			public bool Initialized;
		}
	}
}
