// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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

namespace VisualPinball.Engine.VPT
{
	/// <summary>
	/// Identifies the type of a game item
	/// </summary>
	///
	/// <remarks>
	/// The values are how they are written and read from the .vpx file, so
	/// they are somewhat important ;)
	/// </remarks>
	public enum ItemType
	{
		Surface = 0,
		Flipper = 1,
		Timer = 2,
		Plunger = 3,
		TextBox = 4,
		Bumper = 5,
		Trigger = 6,
		Light = 7,
		Kicker = 8,
		Decal = 9,
		Gate = 10,
		Spinner = 11,
		Ramp = 12,
		Table = 13,
		LightCenter = 14,
		DragPoint = 15,
		Collection = 16,
		DispReel = 17,
		LightSeq = 18,
		Primitive = 19,
		Flasher = 20,
		Rubber = 21,
		HitTarget = 22,
		Trough = 23,
		Count = 24,
		Invalid = -1,

		// VPE internal
		Ball = 100,
	}
}
