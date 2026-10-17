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

#if VPE_PROFILE
using System;
using System.Diagnostics;
using System.Globalization;
using NLog;
using UnityEngine;
using Logger = NLog.Logger;

namespace VisualPinball.Unity.Profiling
{
	/// <summary>
	/// What the profile build does with the incidents it finds: a log line each (rate-limited),
	/// counts per type and cause for the session summary, and traces of the worst ones.
	/// </summary>
	///
	/// <remarks>
	/// A trace (§6.3) is taken for at most 3 incidents per table, 1 per type and cause, above a
	/// severity threshold, not in the first 30 s after the table became playable, plus every
	/// time the user presses F9. Main thread.
	/// </remarks>
	public static class ProfileSession
	{
		public const int MaxEvidencePerTable = 3;
		private const float EvidenceWarmupSeconds = 30f;
		private const float MinLogIntervalSeconds = 2f;
		private const int MaxLogLines = 300;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// An incident worth telling someone about passed the rate limits: for the Sentry logs.
		/// </summary>
		public static event Action<Incident> Reported;

		/// <summary>
		/// An incident was picked for a trace.
		/// </summary>
		public static event Action<Incident> EvidenceRequested;

		private static readonly int[,] Counts = new int[8, 16];
		private static readonly bool[,] Captured = new bool[8, 16];
		private static int _evidence;
		private static int _logLines;
		private static double _lastLogAt = -100;
		private static int _suppressed;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
		private static void Install()
		{
			if (string.IsNullOrEmpty(TraceCapture.Directory)) {
				TraceCapture.Directory = System.IO.Path.Combine(VpeLog.LogDirectory ?? System.IO.Path.Combine(Application.persistentDataPath, "logs"), "perf");
			}
			IncidentDetector.Detected -= OnIncident;
			IncidentDetector.Detected += OnIncident;
			LoadAttempt.Began -= OnLoadBegan;
			LoadAttempt.Began += OnLoadBegan;
			LoadAttempt.Finished -= OnLoadFinished;
			LoadAttempt.Finished += OnLoadFinished;
			ResetTable();
			// realtimeSinceStartup starts over with a new play session in the editor
			_lastLogAt = -100;
			Logger.Info("Profile build: frame, section and simulation timing is recorded; incidents are logged, and the worst get a trace in {Folder:l}", TraceCapture.Directory ?? "the log folder");
		}

		private static void OnLoadBegan(LoadAttempt attempt)
		{
			// a new table: new budget, new counts
			ResetTable();
			TraceCapture.RefreshContext();
		}

		private static void OnLoadFinished(LoadAttempt attempt)
		{
			// the package's fingerprint is known by now
			TraceCapture.RefreshContext();
		}

		private static void ResetTable()
		{
			Array.Clear(Counts, 0, Counts.Length);
			Array.Clear(Captured, 0, Captured.Length);
			_evidence = 0;
			_logLines = 0;
			_suppressed = 0;
		}

		/// <summary>
		/// How many incidents of each kind this table had, e.g. "stutter/render-gpu 12, hitch/gc-correlated 1".
		/// </summary>
		public static string Summary()
		{
			var sb = new System.Text.StringBuilder();
			for (var t = 0; t < Counts.GetLength(0); t++) {
				for (var c = 0; c < Counts.GetLength(1); c++) {
					if (Counts[t, c] == 0) {
						continue;
					}
					if (sb.Length > 0) {
						sb.Append(", ");
					}
					sb.Append(Incident.Name((IncidentType)t)).Append('/').Append(Incident.Name((IncidentCause)c)).Append(' ').Append(Counts[t, c]);
				}
			}
			return sb.Length == 0 ? "none" : sb.ToString();
		}

