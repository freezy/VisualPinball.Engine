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

// ReSharper disable InconsistentNaming

using System;
using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.Game.Engines
{
	[Serializable]
	public class GamelogicEngineLamp : IGamelogicEngineDeviceItem
	{
		/// <summary>
		/// The unique ID of this lamp, as the gamelogic engine addresses it.
		/// </summary>
		public string Id { get; set; }

		/// <summary>
		/// Some gamelogic engines use integers for the ID. In order to avoid repetitive casting, we store it as integer as well.
		/// </summary>
		public int InternalId;

		/// <summary>
		/// Which channel this lamp corresponds to.
		/// </summary>
		public ColorChannel Channel = ColorChannel.Alpha;

		/// <summary>
		/// If it's a fading light, this is the value at maximal intensity.
		/// </summary>
		public int FadingSteps;

		/// <summary>
		/// An optional description of the lamp.
		/// </summary>
		public string Description { get; set; }

		/// <summary>
		/// How the gamelogic engine triggers the lamp. Either through GI or through the normal lamp API.
		/// </summary>
		///
		/// <remarks>
		/// Note that lamps connected to coils will appear under coils.
		/// </remarks>
		public LampSource Source = LampSource.Lamp;

		/// <summary>
		/// A regular expression to match the component on the playfield.
		/// </summary>
		public string DeviceHint;

		/// <summary>
		/// A regular expression to match the lamp component within the component.
		/// </summary>
		public string DeviceItemHint;

		public GamelogicEngineLamp(string id)
		{
			Id = id;
			InternalId = int.TryParse(id, out var internalId) ? internalId : 0;
		}

		public GamelogicEngineLamp(string id, int internalId)
		{
			Id = id;
			InternalId = internalId;
		}
	}

	public enum LampSource
	{
		Lamp = 0,
		GI = 1,
	}
}
