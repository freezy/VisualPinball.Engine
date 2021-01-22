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

// ReSharper disable BuiltInTypeReferenceStyle

using System;

namespace VisualPinball.Engine.Game.Engines
{
	/// <summary>
	/// A switch declaration.<p/>
	///
	/// Gamelogic engines and switch devices use this to announce which inputs
	/// they expect.
	/// </summary>
	///
	/// <remarks>
	/// This class isn't used during gameplay, but serves to declare the properties
	/// that will then used in the mapping.
	/// </remarks>
	public class GamelogicEngineSwitch
	{
		/// <summary>
		/// A unique identifier. This is what VPE uses to identify a switch.
		/// </summary>
		public readonly string Id;

		/// <summary>
		/// A numerical identifier that can be used in gamelogic engines that
		/// are tied to numerical identifiers.
		/// </summary>
		public readonly int InternalId;

		/// <summary>
		/// If true, inverts the signal, i.e. disabled switches return "closed" (true),
		/// while enabled switched return "open" (false).
		/// </summary>
		public bool NormallyClosed;

		public string Description;
		public string InputActionHint;
		public string InputMapHint;
		public string PlayfieldItemHint;
		public string DeviceHint;
		public string DeviceItemHint;
		public SwitchConstantHint ConstantHint = SwitchConstantHint.None;

		public GamelogicEngineSwitch(string id)
		{
			Id = id;
			InternalId = int.TryParse(id, out var internalId) ? internalId : 0;
		}

		public GamelogicEngineSwitch(string id, int internalId)
		{
			Id = id;
			InternalId = internalId;
		}
	}

	public enum SwitchConstantHint
	{
		None, AlwaysOpen, AlwaysClosed
	}
}
