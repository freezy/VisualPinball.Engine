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
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using NLog;
using UnityEngine;
using VisualPinball.Unity.Simulation;
using Logger = NLog.Logger;

namespace VisualPinball.Unity.Profiling
{
	/// <summary>
	/// <c>--vpe-test-perf=&lt;mode&gt;</c>: makes the problem a detector is for, once, 35 s into
	/// a table, so every detector, cause and reporting path can be checked end to end in a real
	/// build. Profile builds only, like <c>--vpe-test-crash</c>.
	/// </summary>
	///
	/// <remarks>
	/// Modes: stutter, hitch, gc, gcslice, lock, gpu, simstall, pacing, deschedule, firstuse,
	/// and all (one after the other, 10 s apart, then the player quits, so the end of a table is
	/// covered too).
	/// </remarks>
	public static class PerfSelfTest
	{
		private const string Argument = "-vpe-test-perf=";
		private const float StartAfterSeconds = 35f;
		private const float AllIntervalSeconds = 10f;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private static readonly string[] AllModes = { "stutter", "hitch", "gc", "gcslice", "lock", "gpu", "simstall", "pacing", "deschedule", "firstuse" };

		private static readonly Queue<string> Pending = new();
		private static double _nextAt = -1;
		private static string _running;
		private static double _runningUntil;
		private static int _frames;
		private static readonly List<byte[]> Garbage = new();
		private static RenderTexture _source, _target;
		private static double _quitAt = -1;
		private static double _endTableAt = -1;

		/// <summary>
		/// Set by the self test, consumed by the simulation thread: sleep this long once.
		/// </summary>
		internal static volatile int SimulationStallMs;

		/// <summary>
		/// The mode from the command line, or null.
		/// </summary>
		public static string Mode { get; private set; }

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
		private static void Install()
		{
			Mode = null;
			Pending.Clear();
			foreach (var raw in Environment.GetCommandLineArgs()) {
				var arg = raw.StartsWith("--", StringComparison.Ordinal) ? raw.Substring(1) : raw;
				if (arg.StartsWith(Argument, StringComparison.OrdinalIgnoreCase)) {
					Mode = arg.Substring(Argument.Length).Trim().ToLowerInvariant();
				}
			}
			if (Mode == null) {
				return;
			}
			if (Mode == "all") {
				foreach (var mode in AllModes) {
					Pending.Enqueue(mode);
				}
			} else if (Array.IndexOf(AllModes, Mode) >= 0) {
				Pending.Enqueue(Mode);
			} else {
				Logger.Warn("Unknown --vpe-test-perf mode {Mode:l}; known are {Modes:l} and all", Mode, string.Join(", ", AllModes));
				return;
			}
			Logger.Info("Perf self test {Mode:l} armed: runs {Seconds} s after a table became playable", Mode, (int)StartAfterSeconds);
			LoadAttempt.Finished -= OnLoadFinished;
			LoadAttempt.Finished += OnLoadFinished;
		}

		private static void OnLoadFinished(LoadAttempt attempt)
		{
			if (attempt.PlayableMs >= 0 && Pending.Count > 0 && _nextAt < 0) {
				_nextAt = Time.realtimeSinceStartupAsDouble + StartAfterSeconds - (attempt.SettledMs > 0 ? (attempt.SettledMs - attempt.PlayableMs) / 1000 : 0);
			}
		}

