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
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using VisualPinball.Plugins;

namespace VisualPinball.Unity
{
	/// <summary>
	/// A few dozen timestamped milestones of something slow and one-off, like starting the
	/// player or loading a table: where the time went.
	/// </summary>
	///
	/// <remarks>
	/// <para>
	/// Each milestone records wall time, the process' CPU time and the GC count, so a phase
	/// that waited (for disk or worker threads) can be told apart from one that worked.
	/// Durations are wall time between consecutive milestones.
	/// </para>
	/// <para>
	/// Cheap enough for every build: a milestone allocates a little and reads the process
	/// times, so it's not for per-frame use. Main thread only.
	/// </para>
	/// </remarks>
	public sealed class Timeline
	{
		/// <summary>
		/// One point in the timeline.
		/// </summary>
		public readonly struct Milestone
		{
			public readonly string Name;
			/// <summary>Stopwatch ticks.</summary>
			public readonly long Ticks;
			/// <summary>Process CPU time in ms (all threads), or -1 if unknown.</summary>
			public readonly double CpuMs;
			/// <summary>GC.CollectionCount(0), or -1 if unknown.</summary>
			public readonly int GcCount;

			public Milestone(string name, long ticks, double cpuMs, int gcCount)
			{
				Name = name;
				Ticks = ticks;
				CpuMs = cpuMs;
				GcCount = gcCount;
			}
		}

		private readonly List<Milestone> _milestones = new();
		private readonly List<KeyValuePair<string, string>> _facts = new();

		public string Name { get; }

		/// <summary>
		/// Stopwatch ticks of the timeline's start, the first milestone.
		/// </summary>
		public long StartTicks => _milestones[0].Ticks;

		public IReadOnlyList<Milestone> Milestones => _milestones;

		/// <summary>
		/// Things observed along the way, like whether a cache was hit.
		/// </summary>
		public IReadOnlyList<KeyValuePair<string, string>> Facts => _facts;

		/// <summary>
		/// Starts a timeline now.
		/// </summary>
		public Timeline(string name, string startMilestone)
		{
			Name = name;
			_milestones.Add(new Milestone(startMilestone, Stopwatch.GetTimestamp(), ProcessCpuMs(), GC.CollectionCount(0)));
		}

		/// <summary>
		/// Starts a timeline at a moment in the past, e.g. when the process was created.
		/// </summary>
		public Timeline(string name, string startMilestone, long startTicks, double startCpuMs)
		{
			Name = name;
			_milestones.Add(new Milestone(startMilestone, startTicks, startCpuMs, -1));
		}

		/// <summary>
		/// Records that something just finished.
		/// </summary>
		public void Mark(string milestone)
		{
			_milestones.Add(new Milestone(milestone, Stopwatch.GetTimestamp(), ProcessCpuMs(), GC.CollectionCount(0)));
		}

		/// <summary>
		/// Records a fact, replacing an earlier one with the same key.
		/// </summary>
		public void Fact(string key, string value)
		{
			for (var i = 0; i < _facts.Count; i++) {
				if (_facts[i].Key == key) {
					_facts[i] = new KeyValuePair<string, string>(key, value);
					return;
				}
			}
			_facts.Add(new KeyValuePair<string, string>(key, value));
		}

		public string GetFact(string key)
		{
			foreach (var fact in _facts) {
				if (fact.Key == key) {
					return fact.Value;
				}
			}
			return null;
		}

		/// <summary>
		/// Wall time from the start to the given milestone in ms, or -1 if it wasn't reached.
		/// </summary>
		public double MsTo(string milestone)
		{
			for (var i = _milestones.Count - 1; i >= 0; i--) {
				if (_milestones[i].Name == milestone) {
					return VpeClock.ElapsedUsec(StartTicks, _milestones[i].Ticks) / 1000.0;
				}
			}
			return -1;
		}

		/// <summary>
		/// Wall time from the start to the last milestone, in ms.
		/// </summary>
		public double TotalMs => VpeClock.ElapsedUsec(StartTicks, _milestones[_milestones.Count - 1].Ticks) / 1000.0;

		/// <summary>
		/// The intervals as one line, e.g. "read 120 ms, import 1.9 s (cpu 1.4 s, 1 gc), plugins 300 ms".
		/// Intervals under the threshold are folded into the next one.
		/// </summary>
		public string Format(double minMs = 0)
		{
			var sb = new StringBuilder();
			var from = 0;
			for (var i = 1; i < _milestones.Count; i++) {
				var ms = VpeClock.ElapsedUsec(_milestones[from].Ticks, _milestones[i].Ticks) / 1000.0;
				if (ms < minMs && i < _milestones.Count - 1) {
					continue;
				}
				if (sb.Length > 0) {
					sb.Append(", ");
				}
				sb.Append(_milestones[i].Name).Append(' ').Append(Duration(ms));
				var cpu = _milestones[i].CpuMs >= 0 && _milestones[from].CpuMs >= 0 ? _milestones[i].CpuMs - _milestones[from].CpuMs : -1;
				var gc = _milestones[from].GcCount >= 0 ? _milestones[i].GcCount - _milestones[from].GcCount : 0;
				if (ms >= 100 && cpu >= 0) {
					sb.Append(" (cpu ").Append(Duration(cpu));
					if (gc > 0) {
						sb.Append(", ").Append(gc).Append(" gc");
					}
					sb.Append(')');
				}
				from = i;
			}
			return sb.ToString();
		}

		/// <summary>
		/// Adds the timeline to a diagnostics section, one entry per milestone plus the facts.
		/// </summary>
		public void Describe(DiagnosticsWriter w, string prefix = null)
		{
			prefix = string.IsNullOrEmpty(prefix) ? "" : prefix + ".";
			w.Add(prefix + "total-ms", System.Math.Round(TotalMs));
			for (var i = 1; i < _milestones.Count; i++) {
				var m = _milestones[i];
				var ms = VpeClock.ElapsedUsec(_milestones[i - 1].Ticks, m.Ticks) / 1000.0;
				var at = VpeClock.ElapsedUsec(StartTicks, m.Ticks) / 1000.0;
				var cpu = m.CpuMs >= 0 && _milestones[i - 1].CpuMs >= 0 ? m.CpuMs - _milestones[i - 1].CpuMs : -1;
				w.Add(prefix + m.Name, string.Format(CultureInfo.InvariantCulture, "+{0:0} ms (at {1:0} ms{2})", ms, at, cpu >= 0 ? string.Format(CultureInfo.InvariantCulture, ", cpu {0:0} ms", cpu) : ""));
			}
			foreach (var fact in _facts) {
				w.Add(prefix + fact.Key, fact.Value);
			}
		}

		private static string Duration(double ms)
		{
			return ms >= 1000
				? (ms / 1000).ToString("0.0", CultureInfo.InvariantCulture) + " s"
				: ms.ToString("0", CultureInfo.InvariantCulture) + " ms";
		}

		/// <summary>
		/// The process' CPU time over all threads in ms, or -1 if the platform won't tell.
		/// Allocates a little; not for per-frame use.
		/// </summary>
		public static double ProcessCpuMs()
		{
			try {
				using var process = Process.GetCurrentProcess();
				return process.TotalProcessorTime.TotalMilliseconds;
			} catch (Exception) {
				return -1;
			}
		}
	}
}
