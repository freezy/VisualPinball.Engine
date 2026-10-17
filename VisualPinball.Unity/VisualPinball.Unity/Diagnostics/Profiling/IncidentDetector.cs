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
using UnityEngine;
using VisualPinball.Unity.Simulation;

namespace VisualPinball.Unity.Profiling
{
	public enum IncidentType : byte
	{
		Stutter,
		Hitch,
		Pacing,
		SimStall,
		Degradation,
		/// <summary>The user pressed the "that just stuttered" key.</summary>
		UserMarker,
	}

	public enum IncidentCause : byte
	{
		Unknown,
		PhysicsLock,
		GcPause,
		GcSlice,
		Section,
		RenderGpu,
		RenderFirstUse,
		Scheduling,
		SimOverload,
		FirstUseCode,
		Environment,
	}

	/// <summary>
	/// A performance problem the profile build found, with what it thinks caused it.
	/// </summary>
	public struct Incident
	{
		public IncidentType Type;
		public IncidentCause Cause;
		/// <summary>A bit per further cause that matched.</summary>
		public uint SecondaryCauses;
		/// <summary>The frame (recorder index) the incident is about.</summary>
		public long FrameIndex;
		public long StartTicks;
		public long EndTicks;
		/// <summary>How bad: the frame time, the stall, or the excess, in ms.</summary>
		public float SeverityMs;
		public float FrameMs;
		public float MedianMs;
		public float TargetMs;
		public float CpuMainMs, CpuRenderMs, GpuMs;
		public float UnexplainedMs;
		public int GcDelta;
		public int MaxTickGapUsec;
		public int MaxTickWorkUsec;
		public int LockWaitUsec;
		/// <summary>The section that stood out, for <see cref="IncidentCause.Section"/>.</summary>
		public ProfileSection Section;
		public int SectionUsec;
		/// <summary>The PlayerLoop phase that took longest, see <see cref="FrameRecorder.PhaseNames"/>.</summary>
		public byte Phase;
		public float PhaseMs;
		/// <summary>Seconds since the table became playable.</summary>
		public float SecondsIntoPlay;

		public static string Name(ProfileSection section) => SectionRecorder.Name(section);

		/// <summary>The top-level PlayerLoop system of the given index, or null.</summary>
		public static string PhaseName(byte phase)
		{
			var names = FrameRecorder.PhaseNames;
			return phase < names.Length ? names[phase] : null;
		}

		public static string Name(IncidentType type) => type switch {
			IncidentType.Stutter => "stutter",
			IncidentType.Hitch => "hitch",
			IncidentType.Pacing => "pacing",
			IncidentType.SimStall => "sim-stall",
			IncidentType.Degradation => "degradation",
			IncidentType.UserMarker => "user-marker",
			_ => "unknown",
		};

		public static string Name(IncidentCause cause) => cause switch {
			IncidentCause.PhysicsLock => "physics-lock",
			IncidentCause.GcPause => "gc-correlated",
			IncidentCause.GcSlice => "gc-incremental-slice",
			IncidentCause.Section => "vpe-section",
			IncidentCause.RenderGpu => "render-gpu",
			IncidentCause.RenderFirstUse => "render-first-use",
			IncidentCause.Scheduling => "scheduling",
			IncidentCause.SimOverload => "sim-overload",
			IncidentCause.FirstUseCode => "first-use-code",
			IncidentCause.Environment => "environment",
			_ => "unknown",
		};
	}

