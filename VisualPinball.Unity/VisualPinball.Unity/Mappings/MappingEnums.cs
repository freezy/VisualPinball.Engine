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

	public enum ESwitchConstant
	{
		Closed = 0,
		Open = 1,
	}

	public enum ESwitchType
	{
		OnOff = 0,
		Pulse = 1,
	}

	public enum ECoilDestination
	{
		Playfield = 0,
		Lamp = 1,
	}

	public enum ECoilType
	{
		SingleWound = 0,
		DualWound = 1,
	}

	public enum EWireType
	{
		OnOff = 0,
		Pulse = 1,
	}
}
