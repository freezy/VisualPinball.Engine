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

namespace VisualPinball.Engine.Game.Engine
{
	/// <summary>
	/// A game logic engine working with switches. <p/>
	///
	/// It provides a list of available switches and a method for triggering
	/// them.
	/// </summary>
	public interface IGamelogicEngineWithSwitches
	{
		/// <summary>
		/// A list of available switches supported by the game logic engine.
		/// </summary>
		string[] AvailableSwitches { get; }

		/// <summary>
		/// Enables or disables a switch.
		/// </summary>
		/// <param name="id">Name of the switch, as defined by <see cref="AvailableSwitches"/>.</param>
		/// <param name="normallyClosed">True for normally closed (NC) i.e. contact, a.k.a. "on". False for normally open (NO), i.e. no contact, a.k.a "off".</param>
		void Switch(string id, bool normallyClosed);
	}
}
