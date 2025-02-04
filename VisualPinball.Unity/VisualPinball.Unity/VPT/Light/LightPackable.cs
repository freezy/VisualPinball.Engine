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

// ReSharper disable MemberCanBePrivate.Global

using MemoryPack;
using VisualPinball.Engine.Game.Engines;
using VisualPinball.Unity.Editor.Packaging;

namespace VisualPinball.Unity
{
	[MemoryPackable]
	public partial struct LightPackable
	{
		public float BulbSize;
		public LampStatus State;
		public string BlinkPattern;
		public int BlinkInterval;
		public float FadeSpeedUp;
		public float FadeSpeedDown;

		public static byte[] Pack(LightComponent comp)
		{
			return PackageApi.Packer.Pack(new LightPackable {
				BulbSize = comp.BulbSize,
				State = comp.State,
				BlinkPattern = comp.BlinkPattern,
				BlinkInterval = comp.BlinkInterval,
				FadeSpeedUp = comp.FadeSpeedUp,
				FadeSpeedDown = comp.FadeSpeedDown,
			});
		}

		public static void Unpack(byte[] bytes, LightComponent comp)
		{
			var data = PackageApi.Packer.Unpack<LightPackable>(bytes);
			comp.BulbSize = data.BulbSize;
			comp.State = data.State;
			comp.BlinkPattern = data.BlinkPattern;
			comp.BlinkInterval = data.BlinkInterval;
			comp.FadeSpeedUp = data.FadeSpeedUp;
			comp.FadeSpeedDown = data.FadeSpeedDown;
		}
	}
}
