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

using Unity.Mathematics;
using VisualPinball.Engine.Common;

namespace VisualPinball.Unity.Simulation
{
	/// <summary>
	/// Render-time interpolation of ball snapshots.
	///
	/// <para>Physics runs at 1 kHz on the simulation thread, the main thread
	/// renders at 60-144 Hz. Rendering the latest snapshot as-is samples the
	/// ball at a random phase within the tick, so the on-screen distance per
	/// frame varies by up to one tick (~7% at 60 fps), which reads as judder.
	/// Instead, the main thread renders the ball at a render time that
	/// advances with the wall clock, <see cref="RenderDelayUsec"/> behind the
	/// simulation clock, and interpolates between the last four physics poses
	/// that every snapshot carries.</para>
	/// </summary>
	internal static class BallSnapshotInterpolation
	{
		/// <summary>
		/// How far behind the simulation clock balls are rendered.
		/// </summary>
		/// <remarks>
		/// The render time spans one tick plus the tick's publish delay between
		/// two snapshots, and the newest pose lies anywhere in [simulation time,
		/// simulation time + one step). A delay of 1.5 steps keeps the render
		/// time within the four poses for publish delays up to half a step.
		/// </remarks>
		internal const long RenderDelayUsec = PhysicsConstants.PhysicsStepTime * 3 / 2;

		/// <summary>
		/// Physics time to render at.
		/// </summary>
		/// <param name="simulationTimeUsec">Simulation time of the latest snapshot.</param>
		/// <param name="elapsedUsec">Wall-clock time since that snapshot's tick was scheduled.</param>
		/// <param name="timeScale">Simulation clock scale (slow motion, time-lapse).</param>
		/// <remarks>
		/// The simulation clock maps to the wall clock through the tick
		/// schedule, so this advances smoothly with the main thread's clock,
		/// whatever the phase between frame and tick.
		/// </remarks>
		internal static long RenderTimeUsec(long simulationTimeUsec, long elapsedUsec, float timeScale)
		{
			return simulationTimeUsec + (long)(math.max(0L, elapsedUsec) * (double)timeScale) - RenderDelayUsec;
		}

		/// <summary>
		/// Finds the pose segment containing the render time.
		/// </summary>
		/// <param name="renderTimeUsec">Physics time to render at.</param>
		/// <param name="time0">Physics time of the current pose.</param>
		/// <param name="time1">Physics time of the pose one step back.</param>
		/// <param name="time2">Physics time of the pose two steps back.</param>
		/// <param name="time3">Physics time of the pose three steps back.</param>
		/// <param name="segment">Index of the newer pose of the segment; the older one is <c>segment + 1</c>.</param>
		/// <returns>Interpolation factor from the older to the newer pose, clamped to [0, 1].</returns>
		internal static float Segment(long renderTimeUsec, long time0, long time1, long time2, long time3, out int segment)
		{
			if (renderTimeUsec >= time1 || time2 >= time1) {
				segment = 0;
				return Factor(renderTimeUsec, time1, time0);
			}
			if (renderTimeUsec >= time2 || time3 >= time2) {
				segment = 1;
				return Factor(renderTimeUsec, time2, time1);
			}
			segment = 2;
			return Factor(renderTimeUsec, time3, time2);
		}

		/// <summary>
		/// Whether a ball moved further than physically possible between two
		/// poses (teleporter, kicker transfer, manual placement). Such a jump
		/// is snapped, never interpolated.
		/// </summary>
		internal static bool IsTeleport(float3 from, float3 to, float radius)
		{
			var maxDistance = 2f * radius;
			return math.distancesq(from, to) > maxDistance * maxDistance;
		}

		private static float Factor(long timeUsec, long fromUsec, long toUsec)
		{
			return toUsec > fromUsec
				? math.saturate((float)((double)(timeUsec - fromUsec) / (toUsec - fromUsec)))
				: 1f;
		}
	}
}
