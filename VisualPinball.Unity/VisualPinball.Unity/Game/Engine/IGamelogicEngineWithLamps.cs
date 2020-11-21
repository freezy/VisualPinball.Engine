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

using System;
using VisualPinball.Engine.Game.Engines;

namespace VisualPinball.Unity
{
	/// <summary>
	/// A game logic engine working with lamps <p/>
	///
	/// It provides a list of available lamps and an event handler to trigger
	/// them.
	/// </summary>
	public interface IGamelogicEngineWithLamps
	{
		/// <summary>
		/// A list of available lamps.
		/// </summary>
		GamelogicEngineLamp[] AvailableLamps { get; }

		/// <summary>
		/// Triggered when a lamp is turned on or off.
		/// </summary>
		event EventHandler<LampEventArgs> OnLampChanged;
	}

	public readonly struct LampEventArgs
	{
		/// <summary>
		/// Id of the lamp, as defined by <see cref="IGamelogicEngineWithLamps.AvailableLamps"/>.
		/// </summary>
		public readonly string Id;

		/// <summary>
		/// State of the lamp, true if the lamp is illuminated, false if not.
		/// </summary>
		public readonly bool IsOn;

		public LampEventArgs(string id, bool isOn)
		{
			Id = id;
			IsOn = isOn;
		}
	}
}
