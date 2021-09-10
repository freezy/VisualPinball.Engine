// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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

using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.VPT.Flipper
{
	public interface IFlipperData
	{
		float PosX { get; }
		float PosY { get; }
		float StartAngle { get; }
		float BaseRadius { get; }
		float RubberThickness { get; }
		float EndRadius { get; }
		float FlipperRadius { get; }
		float Height { get; }
		float RubberWidth { get; }
		float RubberHeight { get; }
	}
}