	/// <summary>
	/// Finds stutters, hitches, pacing problems, simulation stalls and slowdowns in the
	/// finalized frames of the <see cref="FrameRecorder"/>, says what most likely caused them
	/// (plan §5), and raises <see cref="Detected"/>.
	/// </summary>
	///
	/// <remarks>
	/// Only while a table plays (from playable on), not while paused or in the profile build's
	/// own frames. O(1) per frame apart from a 120-entry sort, and allocation-free. Main thread
	/// only.
	/// </remarks>
	public static unsafe class IncidentDetector
	{
		private const int MedianFrames = 120;
		private const float StutterFactor = 1.6f;
		private const float StutterMarginMs = 4f;
		private const float StutterTargetFactor = 1.25f;
		private const float HitchMinorMs = 50f;
		private const float HitchMajorMs = 250f;
		private const float PacingWindowSeconds = 2f;
		private const float WarmupSeconds = 30f;
		private const float BaselineSeconds = 60f;
		private const float DegradationFactor = 1.25f;
		private const int DegradationWindows = 3;
		private const int TickGapStallUsec = 5000;
		private const int SimStallCooldownFrames = 60;
		private const float IncidentSliceMs = 3f;

		/// <summary>
		/// A problem was found. Main thread, in the frame that finalized it.
		/// </summary>
		public static event Action<Incident> Detected;

		/// <summary>Incidents found in this table's play so far.</summary>
		public static int Count { get; private set; }

		private static readonly float[] Intervals = new float[MedianFrames];
		private static readonly float[] Sorted = new float[MedianFrames];
		private static int _intervalCount;
		private static int _intervalHead;

		// pacing window
		private static readonly float[] Presents = new float[1024];
		private static int _presentCount;
		private static double _pacingWindowStart;
		private static long _lastPresentTicks;

		// baseline and degradation
		private static readonly int[] BaselineHistogram = new int[1000]; // 0.25 ms buckets up to 250 ms
		private static double _referenceFrom;
		private static float _baselineMs;
		private static int _baselineFrames;
		private static double _windowStart;
		private static readonly float[] WindowFrames = new float[1024];
		private static int _windowCount;
		private static int _slowWindows;
		private static bool _degraded;

		// per-section p99 estimates, as an exponentially weighted high quantile
		private static readonly float[] SectionP99 = new float[FrameSample.MaxSections];

		// ticks
		private static long _ticksSeen;
		private static long _locksSeen;
		private static int _simStallCooldown;

		private static double _playableAt = -1;
		private static int _previousGc = -1;
		private static float _presentWaitMedian;
		private static float _unexplainedBaseline;
		private static bool _playEnded;
		private static long _previousGcUsed = -1;
		private static int _gcRisingFrames;

		internal static void Reset()
		{
			Count = 0;
			_intervalCount = _intervalHead = 0;
			_presentCount = 0;
			_pacingWindowStart = 0;
			_lastPresentTicks = 0;
			Array.Clear(BaselineHistogram, 0, BaselineHistogram.Length);
			Array.Clear(SectionP99, 0, SectionP99.Length);
			_baselineMs = 0;
			_baselineFrames = 0;
			_referenceFrom = 0;
			_windowStart = 0;
			_windowCount = 0;
			_slowWindows = 0;
			_degraded = false;
			_ticksSeen = SimulationTrace.TickCount;
			_locksSeen = SimulationTrace.LockCount;
			_simStallCooldown = 0;
			_playableAt = -1;
			_previousGc = -1;
			_previousGcUsed = -1;
			_gcRisingFrames = 0;
			_presentWaitMedian = 0;
			_unexplainedBaseline = 0;
			_playEnded = false;
		}

		/// <summary>
		/// The table stopped playing: the frames until the next load aren't play.
		/// </summary>
		internal static void EndPlay() => _playEnded = true;

		internal static void OnTableLoad()
		{
			Reset();
			ProfileMetrics.Reset();
		}

		/// <summary>
		/// The reference values start over, e.g. after the display mode changed.
		/// </summary>
		private static void RestartReference(double now)
		{
			Array.Clear(BaselineHistogram, 0, BaselineHistogram.Length);
			_baselineMs = 0;
			_baselineFrames = 0;
			_referenceFrom = now + WarmupSeconds;
			_slowWindows = 0;
			_degraded = false;
		}

