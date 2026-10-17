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
using System.Linq;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.LowLevel;
using VisualPinball.Unity.Simulation;

namespace VisualPinball.Unity.Profiling
{
	/// <summary>
	/// Whether a frame got its timing from Unity's <see cref="FrameTimingManager"/>.
	/// </summary>
	internal enum TimingState : byte
	{
		/// <summary>Not yet: timings arrive a few frames late.</summary>
		Pending,
		/// <summary>Matched to a timing result.</summary>
		Matched,
		/// <summary>No result arrived within <see cref="FrameRecorder.TimingTimeoutFrames"/> frames.</summary>
		None,
	}

	/// <summary>
	/// One frame on the main thread, as recorded in the profile build.
	/// </summary>
	internal unsafe struct FrameSample
	{
		public const int Phases = 9; // the top-level PlayerLoop systems, and the frame's end
		public const int MaxSections = 24;
		public const int MaxCounters = 16;

		/// <summary>Time.frameCount</summary>
		public long Frame;
		/// <summary>Stopwatch ticks at the start of each top-level PlayerLoop system; the last one is the end of the frame.</summary>
		public fixed long Phase[Phases];
		public float UnscaledDeltaMs;
		public float TimeScale;
		/// <summary>GC.CollectionCount(0) at the end of the frame.</summary>
		public int GcCount;
		/// <summary>"GC Used Memory" at the end of the frame, or -1.</summary>
		public long GcUsedBytes;

		public TimingState Timing;
		public float CpuFrameMs, CpuMainMs, CpuRenderMs, GpuMs, PresentWaitMs;
		/// <summary>Unity's frame timestamps on the stopwatch clock.</summary>
		public long UnityStartTicks, FirstSubmitTicks, PresentCalledTicks, FrameCompleteTicks;
		public uint SyncInterval;

		public fixed int SectionUsec[MaxSections];
		public fixed int Counter[MaxCounters];
		/// <summary>A bit per <see cref="ProfileEvent"/> that happened in the frame.</summary>
		public uint Events;

		/// <summary>When the rendered simulation snapshot was published (µs), or 0.</summary>
		public long SnapshotPublishUsec;
		public int SnapshotAgeUsec;
		public int Balls;
		public int EventsDrained;
		public byte SnapshotBackwards;
		public byte DrainSkipped;
		public byte SelfInduced;
		public byte Paused;

		public long StartTicks => Phase[0];
		public long EndTicks => Phase[Phases - 1];
		public bool HasEvent(ProfileEvent evt) => (Events & (1u << (int)evt)) != 0;
	}

	/// <summary>
	/// An event with the frame it happened in, for the trace.
	/// </summary>
	internal struct EventSample
	{
		public long Ticks;
		public long Frame;
		public ProfileEvent Event;
		public int Value;
	}

	/// <summary>
	/// Records every frame of the main thread into a ring of the last two minutes, matches
	/// Unity's frame timings to them, and hands each frame on once it's complete.
	/// </summary>
	///
	/// <remarks>
	/// <para>
	/// A stamp at the start of each top-level PlayerLoop system locates time spent inside
	/// Unity between VPE's code. The frame ends after <see cref="PerformanceMonitor"/>, which
	/// calls <see cref="OnFrameEnd"/>.
	/// </para>
	/// <para>
	/// Unity's timings carry no frame index and arrive about four frames late, sometimes two
	/// at once, sometimes not at all. They're read up to the history depth every frame, taken in
	/// order and matched to the frame whose start is closest, never going backwards. A frame is
	/// <i>finalized</i> once it and the next one have their timing or timed out, because a
	/// frame's GPU and present costs show up in the next one; finalized frames go to the
	/// <see cref="IncidentDetector"/> in order. Main thread only, allocation-free per frame.
	/// </para>
	/// </remarks>
	internal static unsafe class FrameRecorder
	{
		public const int Capacity = 120 * 240;
		public const int EventCapacity = 4096;
		public const int TimingTimeoutFrames = 8;
		private const int TimingReads = 4;
		private const double MatchToleranceMs = 50;

