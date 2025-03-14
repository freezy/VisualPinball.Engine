﻿// Visual Pinball Engine
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

namespace VisualPinball.Unity
{
	public struct ScoreReelPackable
	{
		public ScoreReelComponent.ScoreReelDirection Direction;
		public float Speed;
		public float Wait;


		public static byte[] Pack(ScoreReelComponent comp)
		{
			return PackageApi.Packer.Pack(new ScoreReelPackable {
				Direction = comp.Direction,
				Speed = comp.Speed,
				Wait = comp.Wait,
			});
		}

		public static void Unpack(byte[] bytes, ScoreReelComponent comp)
		{
			var data = PackageApi.Packer.Unpack<ScoreReelPackable>(bytes);
			comp.Direction = data.Direction;
			comp.Speed = data.Speed;
			comp.Wait = data.Wait;

		}
	}
}