		private static void OnIncident(Incident incident)
		{
			var type = (int)incident.Type;
			var cause = (int)incident.Cause;
			Counts[type, cause]++;

			var now = Time.realtimeSinceStartupAsDouble;
			var user = incident.Type == IncidentType.UserMarker;
			if (user || _logLines < MaxLogLines && now - _lastLogAt >= MinLogIntervalSeconds) {
				_lastLogAt = now;
				_logLines++;
				Logger.Info("Perf incident: {Type:l} ({Cause:l}{Secondary:l}), {Severity:0.0} ms, frame {FrameMs:0.0} ms vs median {MedianMs:0.0} ms{Details:l}{Suppressed:l}",
					Incident.Name(incident.Type), Incident.Name(incident.Cause), SecondaryText(incident), incident.SeverityMs, incident.FrameMs, incident.MedianMs,
					Details(incident), _suppressed > 0 ? $" ({_suppressed} more since the last line)" : "");
				_suppressed = 0;
				try {
					Reported?.Invoke(incident);
				} catch (Exception e) {
					Logger.Warn(e, "An incident report handler failed.");
				}
			} else {
				_suppressed++;
			}

			if (!ShouldCapture(ref incident, type, cause)) {
				return;
			}
			var (pre, post) = incident.Type switch {
				IncidentType.Hitch or IncidentType.SimStall => (5.0, 2.0),
				IncidentType.UserMarker => (10.0, 0.0),
				IncidentType.Degradation => (4.0, 1.0),
				_ => (3.0, 1.0),
			};
			if (TraceCapture.Request(incident, pre, post)) {
				if (!user) {
					_evidence++;
					Captured[type, cause] = true;
				}
				try {
					EvidenceRequested?.Invoke(incident);
				} catch (Exception e) {
					Logger.Warn(e, "An evidence handler failed.");
				}
			}
		}

		private static bool ShouldCapture(ref Incident incident, int type, int cause)
		{
			if (incident.Type == IncidentType.UserMarker) {
				return true;
			}
			if (_evidence >= MaxEvidencePerTable || Captured[type, cause] || incident.SecondsIntoPlay < EvidenceWarmupSeconds) {
				return false;
			}
			// worth a trace: a clearly visible stutter, a hitch, a real stall
			return incident.Type switch {
				IncidentType.Stutter => incident.SeverityMs >= 12f,
				IncidentType.Hitch => true,
				IncidentType.SimStall => incident.SeverityMs >= 10f,
				IncidentType.Pacing => true,
				IncidentType.Degradation => true,
				_ => false,
			};
		}

		private static string SecondaryText(in Incident incident)
		{
			if (incident.SecondaryCauses == 0) {
				return "";
			}
			var text = "";
			for (var i = 1; i < 32; i++) {
				if ((incident.SecondaryCauses & (1u << i)) != 0) {
					text += ", " + Incident.Name((IncidentCause)i);
				}
			}
			return text;
		}

		private static string Details(in Incident incident)
		{
			var parts = "";
			if (incident.SystemLockUsec >= 1000) {
				parts += $", window manager locked {incident.SystemLockUsec / 1000f:0.0} ms";
			}
			if (incident.Cause == IncidentCause.Section) {
				parts += $", {SectionRecorder.Name(incident.Section)} {incident.SectionUsec / 1000f:0.0} ms";
			}
			if (incident.GpuMs >= 0 && incident.Type != IncidentType.SimStall) {
				parts += string.Format(CultureInfo.InvariantCulture, ", main {0:0.0} / render {1:0.0} / gpu {2:0.0} ms", incident.CpuMainMs, incident.CpuRenderMs, incident.GpuMs);
			}
			if (incident.UnexplainedMs >= 1f) {
				var phases = FrameRecorder.PhaseNames;
				parts += string.Format(CultureInfo.InvariantCulture, ", {0:0.0} ms unexplained, longest phase {1} {2:0.0} ms", incident.UnexplainedMs,
					incident.Phase < phases.Length ? phases[incident.Phase] : "?", incident.PhaseMs);
			}
			if (incident.GcDelta > 0) {
				parts += $", {incident.GcDelta} gc";
			}
			if (incident.MaxTickGapUsec > 2000) {
				parts += $", sim gap {incident.MaxTickGapUsec / 1000f:0.0} ms";
			}
			if (incident.LockWaitUsec > 1000) {
				parts += $", physics lock wait {incident.LockWaitUsec / 1000f:0.0} ms";
			}
			return parts;
		}
	}
}
#endif