		private static FrameSample* _frames;
		private static long _count;          // frames closed so far
		private static long _finalized;      // frames handed on so far
		private static long _lastMatched = -1;
		private static long _timeoutCursor;
		private static long _finalizedThroughTicks;
		private static ulong _lastTimingStart;
		private static EventSample* _events;
		private static long _eventCount;
		private static FrameSample _current;
		private static bool _open;
		private static bool _installed;
		private static int _mainThreadId;

		private static readonly FrameTiming[] Timings = new FrameTiming[TimingReads];
		private static readonly int[] PendingCounters = new int[FrameSample.MaxCounters];
		private static readonly bool[] FirstUseSeen = new bool[32];
		private static ProfilerRecorder _gcUsed;
		private static double _timerToStopwatch = 1;
		private static bool _timerIsStopwatch = true;
		private static ulong _timerFrequency;
		private static long _timerOffset;
		private static long _monotonicOffset;

		/// <summary>
		/// Stopwatch ticks minus the platform's monotonic clock in stopwatch units (QPC on
		/// Windows), for merging other tools' timestamps (PresentMon). 0 if unknown.
		/// </summary>
		public static long MonotonicOffset => _monotonicOffset;
		private static long _lastPublishUsec;
		private static uint _pendingEvents;
		private static bool _selfInduced;
		private static int _selfInducedFrames;
		private static bool _writingAtStart;

		// environment
		private static double _nextPollAt;
		private static int _width, _height, _vsync, _targetFps, _quality, _displays;
		private static FullScreenMode _fullScreen;
		private static double _refresh;
		private static bool _paused;

		public static FrameSample* Frames => _frames;
		public static long Count => _count;
		public static long FinalizedCount => _finalized;

		/// <summary>The end of the newest frame handed on, in stopwatch ticks.</summary>
		public static long FinalizedThroughTicks => _finalizedThroughTicks;
		public static EventSample* Events => _events;
		public static long EventCount => _eventCount;

		/// <summary>Stopwatch ticks of Unity's CPU timer, how the frame timings were mapped.</summary>
		public static double TimerToStopwatch => _timerToStopwatch;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
		private static void Install()
		{
			_mainThreadId = Thread.CurrentThread.ManagedThreadId;
			SectionRecorder.Install();
			if (_frames == null) {
				_frames = (FrameSample*)UnsafeUtility.Malloc((long)Capacity * sizeof(FrameSample), UnsafeUtility.AlignOf<FrameSample>(), Allocator.Persistent);
				_events = (EventSample*)UnsafeUtility.Malloc((long)EventCapacity * sizeof(EventSample), UnsafeUtility.AlignOf<EventSample>(), Allocator.Persistent);
			}
			_count = _finalized = _eventCount = 0;
			_lastMatched = -1;
			_timeoutCursor = 0;
			_finalizedThroughTicks = 0;
			// realtimeSinceStartup starts over with a new play session in the editor
			_nextPollAt = 0;
			_paused = false;
			_lastTimingStart = 0;
			_open = false;
			_selfInduced = false;
			Array.Clear(FirstUseSeen, 0, FirstUseSeen.Length);

			// Unity's timer frequency is only known once it timed a frame
			_timerFrequency = 0;
			if (!_gcUsed.Valid) {
				_gcUsed = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Used Memory");
			}

			if (!_installed) {
				_installed = true;
				Application.focusChanged += focused => Mark(ProfileEvent.Focus, focused ? 1 : 0);
				// a trace still waiting for its post-window is written with what there is
				Application.quitting += () => TraceCapture.Flush();
#if UNITY_EDITOR
				UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Uninstall;
#endif
			}
			// every time: the editor may have reset the loop since (it skips systems already in)
			InstallPhaseStamps();
			IncidentDetector.Reset();
			ProfileMetrics.Reset();
		}