		internal static void OnFrame(long index, ref FrameSample f, ref FrameSample next)
		{
			var load = LoadAttempt.Current;
			if (load == null || load.PlayableMs < 0 || _playEnded) {
				_playableAt = -1;
				return;
			}
			var now = Ticks.ToSeconds(f.StartTicks);
			if (_playableAt < 0) {
				_playableAt = now;
				RestartReference(now);
				_ticksSeen = SimulationTrace.TickCount;
				_locksSeen = SimulationTrace.LockCount;
				_previousGc = f.GcCount;
				_previousGcUsed = f.GcUsedBytes;
			}
			var gcDelta = _previousGc >= 0 ? f.GcCount - _previousGc : 0;
			_previousGc = f.GcCount;
			TrackGc(ref f);

			// the simulation thread's view of the frame
			ScanTicks(f.StartTicks, next.StartTicks, out var maxGap, out var maxWork, out var dropped, out var tickGc);
			var lockWait = ScanLocks(f.StartTicks, next.StartTicks);

			if (f.Paused != 0 || next.Paused != 0 || f.SelfInduced != 0 || next.SelfInduced != 0) {
				// the next present interval would span the excluded frames
				_lastPresentTicks = 0;
				_presentCount = 0;
				_pacingWindowStart = 0;
				return;
			}
			if (f.HasEvent(ProfileEvent.DisplayModeChanged) || f.HasEvent(ProfileEvent.DisplaysChanged) || f.HasEvent(ProfileEvent.QualityChanged) || f.HasEvent(ProfileEvent.Focus)) {
				RestartReference(now);
			}

			var frameMs = (float)Ticks.ToMs(next.StartTicks - f.StartTicks);
			var presentMs = f.Timing == TimingState.Matched && next.Timing == TimingState.Matched && f.PresentCalledTicks > 0 && next.PresentCalledTicks > 0
				? (float)Ticks.ToMs(next.PresentCalledTicks - f.PresentCalledTicks) : 0f;
			var median = PushInterval(frameMs);
			var target = TargetIntervalMs(ref f);
			UpdateBaseline(now, frameMs);
			UpdateSections(ref f);
			TrackPresentWait(ref f);

			var incident = default(Incident);
			incident.FrameIndex = index;
			incident.StartTicks = f.StartTicks;
			incident.EndTicks = next.StartTicks;
			incident.FrameMs = frameMs;
			incident.MedianMs = median;
			incident.TargetMs = target;
			incident.GcDelta = gcDelta + tickGc;
			incident.MaxTickGapUsec = maxGap;
			incident.MaxTickWorkUsec = maxWork;
			incident.LockWaitUsec = lockWait;
			incident.SecondsIntoPlay = (float)(now - _playableAt);

			// a frame that took long
			if (_intervalCount >= MedianFrames / 2) {
				if (frameMs > HitchMinorMs) {
					incident.Type = IncidentType.Hitch;
					incident.SeverityMs = frameMs;
					Attribute(ref incident, ref f, ref next, gcDelta);
					Raise(ref incident);
				} else if (frameMs > System.Math.Max(StutterFactor * median, median + StutterMarginMs) && (target <= 0 || frameMs > StutterTargetFactor * target)) {
					incident.Type = IncidentType.Stutter;
					incident.SeverityMs = frameMs - median;
					Attribute(ref incident, ref f, ref next, gcDelta);
					Raise(ref incident);
				}
			}

			// the simulation fell behind or stalled
			if (_simStallCooldown > 0) {
				_simStallCooldown--;
			} else if (maxGap > TickGapStallUsec || dropped > 0 || f.SnapshotBackwards != 0) {
				var stall = incident;
				stall.Type = IncidentType.SimStall;
				stall.SeverityMs = System.Math.Max(maxGap, dropped) / 1000f;
				AttributeStall(ref stall, gcDelta + tickGc, lockWait, maxWork);
				_simStallCooldown = SimStallCooldownFrames;
				Raise(ref stall);
			}

			TrackPacing(ref f, ref next, target, median, now, ref incident);
			TrackDegradation(now, frameMs, target, ref incident);
			// after the frame's incidents, which count in the window this frame may close
			ProfileMetrics.OnFrame(frameMs, presentMs, f.GcCount, ref f);
		}