		/// <summary>
		/// Called every frame by the <see cref="FrameRecorder"/>.
		/// </summary>
		internal static void Update()
		{
			if (_endTableAt > 0 && Time.realtimeSinceStartupAsDouble >= _endTableAt) {
				_endTableAt = -1;
				// like exiting the table: the player goes, and with it the simulation and the play
				var player = UnityEngine.Object.FindAnyObjectByType<Player>();
				if (player != null) {
					Logger.Info("Perf self test done, ending the table");
					UnityEngine.Object.Destroy(player.gameObject);
				}
				return;
			}
			if (_quitAt > 0 && Time.realtimeSinceStartupAsDouble >= _quitAt) {
				_quitAt = -1;
				Logger.Info("Perf self test done, quitting");
				Application.Quit();
				return;
			}
			if (_running != null) {
				Continue();
				return;
			}
			if (_nextAt < 0 || Pending.Count == 0 || Time.realtimeSinceStartupAsDouble < _nextAt) {
				return;
			}
			_running = Pending.Dequeue();
			_frames = 0;
			_runningUntil = Time.realtimeSinceStartupAsDouble;
			_nextAt = Pending.Count > 0 ? Time.realtimeSinceStartupAsDouble + AllIntervalSeconds : -1;
			if (Pending.Count == 0 && Mode == "all") {
				_endTableAt = Time.realtimeSinceStartupAsDouble + 2 * AllIntervalSeconds;
				_quitAt = _endTableAt + AllIntervalSeconds;
			}
			Logger.Info("Perf self test: {Mode:l}", _running);
			Start(_running);
		}

		private static void Start(string mode)
		{
			switch (mode) {
				case "stutter":
					Spin(40);
					_running = null;
					break;
				case "hitch":
					Spin(300);
					_running = null;
					break;
				case "gc":
					for (var i = 0; i < 200; i++) {
						Garbage.Add(new byte[256 * 1024]);
					}
					Garbage.Clear();
					GC.Collect();
					_running = null;
					break;
				case "gcslice":
					// steady garbage for three seconds, for the incremental collector's slices
					_runningUntil = Time.realtimeSinceStartupAsDouble + 3;
					break;
				case "lock":
					var gate = new object();
					var held = new ManualResetEventSlim();
					new Thread(() => {
						lock (gate) {
							held.Set();
							Thread.Sleep(30);
						}
					}) { IsBackground = true }.Start();
					held.Wait();
					using (SimulationTrace.Lock(gate, SimulationTrace.LockSite.Unknown)) {
					}
					_running = null;
					break;
				case "gpu":
					_source ??= new RenderTexture(4096, 4096, 0, RenderTextureFormat.ARGBFloat);
					_target ??= new RenderTexture(4096, 4096, 0, RenderTextureFormat.ARGBFloat);
					for (var i = 0; i < 300; i++) {
						Graphics.Blit(i % 2 == 0 ? _source : _target, i % 2 == 0 ? _target : _source);
					}
					_running = null;
					break;
				case "simstall":
					SimulationStallMs = 60;
					_running = null;
					break;
				case "pacing":
					_runningUntil = Time.realtimeSinceStartupAsDouble + 3;
					break;
				case "deschedule":
					// every core busy for a second, at normal priority: the simulation and main threads
					// have to share
					var until = Stopwatch.GetTimestamp() + Stopwatch.Frequency;
					for (var i = 0; i < SystemInfo.processorCount; i++) {
						new Thread(() => {
							while (Stopwatch.GetTimestamp() < until) {
							}
						}) { IsBackground = true }.Start();
					}
					_running = null;
					break;
				case "firstuse":
					VpeProfiler.Mark(ProfileEvent.FirstUseMultiball);
					Spin(30);
					_running = null;
					break;
			}
		}

		private static void Continue()
		{
			_frames++;
			switch (_running) {
				case "gcslice":
					for (var i = 0; i < 64; i++) {
						Garbage.Add(new byte[16 * 1024]);
					}
					if (Garbage.Count > 4096) {
						Garbage.Clear();
					}
					break;
				case "pacing":
					// long, short, long, short
					if (_frames % 2 == 0) {
						Spin(12);
					}
					break;
			}
			if (Time.realtimeSinceStartupAsDouble >= _runningUntil) {
				Garbage.Clear();
				_running = null;
			}
		}

		/// <summary>
		/// Busy for the given time, inside the Perf.Self section, so the incident's cause is
		/// that section.
		/// </summary>
		private static void Spin(double ms)
		{
			VpeProfiler.Begin(ProfileSection.PerfSelf);
			var until = Stopwatch.GetTimestamp() + (long)(ms * Stopwatch.Frequency / 1000);
			while (Stopwatch.GetTimestamp() < until) {
			}
			VpeProfiler.End(ProfileSection.PerfSelf);
		}
	}
}
#endif