		/// <summary>
		/// Takes the stamps out of the PlayerLoop and frees the native memory, before a domain
		/// reload throws away the pointers (and the delegates the loop still calls).
		/// </summary>
		private static void Uninstall()
		{
			var loop = PlayerLoop.GetCurrentPlayerLoop();
			for (var i = 0; i < loop.subSystemList.Length; i++) {
				var systems = loop.subSystemList[i].subSystemList;
				if (systems != null && systems.Any(s => s.type == typeof(FrameRecorder))) {
					loop.subSystemList[i].subSystemList = systems.Where(s => s.type != typeof(FrameRecorder)).ToArray();
				}
			}
			PlayerLoop.SetPlayerLoop(loop);
			_open = false;
			if (_gcUsed.Valid) {
				_gcUsed.Dispose();
			}
			TraceCapture.Free();
			SectionRecorder.Free();
			if (_frames != null) {
				UnsafeUtility.Free(_frames, Allocator.Persistent);
				UnsafeUtility.Free(_events, Allocator.Persistent);
				_frames = null;
				_events = null;
			}
		}

		/// <summary>
		/// A system at the start of each top-level PlayerLoop system, stamping the phase.
		/// </summary>
		private static void InstallPhaseStamps()
		{
			var loop = PlayerLoop.GetCurrentPlayerLoop();
			var count = System.Math.Min(loop.subSystemList.Length, FrameSample.Phases - 1);
			for (var i = 0; i < count; i++) {
				var phase = i;
				var systems = loop.subSystemList[i].subSystemList?.ToList() ?? new System.Collections.Generic.List<PlayerLoopSystem>();
				if (systems.Any(s => s.type == typeof(FrameRecorder))) {
					continue;
				}
				systems.Insert(0, new PlayerLoopSystem {
					type = typeof(FrameRecorder),
					updateDelegate = phase == 0 ? (PlayerLoopSystem.UpdateFunction)OnFrameStart : () => StampPhase(phase),
				});
				loop.subSystemList[i].subSystemList = systems.ToArray();
			}
			PlayerLoop.SetPlayerLoop(loop);
			PhaseNames = loop.subSystemList.Take(count).Select(s => s.type?.Name ?? "?").Concat(new[] { "End" }).ToArray();
			_updatePhase = Array.IndexOf(PhaseNames, "Update");
			// the phase that holds Unity's WaitForLastPresentation system
			PresentWaitPhase = -1;
			for (var i = 0; i < count; i++) {
				if (loop.subSystemList[i].subSystemList?.Any(s => s.type != null && s.type.Name.StartsWith("WaitForLastPresentation", StringComparison.Ordinal)) == true) {
					PresentWaitPhase = i;
				}
			}
		}

		/// <summary>The top-level PlayerLoop systems, in the order of <see cref="FrameSample.Phase"/>.</summary>
		public static string[] PhaseNames { get; private set; } = Array.Empty<string>();

		private static void OnFrameStart()
		{
			if (!Application.isPlaying || _frames == null) {
				return;
			}
			_current = default;
			_current.Frame = Time.frameCount;
			_current.Phase[0] = Stopwatch.GetTimestamp();
			_current.Events = _pendingEvents;
			_pendingEvents = 0;
			// a trace written during the frame may stall it even if it's done by the frame's end
			_writingAtStart = TraceCapture.IsWriting;
			_open = true;
		}

		private static void StampPhase(int phase)
		{
			if (!_open) {
				return;
			}
			_current.Phase[phase] = Stopwatch.GetTimestamp();
			if (phase == _updatePhase) {
				// what the self test injects lands inside the frame, where a real problem would
				PerfSelfTest.Update();
			}
		}

		private static int _updatePhase = -1;

		/// <summary>
		/// The phase in which Unity waits for the previous frame's presentation (vsync, GPU):
		/// TimeUpdate in Unity 6. Time there isn't the main thread's work.
		/// </summary>
		public static int PresentWaitPhase { get; private set; } = -1;

		/// <summary>
		/// The next frames are the profile build's own doing (a trace copy), so they're no incident.
		/// </summary>
		internal static void MarkSelfInduced(int frames)
		{
			_selfInducedFrames = System.Math.Max(_selfInducedFrames, frames);
		}

