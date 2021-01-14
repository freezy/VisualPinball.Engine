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

using System;
using VisualPinball.Engine.Game.Engines;
using VisualPinball.Engine.VPT;

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

		/// <summary>
		/// Triggered when multiple lamps are turned on or off at once.
		/// </summary>
		///
		/// <remarks>
		/// This also allows to to group RGB updates, i.e. updating the color
		/// at once instead of each channel individually.
		/// </remarks>
		event EventHandler<LampsEventArgs> OnLampsChanged;
	}

	public readonly struct LampEventArgs
	{
		/// <summary>
		/// Id of the lamp, as defined by <see cref="IGamelogicEngineWithLamps.AvailableLamps"/>.
		/// </summary>
		public readonly string Id;

		/// <summary>
		/// Value of the lamp. Depending on its type, it can be 0/1 for on/off, or 0-255 for
		/// a fading light.
		/// </summary>
		public readonly int Value;

		/// <summary>
		/// Source which triggered the lamp.
		/// </summary>
		public readonly int Source;

		public LampEventArgs(string id, int value)
		{
			Id = id;
			Value = value;
			Source = LampSource.Lamps;
		}

		public LampEventArgs(string id, int value, int source)
		{
			Id = id;
			Value = value;
			Source = source;
		}
	}

	public readonly struct LampsEventArgs
	{
		public readonly LampEventArgs[] LampsChanged;

		public LampsEventArgs(LampEventArgs[] lampsChanged)
		{
			LampsChanged = lampsChanged;
		}
	}
}
