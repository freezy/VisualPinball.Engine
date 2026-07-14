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
	public readonly struct CueHandle : IEquatable<CueHandle>
	{
		internal readonly uint Id;
		internal readonly uint Generation;

		internal CueHandle(uint id, uint generation)
		{
			Id = id;
			Generation = generation;
		}

		public bool IsValid => Id != 0 && Generation != 0;

		public bool Equals(CueHandle other) => Id == other.Id && Generation == other.Generation;
		public override bool Equals(object obj) => obj is CueHandle other && Equals(other);
		public override int GetHashCode() => unchecked(((int)Id * 397) ^ (int)Generation);
		public static bool operator ==(CueHandle left, CueHandle right) => left.Equals(right);
		public static bool operator !=(CueHandle left, CueHandle right) => !left.Equals(right);
		public override string ToString() => IsValid ? $"{Generation}:{Id}" : "Invalid";
	}
}