		/// <summary>
		/// Something happened. First-use events count once per table.
		/// </summary>
		public static void Mark(ProfileEvent evt, int value)
		{
			if (_frames == null || Thread.CurrentThread.ManagedThreadId != _mainThreadId) {
				return;
			}
			var i = (int)evt;
			if (IsFirstUse(evt)) {
				if (FirstUseSeen[i]) {
					return;
				}
				FirstUseSeen[i] = true;
			}
			switch (evt) {
				case ProfileEvent.TableLoad:
					Array.Clear(FirstUseSeen, 0, FirstUseSeen.Length);
					IncidentDetector.OnTableLoad();
					break;
				case ProfileEvent.SelfInduced:
					_selfInduced = value != 0;
					break;
				case ProfileEvent.UserMarker:
					IncidentDetector.UserMarker();
					break;
				case ProfileEvent.TableUnload:
					// while the load is still current: the last frames count, nothing after does
					Drain();
					IncidentDetector.EndPlay();
					TraceCapture.Flush();
					break;
			}
			if (_open) {
				_current.Events |= 1u << i;
			} else {
				_pendingEvents |= 1u << i;
			}
			ref var e = ref _events[_eventCount % EventCapacity];
			e.Ticks = Stopwatch.GetTimestamp();
			e.Frame = Time.frameCount;
			e.Event = evt;
			e.Value = value;
			_eventCount++;
		}

		public static void AddCount(ProfileCounter counter, int amount)
		{
			var i = (int)counter;
			if (i < FrameSample.MaxCounters) {
				Interlocked.Add(ref PendingCounters[i], amount);
			}
		}

		private static bool IsFirstUse(ProfileEvent evt) => evt >= ProfileEvent.FirstUseLight && evt <= ProfileEvent.FirstUseDrain;

		/// <summary>
		/// Closes the frame. Called by <see cref="PerformanceMonitor"/> right after it captured
		/// the frame timings.
		/// </summary>
		internal static void OnFrameEnd()
		{
			if (!_open || _frames == null) {
				return;
			}
			_open = false;
			var end = Stopwatch.GetTimestamp();
			ref var f = ref _current;
			f.Phase[FrameSample.Phases - 1] = end;
			// a phase whose stamp didn't run this frame starts where the previous one did
			for (var i = 1; i < FrameSample.Phases - 1; i++) {
				if (f.Phase[i] == 0) {
					f.Phase[i] = f.Phase[i - 1];
				}
			}
			f.UnscaledDeltaMs = Time.unscaledDeltaTime * 1000f;
			f.TimeScale = Time.timeScale;
			f.Paused = (byte)(Time.timeScale == 0f ? 1 : 0);
			f.GcCount = GC.CollectionCount(0);
			f.GcUsedBytes = _gcUsed.Valid ? _gcUsed.LastValue : -1;
			// also while a trace is written: its allocations may trigger a collection
			f.SelfInduced = (byte)(_selfInduced || _selfInducedFrames > 0 || _writingAtStart || TraceCapture.IsWriting ? 1 : 0);
			if (_selfInducedFrames > 0) {
				_selfInducedFrames--;
			}
			for (var i = 0; i < FrameSample.MaxCounters; i++) {
				f.Counter[i] = Interlocked.Exchange(ref PendingCounters[i], 0);
			}

			// the physics engine times its own steps; take them when it ran this frame
			ref var sim = ref SimulationTrace.Frame;
			if (SimulationTrace.IsRecording && sim.Frame == Time.frameCount) {
				SectionRecorder.Add(ProfileSection.PhysicsEventDrain, UsecToTicks(sim.DrainUsec));
				SectionRecorder.Add(ProfileSection.PhysicsKinematicScan, UsecToTicks(sim.ScanUsec));
				SectionRecorder.Add(ProfileSection.PhysicsApplyMovements, UsecToTicks(sim.ApplyUsec));
				f.SnapshotPublishUsec = sim.SnapshotPublishUsec;
				f.SnapshotAgeUsec = sim.SnapshotPublishUsec > 0 ? (int)(VpeClock.TicksToUsec(f.StartTicks) - sim.SnapshotPublishUsec) : 0;
				f.Balls = sim.SnapshotBallCount;
				f.EventsDrained = sim.EventsDrained;
				f.DrainSkipped = (byte)sim.DrainSkipped;
				f.SnapshotBackwards = (byte)(sim.SnapshotPublishUsec > 0 && sim.SnapshotPublishUsec < _lastPublishUsec ? 1 : 0);
				if (sim.SnapshotPublishUsec > 0) {
					_lastPublishUsec = sim.SnapshotPublishUsec;
				}
			}
			fixed (int* sections = f.SectionUsec) {
				SectionRecorder.EndFrame(sections, FrameSample.MaxSections);
			}

			_frames[_count % Capacity] = f;
			_count++;

			ReadTimings();
			FinalizeFrames();
			PollEnvironment();
			TraceCapture.Update(end);
		}