		#region Reference values

		private static float PushInterval(float ms)
		{
			Intervals[_intervalHead] = ms;
			_intervalHead = (_intervalHead + 1) % MedianFrames;
			if (_intervalCount < MedianFrames) {
				_intervalCount++;
			}
			Array.Copy(Intervals, Sorted, _intervalCount);
			Array.Sort(Sorted, 0, _intervalCount);
			return Sorted[_intervalCount / 2];
		}

		/// <summary>
		/// The interval the settings ask for: vsync, else the target frame rate, else none (0).
		/// </summary>
		private static float TargetIntervalMs(ref FrameSample f)
		{
			if (QualitySettings.vSyncCount > 0) {
				var hz = Screen.currentResolution.refreshRateRatio.value;
				return hz > 0 ? (float)(1000.0 / hz * QualitySettings.vSyncCount) : 0f;
			}
			return Application.targetFrameRate > 0 ? 1000f / Application.targetFrameRate : 0f;
		}

		private static void UpdateBaseline(double now, float frameMs)
		{
			if (now < _referenceFrom || _baselineMs > 0) {
				return;
			}
			var bucket = (int)(frameMs * 4);
			BaselineHistogram[bucket < BaselineHistogram.Length ? bucket : BaselineHistogram.Length - 1]++;
			_baselineFrames++;
			if (now < _referenceFrom + BaselineSeconds) {
				return;
			}
			var half = _baselineFrames / 2;
			var seen = 0;
			for (var i = 0; i < BaselineHistogram.Length; i++) {
				seen += BaselineHistogram[i];
				if (seen > half) {
					_baselineMs = (i + 0.5f) / 4f;
					break;
				}
			}
			_windowStart = now;
			_windowCount = 0;
		}

		private static void UpdateSections(ref FrameSample f)
		{
			var n = System.Math.Min(SectionRecorder.Count, FrameSample.MaxSections);
			for (var i = 0; i < n; i++) {
				var us = f.SectionUsec[i];
				// moves up by 1 % of the gap when above, down by 0.01 % when below: ~p99
				var p = SectionP99[i];
				SectionP99[i] = us > p ? p + (us - p) * 0.01f : p - (p - us) * 0.0001f;
			}
		}

		/// <summary>
		/// A slowly moving median of the present wait, as the reference for a late presentation.
		/// </summary>
		private static void TrackPresentWait(ref FrameSample f)
		{
			var phase = FrameRecorder.PresentWaitPhase;
			var ms = phase >= 0 && phase < FrameSample.Phases - 1 ? (float)Ticks.ToMs(f.Phase[phase + 1] - f.Phase[phase]) : 0f;
			// what a frame usually spends outside the sections and the present wait
			var sections = 0L;
			var n = System.Math.Min(SectionRecorder.Count, FrameSample.MaxSections);
			for (var i = 0; i < n; i++) {
				sections += f.SectionUsec[i];
			}
			var unexplained = System.Math.Max(0f, (float)Ticks.ToMs(f.EndTicks - f.StartTicks) - ms - sections / 1000f);
			_unexplainedBaseline += unexplained > _unexplainedBaseline ? 0.02f : -0.02f;
			if (_unexplainedBaseline < 0) {
				_unexplainedBaseline = 0;
			}
			if (phase < 0) {
				return;
			}
			_presentWaitMedian += ms > _presentWaitMedian ? 0.05f : -0.05f;
			if (_presentWaitMedian < 0) {
				_presentWaitMedian = 0;
			}
		}

		private static void TrackGc(ref FrameSample f)
		{
			if (f.GcUsedBytes < 0) {
				return;
			}
			if (_previousGcUsed >= 0 && f.GcUsedBytes > _previousGcUsed) {
				_gcRisingFrames++;
			} else if (_previousGcUsed >= 0 && f.GcUsedBytes < _previousGcUsed) {
				_gcRisingFrames = 0;
			}
			_previousGcUsed = f.GcUsedBytes;
		}

