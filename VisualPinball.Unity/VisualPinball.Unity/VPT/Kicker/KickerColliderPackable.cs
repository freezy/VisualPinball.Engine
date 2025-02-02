// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using MemoryPack;

namespace VisualPinball.Unity
{
	[MemoryPackable]
	public readonly partial struct KickerColliderPackable
	{
		public float Scatter { get; }
		public float HitAccuracy { get; }
		public float HitHeight { get; }
		public bool FallThrough { get; }
		public bool FallIn { get; }
		public bool LegacyMode { get; }

		public KickerColliderPackable(float scatter, float hitAccuracy, float hitHeight, bool fallThrough, bool fallIn, bool legacyMode)
		{
			Scatter = scatter;
			HitAccuracy = hitAccuracy;
			HitHeight = hitHeight;
			FallThrough = fallThrough;
			FallIn = fallIn;
			LegacyMode = legacyMode;
		}

		public static KickerColliderPackable Unpack(byte[] data) => MemoryPackSerializer.Deserialize<KickerColliderPackable>(data);

		public byte[] Pack() => MemoryPackSerializer.Serialize(this);
	}
}