		#region Timings

		private static void ReadTimings()
		{
			var n = (int)FrameTimingManager.GetLatestTimings(TimingReads, Timings);
			if (n > 0 && _timerFrequency == 0) {
				_timerFrequency = FrameTimingManager.GetCpuTimerFrequency();
				if (_timerFrequency == 0) {
					return; // can't map the timestamps yet
				}
				_timerIsStopwatch = (long)_timerFrequency == Stopwatch.Frequency;
				_timerToStopwatch = Stopwatch.Frequency / (double)_timerFrequency;
				// Unity's timer is the platform's monotonic clock, Mono's Stopwatch starts with the
				// process: read both at once for the offset
				_timerOffset = 0;
				if (CpuTime.TryReadMonotonic(out var monotonic, out var monotonicFrequency)) {
					var now = Stopwatch.GetTimestamp();
					var monotonicInTimer = monotonicFrequency == (long)_timerFrequency ? monotonic : (long)(monotonic * ((double)_timerFrequency / monotonicFrequency));
					_timerOffset = now - (long)(monotonicInTimer * _timerToStopwatch);
					_monotonicOffset = now - (long)(monotonic * (Stopwatch.Frequency / (double)monotonicFrequency));
				}
				NLog.LogManager.GetCurrentClassLogger().Info("Frame timing: Unity's timer runs at {UnityHz} Hz, the stopwatch at {StopwatchHz} Hz with an offset of {Offset} ticks; a frame started at {UnityStart} (mapped), the last recorded one at {OwnStart}",
					_timerFrequency, Stopwatch.Frequency, _timerOffset, ToStopwatch(Timings[0].frameStartTimestamp), _count > 0 ? _frames[(_count - 1) % Capacity].StartTicks : 0);
			}
			// newest first: take the new ones oldest first
			for (var i = n - 1; i >= 0; i--) {
				ref var t = ref Timings[i];
				if (t.frameStartTimestamp <= _lastTimingStart) {
					continue;
				}
				_lastTimingStart = t.frameStartTimestamp;
				Match(ref t);
			}
			// frames that waited too long don't get one anymore; each is looked at once
			var oldest = _count - TimingTimeoutFrames;
			for (var i = System.Math.Max(_timeoutCursor, System.Math.Max(_lastMatched + 1, System.Math.Max(0, _count - Capacity))); i < oldest; i++) {
				ref var f = ref _frames[i % Capacity];
				if (f.Timing == TimingState.Pending) {
					f.Timing = TimingState.None;
				}
			}
			if (oldest > _timeoutCursor) {
				_timeoutCursor = oldest;
			}
		}