		#endregion

		#region Attribution

		/// <summary>
		/// The first matching cause, in the order of plan §5; the others become secondary.
		/// </summary>
		private static void Attribute(ref Incident incident, ref FrameSample f, ref FrameSample next, int gcDelta)
		{
			// main-thread time no section explains, and the phase that took longest
			var sectionUsec = 0L;
			var topSection = -1;
			var topRatio = 0f;
			var n = System.Math.Min(SectionRecorder.Count, FrameSample.MaxSections);
			for (var i = 0; i < n; i++) {
				var us = f.SectionUsec[i];
				sectionUsec += us;
				var p99 = System.Math.Max(SectionP99[i], 200f);
				var ratio = us / p99;
				if (us > 1000 && ratio > 2f && ratio > topRatio) {
					topRatio = ratio;
					topSection = i;
				}
			}
			var phaseMs = 0f;
			for (var i = 0; i < FrameSample.Phases - 1; i++) {
				var ms = (float)Ticks.ToMs(f.Phase[i + 1] - f.Phase[i]);
				if (ms > phaseMs) {
					phaseMs = ms;
					incident.Phase = (byte)i;
				}
			}
			incident.PhaseMs = phaseMs;
			// waiting for the previous frame to be presented (vsync, GPU) isn't the main thread's work
			var waitPhase = FrameRecorder.PresentWaitPhase;
			var presentWaitMs = waitPhase >= 0 && waitPhase < FrameSample.Phases - 1 ? (float)Ticks.ToMs(f.Phase[waitPhase + 1] - f.Phase[waitPhase]) : 0f;
			var workMs = (float)Ticks.ToMs(f.EndTicks - f.StartTicks) - presentWaitMs;
			var timed = f.Timing == TimingState.Matched;
			incident.CpuMainMs = timed ? f.CpuMainMs : -1;
			incident.CpuRenderMs = timed ? f.CpuRenderMs : -1;
			incident.GpuMs = timed ? f.GpuMs : -1;
			incident.UnexplainedMs = System.Math.Max(0, workMs - sectionUsec / 1000f);
			_presentWaitMedian = _presentWaitMedian <= 0 ? presentWaitMs : _presentWaitMedian;

			var causes = 0u;
			void Add(IncidentCause cause) => causes |= 1u << (int)cause;

			if (incident.LockWaitUsec > 1000) {
				Add(IncidentCause.PhysicsLock);
			}
			var tickStall = incident.MaxTickGapUsec > 2000 || incident.MaxTickWorkUsec > 2000;
			if (incident.GcDelta > 0 && (tickStall || incident.UnexplainedMs >= 1f)) {
				Add(IncidentCause.GcPause);
			}
			// a slice is unexplained time above what a frame usually has (rendering, Unity's own work)
			var extraUnexplained = incident.UnexplainedMs - _unexplainedBaseline;
			if (incident.GcDelta == 0 && _gcRisingFrames > 30 && extraUnexplained > 1.5f && System.Math.Abs(extraUnexplained - IncidentSliceMs) < 1.5f) {
				Add(IncidentCause.GcSlice);
			}
			if (topSection >= 0) {
				Add(IncidentCause.Section);
				incident.Section = (ProfileSection)topSection;
				incident.SectionUsec = f.SectionUsec[topSection];
			}
			if (timed) {
				var target = incident.TargetMs;
				var render = System.Math.Max(f.CpuRenderMs, f.GpuMs);
				var renderBound = target > 0
					? render > target && f.CpuMainMs <= target
					: render > f.CpuMainMs && render > 1.5f * incident.MedianMs;
				// GPU and present costs of a frame often show up in the next one
				var nextRender = next.Timing == TimingState.Matched ? System.Math.Max(next.CpuRenderMs, next.GpuMs) : 0f;
				if (renderBound || nextRender > System.Math.Max(target, incident.MedianMs) * 1.5f) {
					var firstUse = f.HasEvent(ProfileEvent.FirstUseLight) || f.HasEvent(ProfileEvent.FirstUseFlasher) || f.HasEvent(ProfileEvent.FirstUseMaterial)
						|| PreviousFrameHasRenderFirstUse(incident.FrameIndex);
					Add(firstUse ? IncidentCause.RenderFirstUse : IncidentCause.RenderGpu);
				}
			}
			// most of the extra time went into waiting for the last frame's presentation: the GPU
			// or the compositor was late (with a fast GPU: a missed vsync, or the compositor)
			if (presentWaitMs - _presentWaitMedian >= 0.4f * (incident.FrameMs - incident.MedianMs)) {
				Add(IncidentCause.RenderGpu);
			}
			// both threads held up at once, by neither GC nor the lock, with the time on no
			// section: the OS didn't run them (descheduled, oversubscribed)
			if (incident.GcDelta == 0 && incident.LockWaitUsec < 1000 && incident.MaxTickGapUsec > 4000
			    && incident.UnexplainedMs - _unexplainedBaseline > 0.5f * (incident.FrameMs - incident.MedianMs)) {
				Add(IncidentCause.Scheduling);
			}
			if (incident.MaxTickWorkUsec > 1500) {
				Add(IncidentCause.SimOverload);
			}
			if (f.HasEvent(ProfileEvent.FirstUseMultiball) || f.HasEvent(ProfileEvent.FirstUseServiceMenu) || f.HasEvent(ProfileEvent.FirstUseDrain)) {
				Add(IncidentCause.FirstUseCode);
			}
			if (f.HasEvent(ProfileEvent.DisplayModeChanged) || f.HasEvent(ProfileEvent.DisplaysChanged) || f.HasEvent(ProfileEvent.Focus)
			    || f.HasEvent(ProfileEvent.QualityChanged) || f.HasEvent(ProfileEvent.VramOverBudget) || f.HasEvent(ProfileEvent.Pause)) {
				Add(IncidentCause.Environment);
			}
			SetCauses(ref incident, causes);
		}

