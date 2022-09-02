// Visual Pinball Engine
// Copyright (C) 2022 freezy and VPE Team
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

namespace VisualPinball.Unity
{
	/// <summary>
	/// A game logic engine is the part that handles the game. <p/>
	///
	/// There will typically be implementations for PinMAME and MPF.
	/// </summary>
	public interface IGamelogicEngine : IGamelogicBridge
	{
		string Name { get; }

		void OnInit(Player player, TableApi tableApi, BallManager ballManager);

		#region Displays

		/// <summary>
		/// Emitted during gameplay when a new display is requested.
		/// </summary>
		///
		/// <remarks>
		/// The reason we don't have a `AvailableDisplays` at design time is because
		/// most GLEs only know about their displays when they start the game.
		/// </remarks>
		event EventHandler<RequestedDisplays> OnDisplaysRequested;

		/// <summary>
		/// Emitted by the display player when a display clear is requested.
		/// </summary>
		event EventHandler<string> OnDisplayClear;

		/// <summary>
		/// Emitted by the display player when a display frame update is requested.
		/// </summary>
		event EventHandler<DisplayFrameData> OnDisplayUpdateFrame;

		/// <summary>
		/// Indicate a display has been updated.
		/// </summary>
		void DisplayChanged(DisplayFrameData displayFrameData);

		#endregion

		#region Switches

		/// <summary>
		/// A list of available switches supported by the game logic engine.
		/// </summary>
		GamelogicEngineSwitch[] RequestedSwitches { get; }

		/// <summary>
		/// Enables or disables a switch.
		/// </summary>
		/// <param name="id">Name of the switch, as defined by <see cref="RequestedSwitches"/>.</param>
		/// <param name="isClosed">True for normally closed (NC) i.e. contact, a.k.a. "enabled". False for normally open (NO), i.e. no contact, a.k.a "off".</param>
		void Switch(string id, bool isClosed);

		#endregion

		#region Lamps

		/// <summary>
		/// A list of available lamps.
		/// </summary>
		GamelogicEngineLamp[] RequestedLamps { get; }

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

		#endregion

		#region Coils

		/// <summary>
		/// A list of available coils.
		/// </summary>
		GamelogicEngineCoil[] RequestedCoils { get; }

		/// <summary>
		/// Triggered when a coil is enabled or disabled.
		/// </summary>
		event EventHandler<CoilEventArgs> OnCoilChanged;

		#endregion

		#region Wires

		/// <summary>
		/// A list of *dynamic* wires.
		/// </summary>
		GamelogicEngineWire[] AvailableWires { get; }

		#endregion

		/// <summary>
		/// This event is triggered when the gamelogic has booted up and is ready to handle events.
		/// </summary>
		event EventHandler<EventArgs> OnStarted;
	}

	/// <summary>
	/// Sometimes we want to extend an existing GLE with other components. This
	/// API allows other components to be able to drive the GLE.
	/// </summary>
	public interface IGamelogicBridge
	{
		void SetCoil(string id, bool isEnabled);

		void SetLamp(string id, float value, bool isCoil = false, LampSource source = LampSource.Lamp);

		bool GetSwitch(string id);
		bool GetCoil(string id);
		LampState GetLamp(string id);

		public event EventHandler<SwitchEventArgs2> OnSwitchChanged;

		// todo displays
	}

	public class RequestedDisplays
	{
		public readonly DisplayConfig[] Displays;

		public RequestedDisplays(DisplayConfig[] availableDisplays)
		{
			Displays = availableDisplays;
		}

		public RequestedDisplays(DisplayConfig config)
		{
			Displays = new [] { config };
		}
	}

	[Serializable]
	public class DisplayConfig
	{
		public readonly string Id;
		public readonly int Width;
		public readonly int Height;
		public readonly bool FlipX;

		public DisplayConfig(string id, int width, int height)
		{
			Id = id;
			Width = width;
			Height = height;
		}

		public DisplayConfig(string id, uint width, uint height)
		{
			Id = id;
			Width = (int)width;
			Height = (int)height;
		}

		public DisplayConfig(string id, uint width, uint height, bool flipX)
		{
			Id = id;
			Width = (int)width;
			Height = (int)height;
			FlipX = flipX;
		}
	}

	public enum DisplayFrameFormat
	{
		Dmd2, // 2-bit (0-4)
		Dmd4, // 4-bit (0-15)
		Dmd8, // 8-bit (0-255)
		Dmd24, // rgb (3x 0-255)
		Segment,
		AlphaNumeric, // gets a byte-array converted string
		Numeric       // gets a byte-array converted float
	}

	public class DisplayFrameData
	{
		public readonly string Id;
		public readonly byte[] Data;
		public readonly DisplayFrameFormat Format;
		public readonly float Brightness = 1;

		public DisplayFrameData(string id, DisplayFrameFormat format, byte[] data)
		{
			Id = id;
			Format = format;
			Data = data;
		}
	}

	public readonly struct CoilEventArgs
	{
		/// <summary>
		/// Id of the coil, as defined by <see cref="IGamelogicEngine.RequestedCoils"/>.
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
		/// ID of the lamp, as defined by <see cref="IGamelogicEngine.RequestedLamps"/>.
		/// </summary>
		public readonly string Id;

		/// <summary>
		/// The intensity of the light. The range is dependent on the GLE,
		/// i.e. PinMAME sends 0-255 or sometimes 0-8 for GI. MPF sends 0-1.
		/// This value get normalized *after* the mapping.
		/// </summary>
		public readonly float Value;

		/// <summary>
		/// Source which triggered the lamp.
		/// </summary>
		public readonly LampSource Source;

		/// <summary>
		/// True if it was triggered by a coil.
		/// </summary>
		public readonly bool IsCoil;

		public LampEventArgs(string id, float value, LampSource source = LampSource.Lamp)
		{
			Id = id;
			Value = value;
			Source = source;
			IsCoil = false;
		}

		public LampEventArgs(string id, float value, bool isCoil, LampSource source = LampSource.Lamp)
		{
			Id = id;
			Value = value;
			Source = source;
			IsCoil = isCoil;
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

	public readonly struct SwitchEventArgs2
	{
		public readonly string Id;
		public readonly bool IsEnabled;

		public SwitchEventArgs2(string id, bool isEnabled)
		{
			Id = id;
			IsEnabled = isEnabled;
		}
	}

	public readonly struct DisplayChangedEventArgs
	{
		public readonly DisplayFrameData DisplayFrameData;

		public DisplayChangedEventArgs(DisplayFrameData displayFrameData)
		{
			DisplayFrameData = displayFrameData;
		}
	}

}
