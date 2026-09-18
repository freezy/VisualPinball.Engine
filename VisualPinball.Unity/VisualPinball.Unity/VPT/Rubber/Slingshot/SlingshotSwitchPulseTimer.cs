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
	/// The pulse-delivery contract of the plan's 3.2, on main-thread time.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The physics thread can produce a closure and its opening inside one frame, so
	/// draining the event queue would hand the gamelogic engine a zero-length pulse.
	/// This timer delivers the close at once and holds the open back until at least
	/// <see cref="MinimumPulseSeconds"/> after the delivered close; a second close
	/// inside the window extends it instead of opening.
	/// </para>
	/// <para>
	/// It is deliberately a plain value type with an injected clock so it can be tested
	/// without a player, a frame loop or a gamelogic engine.
	/// </para>
	/// </remarks>
	internal struct SlingshotSwitchPulseTimer
	{
		/// <summary>
		/// Half a millisecond of slack on the window, so a frame clock that lands on the
		/// boundary is not pushed a whole frame further by float rounding.
		/// </summary>
		private const float Tolerance = 1e-4f;

		/// <summary>Shortest pulse handed to the gamelogic engine, in seconds.</summary>
		internal float MinimumPulseSeconds;

		/// <summary>Whether the gamelogic engine currently sees the switch closed.</summary>
		internal bool IsEngineClosed { get; private set; }

		/// <summary>Whether an opening edge is waiting for the window to elapse.</summary>
		internal bool IsOpenPending { get; private set; }

		/// <summary>Main-thread time of the last delivered or extended close.</summary>
		internal float CloseTime { get; private set; }

		internal SlingshotSwitchPulseTimer(float minimumPulseSeconds) : this()
		{
			MinimumPulseSeconds = minimumPulseSeconds;
		}

		/// <summary>
		/// Records a closing edge.
		/// </summary>
		/// <returns>True when the gamelogic engine has to be told the switch closed.</returns>
		internal bool OnClose(float now)
		{
			var deliver = !IsEngineClosed;
			IsEngineClosed = true;
			IsOpenPending = false;
			CloseTime = now;
			return deliver;
		}

		/// <summary>
		/// Records an opening edge.
		/// </summary>
		/// <returns>
		/// True when the gamelogic engine may be told right away; false when the open
		/// was deferred and <see cref="Poll"/> will release it.
		/// </returns>
		internal bool OnOpen(float now)
		{
			if (!IsEngineClosed) {
				IsOpenPending = false;
				return false;
			}
			if (IsElapsed(now)) {
				IsEngineClosed = false;
				IsOpenPending = false;
				return true;
			}
			IsOpenPending = true;
			return false;
		}

		/// <summary>
		/// Called once per frame.
		/// </summary>
		/// <returns>True when a deferred open is due now.</returns>
		internal bool Poll(float now)
		{
			if (!IsOpenPending || !IsElapsed(now)) {
				return false;
			}
			IsOpenPending = false;
			IsEngineClosed = false;
			return true;
		}

		/// <summary>Whether the minimum pulse elapsed since the last delivered close.</summary>
		private bool IsElapsed(float now) => now - CloseTime >= MinimumPulseSeconds - Tolerance;
	}
}