		private static void AttributeStall(ref Incident incident, int gcDelta, int lockWait, int maxWork)
		{
			var causes = 0u;
			void Add(IncidentCause cause) => causes |= 1u << (int)cause;
			if (gcDelta > 0) {
				Add(IncidentCause.GcPause);
			}
			if (maxWork > 1500) {
				Add(IncidentCause.SimOverload);
			}
			if (gcDelta == 0 && maxWork <= 1500) {
				// a gap without GC or work: the thread wasn't scheduled
				Add(IncidentCause.Scheduling);
			}
			SetCauses(ref incident, causes);
		}

		private static void SetCauses(ref Incident incident, uint causes)
		{
			incident.Cause = IncidentCause.Unknown;
			for (var i = 1; i < 32; i++) {
				if ((causes & (1u << i)) != 0) {
					incident.Cause = (IncidentCause)i;
					causes &= ~(1u << i);
					break;
				}
			}
			incident.SecondaryCauses = causes;
		}

		private static bool PreviousFrameHasRenderFirstUse(long index)
		{
			if (index <= 0 || index < FrameRecorder.Count - FrameRecorder.Capacity + 1) {
				return false;
			}
			ref var previous = ref FrameRecorder.Frames[(index - 1) % FrameRecorder.Capacity];
			return previous.HasEvent(ProfileEvent.FirstUseLight) || previous.HasEvent(ProfileEvent.FirstUseFlasher) || previous.HasEvent(ProfileEvent.FirstUseMaterial);
		}

		#endregion

		#region Simulation thread

