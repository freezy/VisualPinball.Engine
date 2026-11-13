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

using System;
using NLog;
using UnityEngine;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	/// <summary>
	/// What the performance instrumentation runs for.
	/// </summary>
	public enum ProfilerPurpose
	{
		/// <summary>It doesn't run: normal play.</summary>
		Off,
		/// <summary>A whole session, asked for with <c>--vpe-profile</c>: incidents are logged, traced and reported.</summary>
		Session,
		/// <summary>A benchmark run: the frames are recorded for its statistics, and it reports them itself.</summary>
		Benchmark,
	}

	/// <summary>
	/// The performance instrumentation: what each part of VPE costs per frame, and what happened
	/// around a stutter.
	/// </summary>
	///
	/// <remarks>
	/// <para>
	/// It's part of every build and off during normal play, where a call here costs one check of
	/// a flag. A benchmark turns it on for its run with <see cref="Start"/> and off again with
	/// <see cref="Stop"/>; <c>--vpe-profile</c> on the command line turns it on for the session.
	/// The recorders only hold memory while it's on.
	/// </para>
	/// <para>
	/// A section is measured with <see cref="Begin"/> and <see cref="End"/> on the same thread.
	/// </para>
	/// </remarks>
	public static class VpeProfiler
	{
		private const string SessionArgument = "-vpe-profile";
		private const string SelfTestArgument = "-vpe-test-perf=";

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private static volatile bool _enabled;

		/// <summary>
		/// Whether the instrumentation records right now.
		/// </summary>
		public static bool IsEnabled => _enabled;

		/// <summary>
		/// What it records for; <see cref="ProfilerPurpose.Off"/> while it doesn't.
		/// </summary>
		public static ProfilerPurpose Purpose { get; private set; }

		/// <summary>
		/// Whether incidents are logged, traced and reported as they happen (not during a benchmark,
		/// which changes the settings all the time and reports its own numbers).
		/// </summary>
		public static bool ReportsIncidents => _enabled && Purpose == ProfilerPurpose.Session;

		/// <summary>
		/// The instrumentation was turned on or off. Main thread.
		/// </summary>
		public static event Action Changed;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
		private static void OnLoad()
		{
			// a play session in the editor that ended without a domain reload left it running
			if (_enabled) {
				Stop();
			}
			foreach (var raw in Environment.GetCommandLineArgs()) {
				// --name is the documented form, -name still works
				var arg = raw.StartsWith("--", StringComparison.Ordinal) ? raw.Substring(1) : raw;
				if (string.Equals(arg, SessionArgument, StringComparison.OrdinalIgnoreCase) || arg.StartsWith(SelfTestArgument, StringComparison.OrdinalIgnoreCase)) {
					Start(ProfilerPurpose.Session);
					break;
				}
			}
		}

		/// <summary>
		/// Turns the instrumentation on. Main thread. Returns false if it already runs for
		/// something else, which keeps it.
		/// </summary>
		public static bool Start(ProfilerPurpose purpose)
		{
			if (purpose == ProfilerPurpose.Off) {
				return false;
			}
			if (_enabled) {
				return Purpose == purpose;
			}
			Purpose = purpose;
			Profiling.FrameRecorder.Start();
			Profiling.ProfileSession.Start();
			_enabled = true;
			Logger.Info("Performance instrumentation on ({Purpose:l}): frame, section and simulation timing is recorded", purpose.ToString().ToLowerInvariant());
			Changed?.Invoke();
			return true;
		}

		/// <summary>
		/// Turns the instrumentation off and frees what it recorded. Main thread. A benchmark only
		/// stops what it started: a session asked for on the command line keeps running.
		/// </summary>
		public static void Stop(ProfilerPurpose purpose)
		{
			if (_enabled && Purpose == purpose) {
				Stop();
			}
		}

		private static void Stop()
		{
			_enabled = false;
			Profiling.ProfileSession.Stop();
			Profiling.FrameRecorder.Stop();
			Purpose = ProfilerPurpose.Off;
			Logger.Info("Performance instrumentation off");
			Changed?.Invoke();
		}

		/// <summary>
		/// A section starts on the current thread.
		/// </summary>
		public static void Begin(ProfileSection section)
		{
			if (_enabled) {
				Profiling.SectionRecorder.Begin(section);
			}
		}

		/// <summary>
		/// The section started last on the current thread ends.
		/// </summary>
		public static void End(ProfileSection section)
		{
			if (_enabled) {
				Profiling.SectionRecorder.End(section);
			}
		}

		/// <summary>
		/// Something happened that may explain a slow frame, like the first multiball or a
		/// display change. Main thread only.
		/// </summary>
		public static void Mark(ProfileEvent evt, int value = 0)
		{
			if (_enabled) {
				Profiling.FrameRecorder.Mark(evt, value);
			}
		}

		/// <summary>
		/// Counts something per frame, like DMD frames uploaded. Any thread.
		/// </summary>
		public static void Count(ProfileCounter counter, int amount = 1)
		{
			if (_enabled) {
				Profiling.FrameRecorder.AddCount(counter, amount);
			}
		}
	}
}
