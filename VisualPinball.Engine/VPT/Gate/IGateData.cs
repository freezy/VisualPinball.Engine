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

namespace VisualPinball.Engine.VPT.Gate
{
	public interface IGateData
	{
		public float Rotation { get; }
		public float Length { get; }
		public float PosX { get; }
		public float PosY { get; }

		public bool ShowBracket { get; }
		public float Height { get; }
	}

	public interface IGateColliderData
	{
		public float AngleMin { get; set; }
		public float AngleMax { get; set; }
		public bool TwoWay { get; }
	}
}