		/// <summary>
		/// The ticks published since the last frame that started before <paramref name="endTicks"/>:
		/// the largest wait gap and work time, dropped backlog and GC collections.
		/// </summary>
		private static void ScanTicks(long startTicks, long endTicks, out int maxGap, out int maxWork, out int dropped, out int gc)
		{
			maxGap = maxWork = dropped = gc = 0;
			var committed = SimulationTrace.TickCount;
			if (_ticksSeen < committed - SimulationTrace.SessionTickCapacity) {
				_ticksSeen = committed - SimulationTrace.SessionTickCapacity;
			}
			var endUsec = VpeClock.TicksToUsec(endTicks);
			var firstGc = int.MinValue;
			var lastGc = 0;
			for (; _ticksSeen < committed; _ticksSeen++) {
				if (!SimulationTrace.TryReadTick(_ticksSeen, out var t)) {
					continue;
				}
				if (t.StartUsec > endUsec) {
					break; // belongs to the next frame
				}
				// a stall shows as a longer wait than asked for, or, when it hit the tick itself, as
				// lateness of the next ones
				var expectedGap = System.Math.Max(0, t.WaitRequestedUsec);
				var gap = System.Math.Max(t.WaitUsec - expectedGap, (int)System.Math.Min(int.MaxValue, t.StartUsec - t.TargetUsec));
				if (gap > maxGap) {
					maxGap = gap;
				}
				if (t.TotalUsec > maxWork) {
					maxWork = t.TotalUsec;
				}
				dropped += t.DroppedUsec;
				ProfileMetrics.OnTick(t.TotalUsec, t.DroppedUsec);
				if (firstGc == int.MinValue) {
					firstGc = t.Gc0;
				}
				lastGc = t.Gc0;
			}
			gc = firstGc == int.MinValue ? 0 : lastGc - firstGc;
		}

		private static int ScanLocks(long startTicks, long endTicks)
		{
			var maxWait = 0;
			var committed = SimulationTrace.LockCount;
			if (_locksSeen < committed - SimulationTrace.SessionLockCapacity) {
				_locksSeen = committed - SimulationTrace.SessionLockCapacity;
			}
			var endUsec = VpeClock.TicksToUsec(endTicks);
			for (; _locksSeen < committed; _locksSeen++) {
				if (!SimulationTrace.TryReadLock(_locksSeen, out var l)) {
					continue;
				}
				if (l.StartUsec > endUsec) {
					break;
				}
				if (l.WaitUsec > maxWait) {
					maxWait = l.WaitUsec;
				}
			}
			return maxWait;
		}

		#endregion

		#region Pacing and degradation

		private static void TrackPacing(ref FrameSample f, ref FrameSample next, float target, float median, double now, ref Incident template)
		{
			if (f.Timing != TimingState.Matched || f.PresentCalledTicks == 0) {
				return;
			}
			if (_lastPresentTicks > 0 && _presentCount < Presents.Length) {
				Presents[_presentCount++] = (float)Ticks.ToMs(f.PresentCalledTicks - _lastPresentTicks);
			}
			_lastPresentTicks = f.PresentCalledTicks;
			if (_pacingWindowStart == 0) {
				_pacingWindowStart = now;
			}
			if (now - _pacingWindowStart < PacingWindowSeconds) {
				return;
			}
			var reference0 = target > 0 ? target : median;
			// a few intervals of twice the reference or more are stutters, reported as such; many
			// are the cadence itself (missed vsync every other frame), which is what pacing is
			var longIntervals = 0;
			for (var i = 0; i < _presentCount; i++) {
				if (Presents[i] >= 2f * reference0) {
					longIntervals++;
				}
			}
			var n = 0;
			for (var i = 0; i < _presentCount; i++) {
				if (longIntervals >= 3 || Presents[i] < 2f * reference0) {
					Presents[n++] = Presents[i];
				}
			}
			_presentCount = 0;
			_pacingWindowStart = now;
			if (n < 20) {
				return;
			}
			var sum = 0.0;
			for (var i = 0; i < n; i++) {
				sum += Presents[i];
			}
			var mean = sum / n;
			var variance = 0.0;
			var longOnes = 0;
			var alternations = 0;
			var bestRun = 0;
			for (var i = 0; i < n; i++) {
				var d = Presents[i] - mean;
				variance += d * d;
				if (target > 0 && Presents[i] >= 1.5f * target) {
					longOnes++;
				}
				// long, short, long, short...
				if (i > 0 && (Presents[i] - mean) * (Presents[i - 1] - mean) < 0 && System.Math.Abs(Presents[i] - Presents[i - 1]) > 0.25 * mean) {
					alternations++;
					bestRun = System.Math.Max(bestRun, alternations);
				} else {
					alternations = 0;
				}
			}
			var stddev = (float)System.Math.Sqrt(variance / n);
			var reference = target > 0 ? target : median;
			var uneven = stddev > 0.25f * reference || target > 0 && longOnes >= 3 || target <= 0 && bestRun >= 10;
			if (!uneven) {
				return;
			}
			var incident = template;
			incident.Type = IncidentType.Pacing;
			incident.SeverityMs = stddev;
			incident.MedianMs = (float)mean;
			incident.StartTicks = f.StartTicks - Ticks.FromSeconds(PacingWindowSeconds);
			incident.Cause = IncidentCause.Unknown;
			incident.SecondaryCauses = 0;
			Raise(ref incident);
		}

