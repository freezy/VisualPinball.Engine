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
	/// Instrumentation of the profile build: what each part of VPE costs per frame, and what
	/// happened around a stutter.
	/// </summary>
	///
	/// <remarks>
	/// <para>
	/// This API exists in every build, but its calls only do something in a profile build,
	/// which defines <c>VPE_PROFILE</c>. Everywhere else the compiler removes them, including
	/// the evaluation of their arguments, because every method is
	/// <c>[Conditional("VPE_PROFILE")]</c>, returns nothing and has no <c>out</c> parameter. A
	/// test enforces that.
	/// </para>
	/// <para>
	/// The recorders that implement it come with the profile build's instrumentation.
	/// </para>
	/// <para>
	/// A section is measured with <see cref="Begin"/> and <see cref="End"/> on the same thread.
	/// Don't wrap them in a <c>using</c> scope: a conditional method can't remove that.
	/// </para>
	/// </remarks>
	public static class VpeProfiler
	{
		/// <summary>
		/// Whether this is a profile build. Use the define for code that needs to compile out;
		/// this is for the few places that only need to know, like the window title.
		/// </summary>
		public static bool IsProfileBuild =>
#if VPE_PROFILE
			true;
#else
			false;
#endif

		/// <summary>
		/// A section starts on the current thread.
		/// </summary>
		[Conditional("VPE_PROFILE")]
		public static void Begin(ProfileSection section)
		{
		}

		/// <summary>
		/// The section started last on the current thread ends.
		/// </summary>
		[Conditional("VPE_PROFILE")]
		public static void End(ProfileSection section)
		{
		}

		/// <summary>
		/// Something happened that may explain a slow frame, like the first multiball or a
		/// display change. Main thread only.
		/// </summary>
		[Conditional("VPE_PROFILE")]
		public static void Mark(ProfileEvent evt, int value = 0)
		{
		}

		/// <summary>
		/// Counts something per frame, like DMD frames uploaded. Any thread.
		/// </summary>
		[Conditional("VPE_PROFILE")]
		public static void Count(ProfileCounter counter, int amount = 1)
		{
		}
	}
}
