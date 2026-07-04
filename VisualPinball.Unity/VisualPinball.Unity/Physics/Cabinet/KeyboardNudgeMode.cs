// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
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

namespace VisualPinball.Unity
{
	/// <summary>
	/// Selects how a digital keyboard/button nudge is converted into cabinet
	/// acceleration.
	/// </summary>
	public enum KeyboardNudgeMode
	{
		/// <summary>
		/// Legacy VP-style instant shove/retract pulse.
		/// </summary>
		PushRetract = 0,

		/// <summary>
		/// Legacy VP box model where a table-space offset springs back to rest.
		/// </summary>
		BoxModel = 1,

		/// <summary>
		/// Cabinet oscillator model introduced for this nudge stack.
		/// </summary>
		CabModel = 2
	}
}
