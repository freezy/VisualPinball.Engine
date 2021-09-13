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

using System.Collections.Generic;
using VisualPinball.Engine.Game.Engines;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Interface for components that support one or more switches.
	/// </summary>
	///
	/// <remarks>
	/// Note that this is only used at editor time so the switch manager
	/// knows which switches to show. For runtime, see <see cref="IApiSwitchDevice"/>.
	/// </remarks>
	public interface ISwitchDeviceComponent : IMainComponent, IDeviceComponent<GamelogicEngineSwitch>
	{
		/// <summary>
		/// A list of available switches supported by the switch device
		/// </summary>
		IEnumerable<GamelogicEngineSwitch> AvailableSwitches { get; }

		/// <summary>
		/// Whether the switch is always NC, NO, or configurable.
		/// </summary>
		SwitchDefault SwitchDefault { get; }
	}
}