		private static void Match(ref FrameTiming t)
		{
			var start = ToStopwatch(t.frameStartTimestamp);
			var tolerance = (long)(MatchToleranceMs * Stopwatch.Frequency / 1000);
			var best = -1L;
			var bestDistance = long.MaxValue;
			var from = System.Math.Max(_lastMatched + 1, System.Math.Max(0, _count - TimingTimeoutFrames * 2));
			for (var i = from; i < _count; i++) {
				var distance = System.Math.Abs(_frames[i % Capacity].StartTicks - start);
				if (distance < bestDistance) {
					bestDistance = distance;
					best = i;
				}
			}
			if (best < 0 || bestDistance > tolerance) {
				return;
			}
			// in order: frames before the match won't get one anymore
			for (var i = from; i < best; i++) {
				ref var skipped = ref _frames[i % Capacity];
				if (skipped.Timing == TimingState.Pending) {
					skipped.Timing = TimingState.None;
				}
			}
			ref var f = ref _frames[best % Capacity];
			f.Timing = TimingState.Matched;
			f.CpuFrameMs = (float)t.cpuFrameTime;
			f.CpuMainMs = (float)System.Math.Max(0, t.cpuMainThreadFrameTime - t.cpuMainThreadPresentWaitTime);
			f.CpuRenderMs = (float)System.Math.Max(0, t.cpuRenderThreadFrameTime);
			f.GpuMs = (float)System.Math.Max(0, t.gpuFrameTime);
			f.PresentWaitMs = (float)System.Math.Max(0, t.cpuMainThreadPresentWaitTime);
			f.UnityStartTicks = start;
			f.FirstSubmitTicks = ToStopwatch(t.firstSubmitTimestamp);
			f.PresentCalledTicks = ToStopwatch(t.cpuTimePresentCalled);
			f.FrameCompleteTicks = ToStopwatch(t.cpuTimeFrameComplete);
			f.SyncInterval = t.syncInterval;
			_lastMatched = best;
		}

		private static long ToStopwatch(ulong timer)
		{
			if (timer == 0) {
				return 0;
			}
			return (_timerIsStopwatch ? (long)timer : (long)(timer * _timerToStopwatch)) + _timerOffset;
		}

		private static long UsecToTicks(long usec) => usec * Stopwatch.Frequency / 1_000_000;

		/// <summary>
		/// Hands on every recorded frame now, those still waiting for their timing without it:
		/// the play ends, and its statistics shouldn't miss the last frames.
		/// </summary>
		internal static void Drain()
		{
			if (_frames == null) {
				return;
			}
			for (var i = System.Math.Max(_finalized, _count - Capacity); i < _count; i++) {
				ref var f = ref _frames[i % Capacity];
				if (f.Timing == TimingState.Pending) {
					f.Timing = TimingState.None;
				}
			}
			FinalizeFrames();
		}

		/// <summary>
		/// Hands on every frame whose own and next frame's timing is settled, in order.
		/// </summary>
		private static void FinalizeFrames()
		{
			if (_finalized < _count - Capacity) {
				_finalized = _count - Capacity; // overwritten before it could be handed on
			}
			while (_finalized + 1 < _count) {
				ref var f = ref _frames[_finalized % Capacity];
				ref var next = ref _frames[(_finalized + 1) % Capacity];
				if (f.Timing == TimingState.Pending || next.Timing == TimingState.Pending) {
					break;
				}
				IncidentDetector.OnFrame(_finalized, ref f, ref next);
				_finalizedThroughTicks = f.EndTicks;
				_finalized++;
			}
		}

		#endregion

		#region Environment

		private static void PollEnvironment()
		{
			var now = Time.realtimeSinceStartupAsDouble;
			var paused = Time.timeScale == 0f;
			if (paused != _paused) {
				_paused = paused;
				Mark(ProfileEvent.Pause, paused ? 1 : 0);
			}
			if (now < _nextPollAt) {
				return;
			}
			var first = _nextPollAt == 0;
			_nextPollAt = now + 1;
			var width = Screen.width;
			var height = Screen.height;
			var fullScreen = Screen.fullScreenMode;
			var vsync = QualitySettings.vSyncCount;
			var targetFps = Application.targetFrameRate;
			var refresh = Screen.currentResolution.refreshRateRatio.value;
			var quality = QualitySettings.GetQualityLevel();
			var displays = Display.displays.Length;
			if (!first) {
				if (width != _width || height != _height || fullScreen != _fullScreen || vsync != _vsync || targetFps != _targetFps || System.Math.Abs(refresh - _refresh) > 0.5) {
					Mark(ProfileEvent.DisplayModeChanged, 0);
				}
				if (displays != _displays) {
					Mark(ProfileEvent.DisplaysChanged, displays);
				}
				if (quality != _quality) {
					Mark(ProfileEvent.QualityChanged, quality);
				}
			}
			_width = width;
			_height = height;
			_fullScreen = fullScreen;
			_vsync = vsync;
			_targetFps = targetFps;
			_refresh = refresh;
			_quality = quality;
			_displays = displays;
		}

		#endregion
	}
}
#endif
