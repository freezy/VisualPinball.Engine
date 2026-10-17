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
using System.Linq;
using NLog;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;
using VisualPinball.Plugins;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Something that can tell how far the simulation got compared to real time.
	/// </summary>
	public interface ISimulationClockSource
	{
		/// <summary>
		/// Simulated time, real time since the simulation started, and real time skipped
		/// because the simulation fell behind, all in microseconds. Main thread only.
		/// </summary>
		bool TryGetClock(out long simulationUsec, out long realUsec, out long droppedUsec);
	}

	/// <summary>
	/// Watches the frame rate, what limits it, and memory, and logs when that changes.
	/// </summary>
	///
	/// <remarks>
	/// <para>
	/// Runs as a PlayerLoop system at the end of every frame. Per frame it only adds a few
	/// numbers and captures the frame timings, without allocating. Once per second it closes a
	/// window: medians of CPU and GPU time, what bounds the frame (see
	/// <see cref="FrameBoundClassifier"/>), and how the simulation kept up. Memory is read every
	/// two seconds.
	/// </para>
	/// <para>
	/// While a table plays, it logs a line when the frame rate moved by 20 % or more, or when
	/// what bounds it changed, at most every 10 seconds, and every 5 minutes anyway. Pauses
	/// (time scale 0) are left out and start a new baseline. When the table ends, a summary.
	/// </para>
	/// <para>
	/// It's also the only caller of <see cref="FrameTimingManager.CaptureFrameTimings"/>; overlays
	/// read <see cref="TryGetLatestFrameTiming"/> instead of capturing a second time.
	/// </para>
	/// <para>
	/// The Performance section of the diagnostics is refreshed every few seconds, independently
	/// of the log lines, and holds both the latest window and the totals of the session so far.
	/// After the table ends it keeps the session's summary for later reports.
	/// </para>
	/// </remarks>
	public static class PerformanceMonitor
	{
		private const float WindowSeconds = 1f;
		private const float MemoryIntervalSeconds = 2f;
		private const float BaselineDelaySeconds = 5f;
		private const float MinLogIntervalSeconds = 10f;
		private const float HeartbeatSeconds = 300f;
		private const float ChangeThreshold = 0.2f;
		private const float HitchMs = 250f;
		private const int HysteresisWindows = 3;
		private const int MaxTimedFrames = 1024;
		private const float SectionIntervalSeconds = 10f;
		private const float HistogramBucketMs = 0.5f;
		private const int HistogramBuckets = 1000; // up to 500 ms, the last one holds everything above

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private static readonly LogThrottle HitchThrottle = new LogThrottle(0, TimeSpan.FromMinutes(1));
		private static readonly LogThrottle VramThrottle = new LogThrottle(1, TimeSpan.FromMinutes(5));

		private static readonly FrameTiming[] Timing = new FrameTiming[1];
		private static readonly float[] CpuMain = new float[MaxTimedFrames];
		private static readonly float[] CpuRender = new float[MaxTimedFrames];
		private static readonly float[] Gpu = new float[MaxTimedFrames];
		private static readonly float[] PresentWait = new float[MaxTimedFrames];

		private static FrameTiming _latest;
		private static bool _haveLatest;
		private static ulong _lastTimestamp;

		// window
		private static double _windowStart;
		private static double _lastFrameAt;
		private static int _frames;
		private static double _frameMsSum;
		private static float _worstMs;
		private static int _timed;
		private static long _windowSimUsec, _windowRealUsec, _windowDroppedUsec;
		private static bool _windowHasClock;

		// state
		private static bool _playing;
		private static bool _wasPaused;
		private static double _baselineAt;
		private static bool _baselineLogged;
		private static float _emaFps;
		private static float _lastLoggedFps;
		private static FrameBound _lastLoggedBound;
		private static double _lastLogAt;
		private static double _nextSectionAt;
		private static bool _gpuTimingExplained;
		private static bool _timingExplained;
		private static FrameBound _bound;
		private static FrameBound _candidate;
		private static int _candidateWindows;
		private static int _hitches;
		private static float _worstHitchMs;

		// memory
		private static MemoryProbe _memory;
		private static ProfilerRecorder _totalUsed;
		private static ProfilerRecorder _gcUsed;
		private static double _nextMemoryAt;

		// session summary, counted per frame so a window cut short by a pause or the end counts too
		private static double _playSeconds;
		private static double _measuredSeconds; // after the baseline, like the other statistics
		private static long _measuredFrames;
		private static readonly double[] BoundSeconds = new double[5];
		private static readonly int[] FrameHistogram = new int[HistogramBuckets];
		private static long _histogramFrames;
		private static int _totalHitches;
		private static float _worstFrameMs;
		private static float _minWindowFps;
		private static float _minSimRatio;

		/// <summary>
		/// Where the simulation's clock comes from, set by the simulation while it runs.
		/// </summary>
		public static ISimulationClockSource SimulationClock { get; set; }

		/// <summary>
		/// The last window, for reports. Main thread only.
		/// </summary>
		public static PerformanceSample Last;

		#region Lifecycle

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
		private static void Install()
		{
			var loop = PlayerLoop.GetCurrentPlayerLoop();
			if (Contains(loop, typeof(PerformanceMonitor))) {
				return;
			}
			for (var i = 0; i < loop.subSystemList.Length; i++) {
				if (loop.subSystemList[i].type != typeof(PostLateUpdate)) {
					continue;
				}
				var systems = loop.subSystemList[i].subSystemList.ToList();
				systems.Add(new PlayerLoopSystem { type = typeof(PerformanceMonitor), updateDelegate = OnFrame });
				loop.subSystemList[i].subSystemList = systems.ToArray();
				PlayerLoop.SetPlayerLoop(loop);
				break;
			}
#if UNITY_EDITOR
			// Recompiling during play reloads the domain without destroying the player: release
			// the native handles and take the system out of the loop before that.
			UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= Uninstall;
			UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Uninstall;
#endif
		}

		private static void Uninstall()
		{
			ReleaseNative();
			_playing = false;
			var loop = PlayerLoop.GetCurrentPlayerLoop();
			if (Remove(ref loop, typeof(PerformanceMonitor))) {
				PlayerLoop.SetPlayerLoop(loop);
			}
		}

		/// <summary>
		/// A table started playing: start measuring.
		/// </summary>
		public static void BeginPlay()
		{
			_playing = true;
			_wasPaused = false;
			_baselineLogged = false;
			_baselineAt = Time.realtimeSinceStartupAsDouble + BaselineDelaySeconds;
			_lastFrameAt = Time.realtimeSinceStartupAsDouble;
			_lastLogAt = 0;
			_emaFps = 0;
			_lastLoggedFps = 0;
			_lastLoggedBound = FrameBound.Unknown;
			_nextSectionAt = 0;
			_bound = _candidate = FrameBound.Unknown;
			_candidateWindows = 0;
			_hitches = 0;
			_worstHitchMs = 0;
			_playSeconds = 0;
			_measuredSeconds = 0;
			_measuredFrames = 0;
			Array.Clear(BoundSeconds, 0, BoundSeconds.Length);
			Array.Clear(FrameHistogram, 0, FrameHistogram.Length);
			_histogramFrames = 0;
			_totalHitches = 0;
			_worstFrameMs = 0;
			_minWindowFps = float.MaxValue;
			_minSimRatio = float.MaxValue;
			Last = default;
			Last.RamBytes = Last.PrivateBytes = Last.VramUsage = Last.VramBudget = -1;
			Last.PeakRamBytes = Last.PeakVramUsage = -1;

			_memory ??= new MemoryProbe();
			if (!_totalUsed.Valid) {
				_totalUsed = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
			}
			if (!_gcUsed.Valid) {
				_gcUsed = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Used Memory");
			}
			_nextMemoryAt = 0;
			StartWindow();
		}

		/// <summary>
		/// The table stopped: log the summary and stop measuring.
		/// </summary>
		public static void EndPlay()
		{
			if (!_playing) {
				return;
			}
			_playing = false;
			LogSummary();
#if VPE_PROFILE
			// the last frames still waiting for their timing count, nothing after does
			Profiling.FrameRecorder.Drain();
			Profiling.IncidentDetector.EndPlay();
			// a capture the last frames asked for (e.g. on quit, where nothing flushes later)
			Profiling.TraceCapture.Flush();
			Profiling.ProfileMetrics.EndPlay(AverageFps(), _minSimRatio == float.MaxValue ? -1 : _minSimRatio, Last.PeakVramUsage);
			Logger.Info("Perf incidents: {Summary:l}", Profiling.ProfileSession.Summary());
#endif
			ReleaseNative();
		}

		private static void ReleaseNative()
		{
			if (_totalUsed.Valid) {
				_totalUsed.Dispose();
			}
			if (_gcUsed.Valid) {
				_gcUsed.Dispose();
			}
			_memory?.Dispose();
			_memory = null;
		}

		/// <summary>
		/// The frame timing captured this frame, if the platform provides one.
		/// </summary>
		public static bool TryGetLatestFrameTiming(out FrameTiming timing)
		{
			timing = _latest;
			return _haveLatest;
		}

		#endregion

		#region Per frame

		private static void OnFrame()
		{
			if (!Application.isPlaying) {
				return;
			}
			FrameTimingManager.CaptureFrameTimings();
			_haveLatest = FrameTimingManager.GetLatestTimings(1, Timing) > 0;
			if (_haveLatest) {
				_latest = Timing[0];
			}
			StartupTimeline.OnFrameEnd();
			LoadAttempt.OnFrameEnd();
#if VPE_PROFILE
			Profiling.FrameRecorder.OnFrameEnd();
#endif
			if (!_playing) {
				return;
			}

			var now = Time.realtimeSinceStartupAsDouble;
			if (Time.timeScale == 0f) {
				// paused: leave it out, and measure afresh afterwards
				if (!_wasPaused) {
					_wasPaused = true;
					PublishSection(now); // reports are often sent from the pause menu
				}
				return;
			}
			if (_wasPaused) {
				_wasPaused = false;
				_baselineLogged = false;
				_baselineAt = now + BaselineDelaySeconds;
				_lastFrameAt = now;
				StartWindow();
				PublishSection(now); // not paused anymore
				return;
			}

			// measured from the last frame that counted, so loading before BeginPlay or a
			// pause doesn't end up in the first frame
			var frameSeconds = now - _lastFrameAt;
			_lastFrameAt = now;
			var frameMs = (float)(frameSeconds * 1000);
			_frames++;
			_frameMsSum += frameMs;
			if (frameMs > _worstMs) {
				_worstMs = frameMs;
			}
			_playSeconds += frameSeconds;
			// the first frames after loading or a pause are slow by nature
			if (now >= _baselineAt) {
				_measuredFrames++;
				_measuredSeconds += frameSeconds;
				if (frameMs > _worstFrameMs) {
					_worstFrameMs = frameMs;
				}
				var bucket = (int)(frameMs / HistogramBucketMs);
				FrameHistogram[bucket < HistogramBuckets ? bucket : HistogramBuckets - 1]++;
				_histogramFrames++;
				if (frameMs > HitchMs) {
					_hitches++;
					_totalHitches++;
					if (frameMs > _worstHitchMs) {
						_worstHitchMs = frameMs;
					}
				}
			}

			// timings arrive a few frames late; only take each frame once
			if (_haveLatest && _latest.frameStartTimestamp != _lastTimestamp && _timed < MaxTimedFrames) {
				_lastTimestamp = _latest.frameStartTimestamp;
				var wait = (float)System.Math.Max(0, _latest.cpuMainThreadPresentWaitTime);
				CpuMain[_timed] = (float)System.Math.Max(0, _latest.cpuMainThreadFrameTime - _latest.cpuMainThreadPresentWaitTime);
				CpuRender[_timed] = (float)System.Math.Max(0, _latest.cpuRenderThreadFrameTime);
				Gpu[_timed] = (float)System.Math.Max(0, _latest.gpuFrameTime);
				PresentWait[_timed] = wait;
				_timed++;
			}

			if (now >= _nextMemoryAt) {
				_nextMemoryAt = now + MemoryIntervalSeconds;
				SampleMemory();
			}
			if (now - _windowStart >= WindowSeconds) {
				CloseWindow(now);
			}
			if (now >= _nextSectionAt) {
				PublishSection(now);
			}
		}

		private static void PublishSection(double now)
		{
			_nextSectionAt = now + SectionIntervalSeconds;
			DiagnosticsContext.Set(DiagnosticsContext.Performance, Describe(), log: false);
		}

		private static void StartWindow()
		{
			_windowStart = Time.realtimeSinceStartupAsDouble;
			_frames = 0;
			_frameMsSum = 0;
			_worstMs = 0;
			_timed = 0;
			_windowHasClock = SimulationClock != null && SimulationClock.TryGetClock(out _windowSimUsec, out _windowRealUsec, out _windowDroppedUsec);
		}

		#endregion

		#region Window

		private static void CloseWindow(double now)
		{
			var seconds = (float)(now - _windowStart);
			if (_frames == 0 || seconds <= 0f) {
				StartWindow();
				return;
			}

			var timings = new FrameWindowTimings {
				FrameMs = (float)(_frameMsSum / _frames),
				CpuMainMs = Median(CpuMain, _timed),
				CpuRenderMs = Median(CpuRender, _timed),
				GpuMs = Median(Gpu, _timed),
				PresentWaitMs = Median(PresentWait, _timed),
				Samples = _timed,
			};
			var fps = _frames / seconds;
			var bound = FrameBoundClassifier.Classify(timings, CapIntervalMs());

			// the simulation's speed over the window
			var simRatio = -1f;
			var simDroppedMs = 0L;
			if (_windowHasClock && SimulationClock != null && SimulationClock.TryGetClock(out var sim, out var real, out var dropped) && real > _windowRealUsec) {
				simRatio = (float)(sim - _windowSimUsec) / (real - _windowRealUsec);
				simDroppedMs = (dropped - _windowDroppedUsec) / 1000;
			}

			ExplainMissingTimings(timings);

			// hysteresis: a new class has to win a few windows in a row
			if (bound == _candidate) {
				_candidateWindows++;
			} else {
				_candidate = bound;
				_candidateWindows = 1;
			}
			if (_candidateWindows >= HysteresisWindows) {
				_bound = _candidate;
			}

			_emaFps = _emaFps <= 0f ? fps : _emaFps + (fps - _emaFps) * 0.28f;

			Last.Fps = fps;
			Last.FrameMs = timings.FrameMs;
			Last.WorstFrameMs = _worstMs;
			Last.CpuMainMs = timings.CpuMainMs;
			Last.CpuRenderMs = timings.CpuRenderMs;
			Last.GpuMs = timings.GpuMs;
			Last.PresentWaitMs = timings.PresentWaitMs;
			Last.Bound = _bound;
			Last.SimRatio = simRatio;
			Last.SimLagging = simRatio >= 0f && simRatio < 0.98f || simDroppedMs > 0;

			// session summary (frames, time and the worst frame are counted per frame)
			BoundSeconds[(int)_bound] += seconds;
			if (now >= _baselineAt) {
				if (fps < _minWindowFps) {
					_minWindowFps = fps;
				}
				if (simRatio >= 0f && simRatio < _minSimRatio) {
					_minSimRatio = simRatio;
				}
			}

			MaybeLog(now);
			MaybeWarnHitches();
			StartWindow();
		}

		private static void MaybeLog(double now)
		{
			if (now < _baselineAt) {
				return;
			}
			// compared to what was last logged, so a change during the cooldown isn't lost
			var first = !_baselineLogged;
			var changed = _lastLoggedFps > 0f && System.Math.Abs(_emaFps - _lastLoggedFps) / _lastLoggedFps >= ChangeThreshold;
			var boundChanged = _bound != _lastLoggedBound;
			var due = now - _lastLogAt >= MinLogIntervalSeconds;
			var heartbeat = now - _lastLogAt >= HeartbeatSeconds;
			if (!first && !heartbeat && !(due && (changed || boundChanged))) {
				return;
			}
			_baselineLogged = true;
			_lastLoggedFps = _emaFps;
			_lastLoggedBound = _bound;
			_lastLogAt = now;

			Logger.Info("Perf: {Fps:0} fps ({FrameMs:0.0} ms, worst {WorstMs:0.0} ms), {Bound:l} (main {CpuMain:0.0} / render {CpuRender:0.0} / gpu {Gpu:0.0} ms), sim {Sim:l}, RAM {Ram:l}, VRAM {Vram:l}",
				_emaFps, Last.FrameMs, Last.WorstFrameMs, FrameBoundClassifier.Name(_bound) + (Last.SimLagging ? ", sim lagging" : ""),
				Last.CpuMainMs, Last.CpuRenderMs, Last.GpuMs,
				Last.SimRatio >= 0f ? Last.SimRatio.ToString("0.00") + "x" : "n/a",
				Bytes(Last.RamBytes) + (Last.PeakRamBytes > 0 ? " (peak " + Bytes(Last.PeakRamBytes) + ")" : ""),
				Last.VramUsage >= 0 ? Bytes(Last.VramUsage) + " / " + Bytes(Last.VramBudget) + " budget" : "n/a of " + SystemInfo.graphicsMemorySize + " MB");
			PublishSection(now);
		}

		/// <summary>
		/// Says once per session why what limits the frame rate is unknown, when it's because
		/// the platform doesn't time frames or the GPU.
		/// </summary>
		private static void ExplainMissingTimings(in FrameWindowTimings timings)
		{
			if (_frames < FrameBoundClassifier.MinSamples) {
				return;
			}
			if (timings.Samples == 0) {
				if (!_timingExplained) {
					_timingExplained = true;
					Logger.Info("No frame timings on {Api:l} (frame timing stats off, or not supported), what limits the frame rate is reported as unknown.",
						SystemInfo.graphicsDeviceType.ToString());
				}
			} else if (timings.Samples >= FrameBoundClassifier.MinSamples && timings.GpuMs <= 0f && !_gpuTimingExplained) {
				_gpuTimingExplained = true;
				Logger.Info("The GPU time isn't measured on {Api:l} with this driver, what limits the frame rate is reported as unknown.",
					SystemInfo.graphicsDeviceType.ToString());
			}
		}

		private static void MaybeWarnHitches()
		{
			if (_hitches == 0 || !HitchThrottle.ShouldLog()) {
				return;
			}
			Logger.Warn("{Count} frames took longer than {HitchMs} ms, the longest {WorstMs:0} ms.", _hitches, (int)HitchMs, _worstHitchMs);
			_hitches = 0;
			_worstHitchMs = 0;
		}

		private static void LogSummary()
		{
			// whatever the session got to, it's what later reports get
			DiagnosticsContext.Set(DiagnosticsContext.Performance, Describe(), log: false);
			if (_playSeconds < 1) {
				return;
			}
			Logger.Info("Perf summary: played {Minutes:0.0} min at {Fps:0} fps on average, lowest {MinFps:0} fps, worst 1% {Worst1Ms:l} ms, worst frame {WorstMs:0} ms, {Hitches} hitches; {Shares:l}; sim lowest {MinSim:l}; peak RAM {Ram:l}, peak VRAM {Vram:l}",
				_playSeconds / 60, AverageFps(), _minWindowFps == float.MaxValue ? 0 : _minWindowFps, Worst1PercentMs(), _worstFrameMs, _totalHitches, BoundShares(),
				_minSimRatio == float.MaxValue ? "n/a" : _minSimRatio.ToString("0.00") + "x",
				Bytes(Last.PeakRamBytes), Last.PeakVramUsage >= 0 ? Bytes(Last.PeakVramUsage) : "n/a");
		}

		private static double AverageFps() => _measuredSeconds > 0 ? _measuredFrames / _measuredSeconds : 0;

		/// <summary>
		/// The share of play time per bound class, largest first.
		/// </summary>
		private static string BoundShares()
		{
			var total = BoundSeconds.Sum();
			if (total <= 0) {
				return "n/a";
			}
			return string.Join(", ", Enumerable.Range(0, BoundSeconds.Length)
				.Where(i => BoundSeconds[i] > 0)
				.OrderByDescending(i => BoundSeconds[i])
				.Select(i => $"{FrameBoundClassifier.Name((FrameBound)i)} {BoundSeconds[i] / total:P0}"));
		}

		/// <summary>
		/// The frame time the slowest 1 % of frames since the baseline took at least, from the
		/// histogram.
		/// </summary>
		private static string Worst1PercentMs()
		{
			if (_histogramFrames == 0) {
				return "n/a";
			}
			var target = System.Math.Max(1, _histogramFrames / 100);
			long seen = 0;
			for (var i = HistogramBuckets - 1; i >= 0; i--) {
				seen += FrameHistogram[i];
				if (seen >= target) {
					return i == HistogramBuckets - 1
						? ">" + (i * HistogramBucketMs).ToString("0")
						: (i * HistogramBucketMs).ToString("0.0");
				}
			}
			return "n/a";
		}

		#endregion

		#region Memory

		private static void SampleMemory()
		{
			if (_memory == null) {
				return;
			}
			_memory.ReadProcess(out var workingSet, out var privateBytes);
			_memory.ReadVram(out var vramUsage, out var vramBudget);
			Last.RamBytes = workingSet;
			Last.PrivateBytes = privateBytes;
			Last.UnityUsedBytes = _totalUsed.Valid ? _totalUsed.LastValue : -1;
			Last.GcUsedBytes = _gcUsed.Valid ? _gcUsed.LastValue : -1;
			Last.VramUsage = vramUsage;
			Last.VramBudget = vramBudget;
			var utcNow = DateTime.UtcNow;
			Last.MemorySampledUtc = utcNow;
			if (workingSet > Last.PeakRamBytes) {
				Last.PeakRamBytes = workingSet;
				Last.PeakRamUtc = utcNow;
			}
			if (vramUsage > Last.PeakVramUsage) {
				Last.PeakVramUsage = vramUsage;
				Last.PeakVramUtc = utcNow;
			}
			if (vramBudget > 0 && vramUsage > vramBudget) {
				VpeProfiler.Mark(ProfileEvent.VramOverBudget);
			}
			if (vramBudget > 0 && vramUsage > 0.9 * vramBudget && VramThrottle.ShouldLog()) {
				Logger.Warn("The GPU memory is nearly full ({Usage:l} of a {Budget:l} budget). Textures get paged out, which causes stutter. Lower the texture size in the graphics settings.",
					Bytes(vramUsage), Bytes(vramBudget));
			}
		}

		#endregion

		#region Helpers

		/// <summary>
		/// The performance section of the diagnostics: the session so far, then the latest
		/// window and memory sample.
		/// </summary>
		public static DiagnosticsWriter Describe()
		{
			var w = new DiagnosticsWriter();
			w.Add("session.playing", _playing);
			w.Add("session.paused", _playing && _wasPaused);
			w.Add("session.played-minutes", _playSeconds / 60);
			w.Add("session.avg-fps", AverageFps());
			w.Add("session.min-fps", _minWindowFps == float.MaxValue ? 0 : _minWindowFps);
			w.Add("session.worst-1pct-ms", Worst1PercentMs());
			w.Add("session.worst-frame-ms", _worstFrameMs);
			w.Add("session.hitches", _totalHitches);
			w.Add("session.bound-shares", BoundShares());
			w.Add("session.min-sim-ratio", _minSimRatio == float.MaxValue ? "n/a" : _minSimRatio.ToString("0.000"));
			w.Add("fps", Last.Fps);
			w.Add("frame-ms", Last.FrameMs);
			w.Add("worst-frame-ms", Last.WorstFrameMs);
			w.Add("bound", FrameBoundClassifier.Name(Last.Bound));
			w.Add("cpu-main-ms", Last.CpuMainMs);
			w.Add("cpu-render-ms", Last.CpuRenderMs);
			w.Add("gpu-ms", Last.GpuMs);
			w.Add("present-wait-ms", Last.PresentWaitMs);
			w.Add("sim-ratio", Last.SimRatio >= 0f ? Last.SimRatio.ToString("0.000") : "n/a");
			w.Add("sim-lagging", Last.SimLagging);
			w.Add("ram", Bytes(Last.RamBytes));
			w.Add("ram.private", Bytes(Last.PrivateBytes));
			w.Add("ram.peak", Bytes(Last.PeakRamBytes));
			w.Add("ram.unity", Bytes(Last.UnityUsedBytes));
			w.Add("ram.gc", Bytes(Last.GcUsedBytes));
			w.Add("vram", Last.VramUsage >= 0 ? Bytes(Last.VramUsage) : "unavailable");
			w.Add("vram.budget", Last.VramBudget >= 0 ? Bytes(Last.VramBudget) : "unavailable");
			w.Add("vram.peak", Last.PeakVramUsage >= 0 ? Bytes(Last.PeakVramUsage) : "unavailable");
			w.Add("vram.capacity-mb", SystemInfo.graphicsMemorySize);
			w.Add("memory.sampled-at", Utc(Last.MemorySampledUtc));
			w.Add("ram.peak-at", Utc(Last.PeakRamUtc));
			w.Add("vram.peak-at", Utc(Last.PeakVramUtc));
			w.Add("vsync", QualitySettings.vSyncCount);
			w.Add("target-frame-rate", Application.targetFrameRate);
			return w;
		}

		private static float CapIntervalMs()
		{
			if (QualitySettings.vSyncCount > 0) {
				var hz = Screen.currentResolution.refreshRateRatio.value;
				return hz > 0 ? (float)(1000.0 / hz * QualitySettings.vSyncCount) : 0f;
			}
			return Application.targetFrameRate > 0 ? 1000f / Application.targetFrameRate : 0f;
		}

		private static float Median(float[] values, int count)
		{
			if (count == 0) {
				return 0f;
			}
			Array.Sort(values, 0, count);
			return (count & 1) == 1 ? values[count / 2] : (values[count / 2 - 1] + values[count / 2]) * 0.5f;
		}

		private static string Bytes(long bytes)
		{
			if (bytes < 0) {
				return "n/a";
			}
			return bytes >= 1L << 30 ? $"{bytes / (double)(1L << 30):0.0} GB" : $"{bytes / (double)(1L << 20):0} MB";
		}

		private static string Utc(DateTime time) => time == default ? "n/a" : time.ToString("yyyy-MM-dd HH:mm:ss") + "Z";

		private static bool Remove(ref PlayerLoopSystem system, Type type)
		{
			if (system.subSystemList == null) {
				return false;
			}
			var removed = false;
			var list = system.subSystemList.ToList();
			if (list.RemoveAll(s => s.type == type) > 0) {
				removed = true;
			}
			for (var i = 0; i < list.Count; i++) {
				var child = list[i];
				if (Remove(ref child, type)) {
					list[i] = child;
					removed = true;
				}
			}
			if (removed) {
				system.subSystemList = list.ToArray();
			}
			return removed;
		}

		private static bool Contains(PlayerLoopSystem system, Type type)
		{
			if (system.type == type) {
				return true;
			}
			return system.subSystemList != null && system.subSystemList.Any(s => Contains(s, type));
		}

		#endregion
	}

	/// <summary>
	/// The last performance window plus memory, for reports.
	/// </summary>
	public struct PerformanceSample
	{
		public float Fps;
		public float FrameMs;
		public float WorstFrameMs;
		public float CpuMainMs;
		public float CpuRenderMs;
		public float GpuMs;
		public float PresentWaitMs;
		public FrameBound Bound;
		public float SimRatio;
		public bool SimLagging;
		public long RamBytes;
		public long PrivateBytes;
		public long PeakRamBytes;
		public long UnityUsedBytes;
		public long GcUsedBytes;
		public long VramUsage;
		public long VramBudget;
		public long PeakVramUsage;
		public DateTime MemorySampledUtc;
		public DateTime PeakRamUtc;
		public DateTime PeakVramUtc;
	}
}
