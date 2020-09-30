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

namespace VisualPinball.Engine.Game.Engine
{
	/// <summary>
	/// A game logic engine working with coils (solenoids). <p/>
	///
	/// It provides a list of available coils and an event handler to trigger
	/// them.
	/// </summary>
	public interface IGamelogicEngineWithCoils
	{
		/// <summary>
		/// A list of available coils.
		/// </summary>
		string[] AvailableCoils { get; }

		/// <summary>
		/// Triggered when a coil is enabled or disabled.
		/// </summary>
		event EventHandler<CoilEventArgs> OnCoilChanged;
	}

	public readonly struct CoilEventArgs
	{
		/// <summary>
		/// Name of the coil, as defined by <see cref="IGamelogicEngineWithCoils.AvailableCoils"/>.
		/// </summary>
		public readonly string Name;

		/// <summary>
		/// State of the coil, true if the coil is under voltage, false if not.
		/// </summary>
		public readonly bool IsEnabled;

		public CoilEventArgs(string name, bool isEnabled)
		{
			Name = name;
			IsEnabled = isEnabled;
		}
	}
}
