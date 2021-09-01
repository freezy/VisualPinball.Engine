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


namespace VisualPinball.Unity
{
	public enum ESwitchSource
	{
		InputSystem = 0,
		Playfield = 1,
		Constant = 2,
	}

	public enum SwitchConstant
	{
		Closed = 0,
		Open = 1,
	}

	public enum SwitchType
	{
		OnOff = 0,
		Pulse = 1,
	}

	public enum CoilDestination
	{
		Playfield = 0,
		Lamp = 1,
	}

	public enum CoilType
	{
		SingleWound = 0,
		DualWound = 1,
	}

	public enum WireType
	{
		OnOff = 0,
		Pulse = 1,
	}

	public enum LampSource
	{
		Lamps = 0,
		Coils = 1,
	}

	public enum LampType
	{
		SingleOnOff = 0,
		SingleFading = 1,
		RgbMulti = 2,
		Rgb = 3,
	}
}
