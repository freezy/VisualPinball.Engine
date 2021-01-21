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
using UnityEngine;
using VisualPinball.Engine.Game.Engines;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity
{
	/// <summary>
	/// A game logic engine is the part that handles the game. <p/>
	///
	/// There will typically be implementations for PinMAME and MPF.
	/// </summary>
	public interface IGamelogicEngine
	{
		string Name { get; }

		void OnInit(Player player, TableApi tableApi, BallManager ballManager);

		#region Switches

		/// <summary>
		/// A list of available switches supported by the game logic engine.
		/// </summary>
		GamelogicEngineSwitch[] AvailableSwitches { get; }

		/// <summary>
		/// Enables or disables a switch.
		/// </summary>
		/// <param name="id">Name of the switch, as defined by <see cref="AvailableSwitches"/>.</param>
		/// <param name="isClosed">True for normally closed (NC) i.e. contact, a.k.a. "enabled". False for normally open (NO), i.e. no contact, a.k.a "off".</param>
		void Switch(string id, bool isClosed);

		#endregion

		#region Lamps

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

		/// <summary>
		/// Triggered when the an RGB lamp changes color.
		/// </summary>
		event EventHandler<LampColorEventArgs> OnLampColorChanged;

		#endregion

		#region Coils

		/// <summary>
		/// A list of available coils.
		/// </summary>
		GamelogicEngineCoil[] AvailableCoils { get; }

		/// <summary>
		/// Triggered when a coil is enabled or disabled.
		/// </summary>
		event EventHandler<CoilEventArgs> OnCoilChanged;

		#endregion
	}

	public readonly struct CoilEventArgs
	{
		/// <summary>
		/// Id of the coil, as defined by <see cref="IGamelogicEngine.AvailableCoils"/>.
		/// </summary>
		public readonly string Id;

		/// <summary>
		/// State of the coil, true if the coil is under voltage, false if not.
		/// </summary>
		public readonly bool IsEnabled;

		public CoilEventArgs(string id, bool isEnabled)
		{
			Id = id;
			IsEnabled = isEnabled;
		}
	}

	public readonly struct LampEventArgs
	{
		/// <summary>
		/// Id of the lamp, as defined by <see cref="IGamelogicEngine.AvailableLamps"/>.
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

	public readonly struct LampColorEventArgs
	{
		/// <summary>
		/// Id of the lamp, as defined by <see cref="IGamelogicEngine.AvailableLamps"/>.
		/// </summary>
		public readonly string Id;

		/// <summary>
		/// New color
		/// </summary>
		public readonly Color Color;

		public LampColorEventArgs(string id, Color color)
		{
			Id = id;
			Color = color;
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