		private static void TrackDegradation(double now, float frameMs, float target, ref Incident template)
		{
			if (_baselineMs <= 0) {
				return;
			}
			if (_windowCount < WindowFrames.Length) {
				WindowFrames[_windowCount++] = frameMs;
			}
			if (now - _windowStart < 1) {
				return;
			}
			_windowStart = now;
			var n = _windowCount;
			_windowCount = 0;
			if (n == 0) {
				return;
			}
			Array.Sort(WindowFrames, 0, n);
			var median = WindowFrames[n / 2];
			var slow = target > 0 ? 1000f / median < 0.9f * (1000f / target) : median > DegradationFactor * _baselineMs;
			_slowWindows = slow ? _slowWindows + 1 : 0;
			if (_slowWindows < DegradationWindows) {
				_degraded = false;
				return;
			}
			if (_degraded) {
				return; // one incident per slow stretch
			}
			_degraded = true;
			var incident = template;
			incident.Type = IncidentType.Degradation;
			incident.MedianMs = median;
			incident.SeverityMs = median - _baselineMs;
			incident.StartTicks = Ticks.FromSeconds(now - DegradationWindows) ;
			incident.Cause = IncidentCause.Unknown;
			incident.SecondaryCauses = 0;
			Raise(ref incident);
		}

		#endregion

		/// <summary>
		/// The user said it just stuttered: an incident over the last seconds, whatever the
		/// detectors think.
		/// </summary>
		public static void UserMarker()
		{
			var now = Stopwatch.GetTimestamp();
			var incident = default(Incident);
			incident.Type = IncidentType.UserMarker;
			incident.FrameIndex = FrameRecorder.Count - 1;
			// the window before it comes from the capture's pre-window
			incident.StartTicks = now;
			incident.EndTicks = now;
			incident.SecondsIntoPlay = _playableAt >= 0 ? (float)(Ticks.ToSeconds(now) - _playableAt) : -1;
			Raise(ref incident);
		}

		private static void Raise(ref Incident incident)
		{
			Count++;
			ProfileMetrics.OnIncident(incident);
			try {
				Detected?.Invoke(incident);
			} catch (Exception e) {
				NLog.LogManager.GetCurrentClassLogger().Warn(e, "An incident handler failed.");
			}
		}
	}

	/// <summary>
	/// Stopwatch tick conversions.
	/// </summary>
	internal static class Ticks
	{
		private static readonly double Frequency = Stopwatch.Frequency;

		public static double ToMs(long ticks) => ticks * 1000.0 / Frequency;
		public static double ToSeconds(long ticks) => ticks / Frequency;
		public static long FromSeconds(double seconds) => (long)(seconds * Frequency);
	}
}
#endif
