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

using System.Diagnostics;

namespace VisualPinball.Unity
{
	/// <summary>
	/// The one clock VPE's timing code shares: <see cref="Stopwatch"/> ticks, as microseconds.
	/// </summary>
	///
	/// <remarks>
	/// <para>
	/// Timestamps are the stopwatch's, so they can be compared with each other across threads.
	/// On Mono the stopwatch counts from the process' start, not from boot like the platform's
	/// performance counter (and Unity's frame timings, or the native input library's
	/// <c>VpeGetTimestampUsec</c>): those need an offset to be compared.
	/// </para>
	/// <para>
	/// The conversion splits whole seconds from the rest, so it's exact and can't overflow.
	/// The naive <c>ticks * 1_000_000 / Frequency</c> overflows a <c>long</c> after about 10.7
	/// days of uptime with a 10 MHz counter.
	/// </para>
	/// </remarks>
	public static class VpeClock
	{
		private static readonly long Frequency = Stopwatch.Frequency;

		/// <summary>
		/// Now, in microseconds of the stopwatch.
		/// </summary>
		public static long NowUsec => TicksToUsec(Stopwatch.GetTimestamp());

		/// <summary>
		/// A <see cref="Stopwatch.GetTimestamp"/> value, or a tick count, in microseconds.
		/// </summary>
		public static long TicksToUsec(long ticks)
		{
			var seconds = ticks / Frequency;
			var rest = ticks - seconds * Frequency;
			return seconds * 1_000_000L + rest * 1_000_000L / Frequency;
		}

		/// <summary>
		/// The time between two <see cref="Stopwatch.GetTimestamp"/> values, in microseconds,
		/// or 0 when the end is before the start.
		/// </summary>
		public static long ElapsedUsec(long startTicks, long endTicks)
		{
			var ticks = endTicks - startTicks;
			return ticks <= 0 ? 0 : TicksToUsec(ticks);
		}
	}
}
