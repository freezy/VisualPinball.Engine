// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// https://github.com/freezy/VisualPinball.Engine
//
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using NLog;
using Unity.Mathematics;
using Logger = NLog.Logger;

namespace VisualPinball.Unity.Simulation
{
	/// <summary>
	/// Allocation-free timing tracer for the simulation thread and the main-thread
	/// side of the physics engine. Records one entry per simulation tick, one per
	/// rendered frame and one per traced main-thread physics lock acquisition into
	/// preallocated ring buffers.
	/// </summary>
	/// <remarks>
	/// <para>
	/// In <see cref="SimulationTraceMode.Full"/> it keeps ten minutes and writes them as CSV
	/// (plus a summary) when the simulation thread stops. In
	/// <see cref="SimulationTraceMode.Summary"/>, the default of a player, it keeps one minute
	/// and only summarizes it, on a background thread every minute and once more when the
	/// simulation stops. The newest summary is <see cref="LatestSummary"/>, and it's also
	/// written to <see cref="SummaryPath"/>, so error reports can carry it, even after a crash.
	/// </para>
	/// <para>
	/// The rings belong to one simulation run and are read while they're written using a
	/// per-slot sequence (a seqlock), so a record being overwritten is never read. While
	/// recording, the hot path costs a handful of timestamp reads and one memory barrier per
	/// tick. All timestamps are <see cref="VpeClock"/> microseconds, so tick, frame and lock
	/// records share one clock.
	/// </para>
	/// </remarks>
	public static class SimulationTrace
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>Per simulation tick, written by the simulation thread.</summary>
		public struct TickRecord
		{
			public long Index;
			/// <summary>Wall clock at tick start.</summary>
			public long StartUsec;
			/// <summary>Wall clock the tick was scheduled for.</summary>
			public long TargetUsec;
			/// <summary>Simulation clock at tick start, before the main-thread clock sync.</summary>
			public long SimTimeUsec;
			/// <summary>Latest Unity clock published by the main thread.</summary>
			public long SyncedClockUsec;
			/// <summary>Physics frame time after the physics update.</summary>
			public long PhysicsTimeUsec;
			/// <summary>Clock sync advance applied at this tick.</summary>
			public int ClockJumpUsec;
			/// <summary>Timetable backlog dropped before this tick.</summary>
			public int DroppedUsec;
			/// <summary>Time to the scheduled target when the wait started (negative when already late).</summary>
			public int WaitRequestedUsec;
			/// <summary>Time between the end of the previous tick and the start of this one.</summary>
			public int WaitUsec;
			/// <summary>0 = none, 1 = yield, 2 = sleep.</summary>
			public int WaitMode;
			public int SwitchesUsec;
			public int InputUsec;
			public int OutputsUsec;
			/// <summary>Wait for the physics lock in ExecuteTick.</summary>
			public int LockWaitUsec;
			/// <summary>Staged kinematic transforms applied.</summary>
			public int KinematicUsec;
			/// <summary>Kinematic octree rebuild, when one happened.</summary>
			public int RebuildUsec;
			/// <summary>Burst physics update.</summary>
			public int ExecuteUsec;
			/// <summary>Physics time advanced by the update (1000 per sub step).</summary>
			public int PhysicsAdvanceUsec;
			/// <summary>ExecuteTick total, including the lock wait.</summary>
			public int PhysicsUsec;
			public int PlumbUsec;
			public int FenceUsec;
			/// <summary>Gamelogic shared-state writer (coils, lamps, GI).</summary>
			public int WriterUsec;
			public int DiagUsec;
			/// <summary>Wait for the physics lock in the animation snapshot.</summary>
			public int SnapshotLockUsec;
			/// <summary>Animation snapshot copy, including its lock wait.</summary>
			public int SnapshotUsec;
			/// <summary>SimulationTick total.</summary>
			public int TotalUsec;
			public int BallCount;
			/// <summary>Staged kinematic samples drained this tick.</summary>
			public int KinematicUpdates;
			/// <summary>Kinematic items currently kept out of the octree.</summary>
			public int MovingItems;
			/// <summary>GC.CollectionCount(0) at tick start.</summary>
			public int Gc0;
			/// <summary>Physics work counters of the update.</summary>
			public PhysicsCounters Counters;
			public int BallOctreeRefits;
		}

		/// <summary>Per Unity frame, written by the main thread in PhysicsEngine.Update.</summary>
		public struct FrameRecord
		{
			public int Frame;
			public long StartUsec;
			/// <summary>Time.timeAsDouble, the clock the simulation thread is synced to.</summary>
			public long UnityTimeUsec;
			public long RealtimeUsec;
			public int UnscaledDeltaUsec;
			public int DeltaUsec;
			public float TimeScale;
			public int Gc0;
			/// <summary>Simulation clock of the snapshot the frame rendered.</summary>
			public long SnapshotSimTimeUsec;
			/// <summary>Wall clock the rendered snapshot was published at.</summary>
			public long SnapshotPublishUsec;
			public int SnapshotBallCount;
			public int DrainUsec;
			/// <summary>1 when the event drain skipped the frame because the simulation thread held the lock.</summary>
			public int DrainSkipped;
			public int EventsDrained;
			public int ActionsDrained;
			public int ScanUsec;
			public int KinematicChanged;
			public int KinematicStopped;
			public int ApplyUsec;
			/// <summary>PhysicsEngine.Update total.</summary>
			public int UpdateUsec;
			public int Ball0Id;
			public float3 Ball0;
			public int Ball1Id;
			public float3 Ball1;
			public int Ball2Id;
			public float3 Ball2;
			public int Ball3Id;
			public float3 Ball3;
		}

		/// <summary>A traced main-thread acquisition of the physics lock.</summary>
		public struct LockRecord
		{
			public long StartUsec;
			public LockSite Site;
			public int WaitUsec;
			public int HoldUsec;
		}

		public enum LockSite : byte
		{
			Unknown,
			RegisterBall,
			UnregisterBall,
			KickerBallId,
			NudgeStatus,
			TiltStatus,
			NudgeTelemetry,
		}

		/// <summary>Main-thread scope that times a physics lock acquisition.</summary>
		internal readonly struct LockScope : IDisposable
		{
			private readonly object _lock;
			private readonly LockSite _site;
			private readonly long _startUsec;
			private readonly long _acquiredUsec;

			public LockScope(object lockObject, LockSite site)
			{
				_lock = lockObject;
				_site = site;
				_startUsec = NowUsec();
				Monitor.Enter(lockObject);
				_acquiredUsec = NowUsec();
			}

			public void Dispose()
			{
				var releasedUsec = NowUsec();
				Monitor.Exit(_lock);
				RecordLock(_site, _startUsec, _acquiredUsec, releasedUsec);
			}
		}

		/// <summary>
		/// What the next simulation run records. See <see cref="ResolveMode"/>.
		/// </summary>
		public static SimulationTraceMode Mode { get; set; }

		/// <summary>Where the CSV files go in <see cref="SimulationTraceMode.Full"/>. See <see cref="DefaultOutputDirectory"/>.</summary>
		public static string OutputDirectory { get; set; }

		/// <summary>
		/// Where every new summary is written as well, so it's still there after a crash. By
		/// default next to the session's log (see <see cref="VpeLog.SimulationTraceSummaryPath"/>),
		/// or nowhere without a log file. Any thread.
		/// </summary>
		public static string SummaryPath {
			get {
				var path = _summaryPath;
				if (path != null) {
					return path;
				}
				var log = VpeLog.LogFile;
				return log != null ? VpeLog.SimulationTraceSummaryPath(log) : null;
			}
			set => _summaryPath = value;
		}

		private static volatile string _summaryPath;

		/// <summary>
		/// Length of the recorded window in <see cref="SimulationTraceMode.Full"/>. Older records
		/// are overwritten, so a long session keeps its last minutes; memory and per-tick cost do
		/// not depend on the session length.
		/// </summary>
		public const int WindowSeconds = 600;

		/// <summary>Length of the recorded window in <see cref="SimulationTraceMode.Summary"/>.</summary>
		public const int SummaryWindowSeconds = 60;

		/// <summary>How often the summary is refreshed while the simulation runs.</summary>
		public const int SummaryIntervalSeconds = 60;

		/// <summary>Sessions kept in the output folder; older files are deleted when a new one is written.</summary>
		public const int KeptSessions = 3;

		private const int TicksPerSecond = 1000; // 1 kHz
		private const int FramesPerSecond = 240; // up to 240 fps
		private const int LockCapacity = 1 << 16;
		private const int SummaryLockCapacity = 1 << 13;

		private static volatile bool _recording;
		private static volatile TraceSession _session;
		private static volatile string _latestSummary;
		private static long _latestSummaryTicks;
		private static SummaryBuffers _spareBuffers;
		private static long _generation;
		private static long _currentGeneration;
		// orders run changes against publication: a superseded run never publishes
		private static readonly object PublishLock = new();

		/// <summary>
		/// The last summary and release of a session that ended, for tests.
		/// </summary>
		internal static Task PendingEnd { get; private set; }

		/// <summary>
		/// The newest summary of the simulation's timing, or null if there is none yet. Refreshed
		/// every minute while a table plays, and when it stops; cleared when the next run starts,
		/// so it always describes the table that plays, or played last. Any thread.
		/// </summary>
		public static string LatestSummary => _latestSummary;

		/// <summary>
		/// When <see cref="LatestSummary"/> was computed (UTC), or default.
		/// </summary>
		public static DateTime LatestSummaryUtc {
			get {
				var ticks = Interlocked.Read(ref _latestSummaryTicks);
				return ticks == 0 ? default : new DateTime(ticks, DateTimeKind.Utc);
			}
		}

		/// <summary>
		/// Raised with each new summary, on a background thread.
		/// </summary>
		public static event Action<string> SummaryUpdated;

		/// <summary>
		/// Command-line override for player builds: <c>--simulation-trace</c> records,
		/// <c>--no-simulation-trace</c> does not (one dash works too). Null when neither is given.
		/// </summary>
		public static bool? CommandLineOverride() => ParseCommandLineOverride(Environment.GetCommandLineArgs());

		/// <summary>
		/// <see cref="CommandLineOverride"/> for given arguments. The last switch wins.
		/// </summary>
		public static bool? ParseCommandLineOverride(IReadOnlyList<string> args)
		{
			var result = (bool?)null;
			if (args == null) {
				return null;
			}
			foreach (var raw in args) {
				// --name is the documented form, -name still works
				var arg = raw.StartsWith("--", StringComparison.Ordinal) ? raw.Substring(1) : raw;
				if (string.Equals(arg, "-simulation-trace", StringComparison.OrdinalIgnoreCase)) {
					result = true;
				} else if (string.Equals(arg, "-no-simulation-trace", StringComparison.OrdinalIgnoreCase)) {
					result = false;
				}
			}
			return result;
		}

		/// <summary>
		/// What to record: <c>--simulation-trace</c> writes the full CSVs and
		/// <c>--no-simulation-trace</c> nothing. Otherwise the editor follows the component's
		/// toggle (full CSVs or nothing), and a player keeps a summary of the last minute.
		/// </summary>
		public static SimulationTraceMode ResolveMode(bool? commandLine, bool isEditor, bool componentToggle)
		{
			if (commandLine.HasValue) {
				return commandLine.Value ? SimulationTraceMode.Full : SimulationTraceMode.Off;
			}
			if (isEditor) {
				return componentToggle ? SimulationTraceMode.Full : SimulationTraceMode.Off;
			}
			return SimulationTraceMode.Summary;
		}

		/// <summary>
		/// The project's Logs folder in the editor, the persistent data path in a
		/// player, each with a SimulationTrace sub folder. Main thread only.
		/// </summary>
		public static string DefaultOutputDirectory()
		{
			var root = UnityEngine.Application.isEditor
				? Path.Combine(Directory.GetParent(UnityEngine.Application.dataPath)!.FullName, "Logs")
				: UnityEngine.Application.persistentDataPath;
			return Path.Combine(root, "SimulationTrace");
		}

		/// <summary>Current tick, simulation thread only.</summary>
		internal static TickRecord Tick;

		/// <summary>Current frame, main thread only.</summary>
		internal static FrameRecord Frame;

		internal static bool IsRecording => _recording;

		internal static long NowUsec() => VpeClock.NowUsec;

		internal static long TicksToUsec(long ticks) => VpeClock.TicksToUsec(ticks);

		internal static int ElapsedUsec(long startTicks, long endTicks)
		{
			var usec = VpeClock.ElapsedUsec(startTicks, endTicks);
			return usec > int.MaxValue ? int.MaxValue : (int)usec;
		}

		#region Lifecycle

		/// <summary>
		/// Allocates the ring buffers in native memory (outside the managed heap, so
		/// the collector never scans or moves them) and starts recording in
		/// <see cref="Mode"/>. Main thread, before the simulation thread starts.
		/// </summary>
		internal static void Begin()
		{
			if (_recording) {
				return;
			}
			if (Mode == SimulationTraceMode.Off) {
				// no summary of this run: none of an earlier one either
				Supersede(SummaryPath);
				return;
			}
			var full = Mode == SimulationTraceMode.Full;
			var seconds = full ? WindowSeconds : SummaryWindowSeconds;
			var session = new TraceSession(Mode, seconds * TicksPerSecond, seconds * FramesPerSecond, full ? LockCapacity : SummaryLockCapacity) {
				Table = LoadAttempt.Current?.Title,
				SessionId = VpeLog.SessionId,
				BeginUsec = NowUsec(),
				// this run's file, even if the log moves on before its last summary is written
				SummaryPath = SummaryPath,
			};
			// what's cached, and the file, describe an earlier run
			session.Generation = Supersede(session.SummaryPath);
			// the summary's arrays, allocated now rather than during play
			var buffers = Interlocked.Exchange(ref _spareBuffers, null) ?? new SummaryBuffers();
			buffers.Ensure(session.TickCapacity, session.FrameCapacity, session.LockCapacity);
			Interlocked.CompareExchange(ref _spareBuffers, buffers, null);
			Tick = default;
			Frame = default;
			_session = session;
			_recording = true;
			if (!full) {
				session.Timer = new Timer(_ => RefreshSummary(session, final: false), null,
					TimeSpan.FromSeconds(SummaryIntervalSeconds), TimeSpan.FromSeconds(SummaryIntervalSeconds));
			}
			Logger.Info(full
				? $"[SimulationTrace] Recording the last {WindowSeconds / 60} minutes ({session.Bytes >> 20} MB), written as CSV when the simulation stops."
				: $"[SimulationTrace] Keeping a summary of the last {SummaryWindowSeconds} s ({session.Bytes >> 20} MB).");
		}

		/// <summary>
		/// Makes whatever an earlier run summarized stale: clears the cache and deletes the file,
		/// and keeps that run from publishing. Returns the new current generation.
		/// </summary>
		private static long Supersede(string summaryPath)
		{
			lock (PublishLock) {
				var generation = Interlocked.Increment(ref _generation);
				Volatile.Write(ref _currentGeneration, generation);
				_latestSummary = null;
				Interlocked.Exchange(ref _latestSummaryTicks, 0);
				if (!string.IsNullOrEmpty(summaryPath)) {
					try {
						File.Delete(summaryPath);
					} catch (Exception e) {
						Logger.Debug(e, "[SimulationTrace] Cannot delete the old summary {Path:l}.", summaryPath);
					}
				}
				return generation;
			}
		}

		/// <summary>
		/// A new logging session started (a new play session in the editor): nothing summarized
		/// before belongs to it.
		/// </summary>
		public static void OnLogSessionStarted() => Supersede(null);

		/// <summary>
		/// Stops recording. In <see cref="SimulationTraceMode.Full"/> it writes the files and
		/// the summary, otherwise it computes the last summary on a background thread. Either
		/// way the buffers are freed afterwards. Main thread, after the simulation thread has
		/// exited.
		/// </summary>
		internal static void End()
		{
			var session = _session;
			if (!_recording || session == null) {
				return;
			}
			_recording = false;
			_session = null;
			// no periodic summary after the final one
			session.Ended = true;
			session.Timer?.Dispose();
			if (session.Mode != SimulationTraceMode.Full) {
				// nothing writes anymore; the summary reads from the rings, then frees them
				PendingEnd = Task.Run(() => {
					try {
						RefreshSummary(session, final: true);
					} finally {
						session.Free();
					}
				});
				return;
			}
			try {
				var directory = string.IsNullOrEmpty(OutputDirectory) ? "SimulationTrace" : OutputDirectory;
				Directory.CreateDirectory(directory);
				var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
				WriteTicks(session, Path.Combine(directory, $"ticks-{stamp}.csv"));
				WriteFrames(session, Path.Combine(directory, $"frames-{stamp}.csv"));
				WriteLocks(session, Path.Combine(directory, $"locks-{stamp}.csv"));
				var summary = RefreshSummary(session, final: true);
				if (summary != null) {
					File.WriteAllText(Path.Combine(directory, $"summary-{stamp}.txt"), summary, Encoding.ASCII);
				}
				DeleteOldSessions(directory, stamp);
				Logger.Info($"[SimulationTrace] Wrote {session.TickCount} ticks, {session.FrameCount} frames, {session.LockCount} lock records to {directory} (stamp {stamp}).");
			} catch (Exception e) {
				Logger.Error(e, "[SimulationTrace] Failed to write trace.");
			} finally {
				session.Free();
			}
		}

		/// <summary>
		/// Stops recording without writing or freeing anything, for the case where
		/// the simulation thread could not be joined and may still touch the buffers.
		/// The buffers are leaked; the next <see cref="Begin"/> allocates new ones.
		/// </summary>
		internal static void Abandon()
		{
			var session = _session;
			if (!_recording || session == null) {
				return;
			}
			_recording = false;
			_session = null;
			session.Ended = true;
			session.Timer?.Dispose();
		}

		/// <summary>Keeps the newest <see cref="KeptSessions"/> sessions in the folder.</summary>
		private static void DeleteOldSessions(string directory, string currentStamp)
		{
			var stamps = new List<string>();
			foreach (var file in Directory.GetFiles(directory, "summary-*.txt")) {
				var name = Path.GetFileNameWithoutExtension(file);
				stamps.Add(name.Substring("summary-".Length));
			}
			stamps.Sort(StringComparer.Ordinal);
			for (var i = 0; i < stamps.Count - KeptSessions; i++) {
				if (stamps[i] == currentStamp) {
					continue;
				}
				foreach (var prefix in new[] { "ticks-", "frames-", "locks-", "summary-" }) {
					foreach (var file in Directory.GetFiles(directory, prefix + stamps[i] + ".*")) {
						File.Delete(file);
					}
				}
			}
		}

		#endregion

		#region Writers

		// Each ring has one writer: ticks the simulation thread, frames and locks the main
		// thread. A record is published with a per-slot sequence (a seqlock), so a reader on
		// another thread can tell a complete record from one being overwritten:
		//   odd sequence, full barrier, record, even sequence (release), committed count.

		internal static unsafe void CommitTick()
		{
			var s = _session;
			if (s == null) {
				return;
			}
			var index = s.TickCount;
			var slot = (int)(index % s.TickCapacity);
			Volatile.Write(ref s.TickSeq[slot], InProgress(index));
			Interlocked.MemoryBarrier();
			s.Ticks[slot] = Tick;
			Volatile.Write(ref s.TickSeq[slot], Committed(index));
			Volatile.Write(ref s.TickCount, index + 1);
		}

		internal static void BeginFrame()
		{
			Frame = default;
		}

		internal static unsafe void CommitFrame()
		{
			var s = _session;
			if (s == null) {
				return;
			}
			var index = s.FrameCount;
			var slot = (int)(index % s.FrameCapacity);
			Volatile.Write(ref s.FrameSeq[slot], InProgress(index));
			Interlocked.MemoryBarrier();
			s.Frames[slot] = Frame;
			Volatile.Write(ref s.FrameSeq[slot], Committed(index));
			Volatile.Write(ref s.FrameCount, index + 1);
		}

		internal static unsafe void RecordLock(LockSite site, long startUsec, long acquiredUsec, long releasedUsec)
		{
			var s = _session;
			if (s == null) {
				return;
			}
			var index = s.LockCount;
			var slot = (int)(index % s.LockCapacity);
			Volatile.Write(ref s.LockSeq[slot], InProgress(index));
			Interlocked.MemoryBarrier();
			s.Locks[slot] = new LockRecord {
				StartUsec = startUsec,
				Site = site,
				WaitUsec = (int)(acquiredUsec - startUsec),
				HoldUsec = (int)(releasedUsec - acquiredUsec),
			};
			Volatile.Write(ref s.LockSeq[slot], Committed(index));
			Volatile.Write(ref s.LockCount, index + 1);
		}

		internal static LockScope Lock(object lockObject, LockSite site) => new(lockObject, site);

		// 0 is a slot never written
		private static long Committed(long index) => (index + 1) * 2;
		private static long InProgress(long index) => (index + 1) * 2 - 1;

		/// <summary>
		/// Copies the record of a given index from a ring, if it's complete and not lapped.
		/// </summary>
		internal static unsafe bool TryRead<T>(T* ring, long* seq, int capacity, long index, out T record) where T : unmanaged
		{
			var slot = (int)(index % capacity);
			var before = Volatile.Read(ref seq[slot]);
			if (before != Committed(index)) {
				record = default;
				return false;
			}
			record = ring[slot];
			Interlocked.MemoryBarrier();
			return Volatile.Read(ref seq[slot]) == before;
		}

		#endregion

		#region Summary

		/// <summary>
		/// Summarizes the running session now, as the background timer would. For tests.
		/// </summary>
		internal static string SummarizeNow()
		{
			var session = _session;
			return session == null ? null : RefreshSummary(session, final: false);
		}

		/// <summary>The running session, for tests.</summary>
		internal static object CurrentSession => _session;

		/// <summary>A periodic summary of a given session, as a late timer would run it. For tests.</summary>
		internal static string SummarizeLate(object session) => RefreshSummary((TraceSession)session, final: false);

		/// <summary>
		/// Computes a summary from the rings and publishes it, unless another one is being
		/// computed or the rings are gone. Any thread.
		/// </summary>
		private static string RefreshSummary(TraceSession session, bool final)
		{
			if (!final && session.Ended) {
				return null;
			}
			// the session's lock keeps End from freeing the rings while they're read, and keeps a
			// periodic summary from publishing over the final one
			if (!Monitor.TryEnter(session.ReaderLock, final ? Timeout.Infinite : 0)) {
				return null;
			}
			try {
				if (session.Freed || session.FinalPublished || !final && session.Ended) {
					return null;
				}
				string summary;
				var buffers = Interlocked.Exchange(ref _spareBuffers, null) ?? new SummaryBuffers();
				try {
					summary = Summarize(session, buffers, final);
				} finally {
					Interlocked.CompareExchange(ref _spareBuffers, buffers, null);
				}
				if (final) {
					session.FinalPublished = true;
				}
				BeforePublishForTests?.Invoke();
				Publish(session, summary);
				return summary;

			} catch (Exception e) {
				Logger.Warn(e, "[SimulationTrace] Cannot summarize the trace.");
				return null;
			} finally {
				Monitor.Exit(session.ReaderLock);
			}
		}

		/// <summary>
		/// Writes the summary to the run's file, caches and announces it, unless a newer run (or
		/// logging session) started meanwhile. Under the session's lock, and the publish lock, so
		/// it can't interleave with a run change.
		/// </summary>
		private static void Publish(TraceSession session, string summary)
		{
			lock (PublishLock) {
				if (session.Generation != Volatile.Read(ref _currentGeneration)) {
					return;
				}
				if (!string.IsNullOrEmpty(session.SummaryPath)) {
					try {
						File.WriteAllText(session.SummaryPath, summary, Encoding.ASCII);
					} catch (Exception e) {
						Logger.Debug(e, "[SimulationTrace] Cannot write the summary to {Path:l}.", session.SummaryPath);
					}
				}
				_latestSummary = summary;
				Interlocked.Exchange(ref _latestSummaryTicks, DateTime.UtcNow.Ticks);
				try {
					SummaryUpdated?.Invoke(summary);
				} catch (Exception e) {
					Logger.Warn(e, "[SimulationTrace] A summary handler failed.");
				}
			}
		}

		/// <summary>
		/// Runs between computing and publishing a summary. For tests that race the two.
		/// </summary>
		internal static Action BeforePublishForTests;

		/// <summary>
		/// Reusable arrays for the summary, so computing one every minute doesn't add up to
		/// garbage. Grown on demand, kept across sessions.
		/// </summary>
		private sealed class SummaryBuffers
		{
			public int[] Total = Array.Empty<int>(), Execute = Array.Empty<int>(), LockWait = Array.Empty<int>(),
				SnapshotLock = Array.Empty<int>(), Late = Array.Empty<int>(), Gap = Array.Empty<int>();
			public int[] FrameDelta = Array.Empty<int>(), SnapshotAge = Array.Empty<int>(), Update = Array.Empty<int>();
			public int[] Waits = Array.Empty<int>(), Holds = Array.Empty<int>();
			public readonly TickRecord[] SlowTicks = new TickRecord[TopCount];
			public readonly TickRecord[] GapTicks = new TickRecord[TopCount];
			public readonly TickRecord[] GapPrevious = new TickRecord[TopCount];
			public readonly StringBuilder Text = new(32 * 1024);

			public void Ensure(int ticks, int frames, int locks)
			{
				if (Total.Length < ticks) {
					Total = new int[ticks];
					Execute = new int[ticks];
					LockWait = new int[ticks];
					SnapshotLock = new int[ticks];
					Late = new int[ticks];
					Gap = new int[ticks];
				}
				if (FrameDelta.Length < frames) {
					FrameDelta = new int[frames];
					SnapshotAge = new int[frames];
					Update = new int[frames];
				}
				if (Waits.Length < locks) {
					Waits = new int[locks];
					Holds = new int[locks];
				}
			}
		}

		private const int TopCount = 40;

		private static unsafe string Summarize(TraceSession s, SummaryBuffers b, bool final)
		{
			b.Ensure(s.TickCapacity, s.FrameCapacity, s.LockCapacity);
			var w = b.Text;
			w.Clear();

			// ticks: copy what's complete, newest window only
			var tickEnd = Volatile.Read(ref s.TickCount);
			var tickStart = math.max(0, tickEnd - s.TickCapacity);
			var n = 0;
			var slowCount = 0;
			var gapCount = 0;
			long droppedTotal = 0, jumpTotal = 0, physicsTotal = 0, gaps5 = 0, gaps20 = 0, gaps100 = 0, multiStep = 0, rebuilds = 0, torn = 0;
			TickRecord first = default, last = default, previous = default;
			var havePrevious = false;
			for (var index = tickStart; index < tickEnd; index++) {
				if (!TryRead(s.Ticks, s.TickSeq, s.TickCapacity, index, out TickRecord t)) {
					torn++;
					havePrevious = false;
					continue;
				}
				if (n == 0) {
					first = t;
				}
				last = t;
				b.Total[n] = t.TotalUsec;
				b.Execute[n] = t.ExecuteUsec;
				b.LockWait[n] = t.LockWaitUsec;
				b.SnapshotLock[n] = t.SnapshotLockUsec;
				b.Late[n] = (int)(t.StartUsec - t.TargetUsec);
				b.Gap[n] = t.WaitUsec;
				n++;
				droppedTotal += t.DroppedUsec;
				jumpTotal += t.ClockJumpUsec;
				physicsTotal += t.PhysicsUsec;
				if (t.WaitUsec > 5_000) gaps5++;
				if (t.WaitUsec > 20_000) gaps20++;
				if (t.WaitUsec > 100_000) gaps100++;
				if (t.PhysicsAdvanceUsec > 1_000) multiStep++;
				if (t.RebuildUsec > 0) rebuilds++;
				InsertTop(b.SlowTicks, null, ref slowCount, t, default, r => r.TotalUsec);
				InsertTop(b.GapTicks, b.GapPrevious, ref gapCount, t, havePrevious ? previous : default, r => r.WaitUsec);
				previous = t;
				havePrevious = true;
			}

			w.Append("simulation trace summary").Append(final ? " (final)" : "").Append(", ")
				.AppendUtc(DateTime.UtcNow).Append("Z, session ").Append(s.SessionId ?? "n/a")
				.Append(", table ").Append(s.Table ?? "n/a").Append(", window ").AppendInt(s.TickCapacity / TicksPerSecond).AppendLine(" s");
			w.Append("ticks recorded: ").AppendInt(tickEnd).Append(" (summarized ").AppendInt(n).Append(", torn ").AppendInt(torn)
				.Append("), frames: ").AppendInt(Volatile.Read(ref s.FrameCount)).Append(", locks: ").AppendInt(Volatile.Read(ref s.LockCount)).AppendLine();
			if (n == 0) {
				return w.ToString();
			}

			var wallUsec = last.StartUsec - first.StartUsec;
			var simUsec = last.SimTimeUsec - first.SimTimeUsec;
			w.Append("wall time: ").AppendInt(wallUsec / 1000).Append(" ms, sim time advanced: ").AppendInt(simUsec / 1000)
				.Append(" ms, ticks/s: ").AppendFixed1(wallUsec > 0 ? n * 1_000_000.0 / wallUsec : 0).AppendLine();
			w.Append("sim thread busy: ").AppendFixed1(physicsTotal * 100.0 / math.max(1, wallUsec))
				.Append("% in physics, dropped backlog total: ").AppendInt(droppedTotal / 1000).Append(" ms, clock jumps total: ").AppendInt(jumpTotal / 1000).AppendLine(" ms");
			w.Append("ticks with >1 sub step: ").AppendInt(multiStep).Append(", octree rebuilds: ").AppendInt(rebuilds)
				.Append(", gc0 collections during run: ").AppendInt(last.Gc0 - first.Gc0).AppendLine();
			w.Append("wait gaps >5 ms: ").AppendInt(gaps5).Append(", >20 ms: ").AppendInt(gaps20).Append(", >100 ms: ").AppendInt(gaps100).AppendLine();
			AppendPercentiles(w, "tick total us", b.Total, n);
			AppendPercentiles(w, "execute us", b.Execute, n);
			AppendPercentiles(w, "lock wait us", b.LockWait, n);
			AppendPercentiles(w, "snapshot lock wait us", b.SnapshotLock, n);
			AppendPercentiles(w, "lateness us", b.Late, n);
			AppendPercentiles(w, "wait gap us", b.Gap, n);

			// largest gaps between consecutive ticks, with what preceded them
			w.AppendLine();
			w.AppendLine("largest wait gaps (gap = end of previous tick to start of this tick):");
			w.AppendLine("  start_ms_rel  gap_us  requested_us  mode  gc0_delta  prev_total_us  prev_physics_us  prev_snapshot_us  prev_lock_wait_us  clock_jump_us  dropped_us  balls");
			for (var k = 0; k < gapCount; k++) {
				ref var cur = ref b.GapTicks[k];
				ref var prev = ref b.GapPrevious[k];
				w.Append("  ").AppendPadded((cur.StartUsec - s.BeginUsec) / 1000, 12).Append("  ").AppendPadded(cur.WaitUsec, 6).Append("  ")
					.AppendPadded(cur.WaitRequestedUsec, 12).Append("  ").AppendPadded(cur.WaitMode, 4).Append("  ")
					.AppendPadded(prev.Index > 0 ? cur.Gc0 - prev.Gc0 : 0, 9).Append("  ").AppendPadded(prev.TotalUsec, 13).Append("  ")
					.AppendPadded(prev.PhysicsUsec, 15).Append("  ").AppendPadded(prev.SnapshotUsec, 16).Append("  ")
					.AppendPadded(prev.LockWaitUsec, 17).Append("  ").AppendPadded(cur.ClockJumpUsec, 13).Append("  ")
					.AppendPadded(cur.DroppedUsec, 10).Append("  ").AppendPadded(cur.BallCount, 5).AppendLine();
			}

			// slowest ticks
			w.AppendLine();
			w.AppendLine("slowest ticks:");
			w.AppendLine("  start_ms_rel  total_us  lock_wait_us  kinematic_us  rebuild_us  execute_us  advance_us  plumb_us  fence_us  writer_us  diag_us  snapshot_lock_us  snapshot_us  switches_us  input_us  outputs_us  balls  iterations  hit_tests  ball_tests  contacts  bp_visits");
			for (var k = 0; k < slowCount; k++) {
				ref var cur = ref b.SlowTicks[k];
				ref var c = ref cur.Counters;
				w.Append("  ").AppendPadded((cur.StartUsec - s.BeginUsec) / 1000, 12).Append("  ").AppendPadded(cur.TotalUsec, 8).Append("  ")
					.AppendPadded(cur.LockWaitUsec, 12).Append("  ").AppendPadded(cur.KinematicUsec, 12).Append("  ").AppendPadded(cur.RebuildUsec, 10).Append("  ")
					.AppendPadded(cur.ExecuteUsec, 10).Append("  ").AppendPadded(cur.PhysicsAdvanceUsec, 10).Append("  ").AppendPadded(cur.PlumbUsec, 8).Append("  ")
					.AppendPadded(cur.FenceUsec, 8).Append("  ").AppendPadded(cur.WriterUsec, 9).Append("  ").AppendPadded(cur.DiagUsec, 7).Append("  ")
					.AppendPadded(cur.SnapshotLockUsec, 16).Append("  ").AppendPadded(cur.SnapshotUsec, 11).Append("  ").AppendPadded(cur.SwitchesUsec, 11).Append("  ")
					.AppendPadded(cur.InputUsec, 8).Append("  ").AppendPadded(cur.OutputsUsec, 10).Append("  ").AppendPadded(cur.BallCount, 5).Append("  ")
					.AppendPadded(c.Iterations, 10).Append("  ").AppendPadded(c.HitTests, 9).Append("  ").AppendPadded(c.BallTests, 10).Append("  ")
					.AppendPadded(c.Contacts, 8).Append("  ").AppendPadded(c.BroadPhaseVisits, 9).AppendLine();
			}

			// frames
			var frameEnd = Volatile.Read(ref s.FrameCount);
			var fn = 0;
			long staleSnapshots = 0, drainSkipped = 0, lastPublish = -1;
			for (var index = math.max(0, frameEnd - s.FrameCapacity); index < frameEnd; index++) {
				if (!TryRead(s.Frames, s.FrameSeq, s.FrameCapacity, index, out FrameRecord f)) {
					lastPublish = -1;
					continue;
				}
				b.FrameDelta[fn] = f.UnscaledDeltaUsec;
				b.SnapshotAge[fn] = f.SnapshotPublishUsec > 0 ? (int)(f.StartUsec - f.SnapshotPublishUsec) : 0;
				b.Update[fn] = f.UpdateUsec;
				fn++;
				if (f.SnapshotPublishUsec == lastPublish) staleSnapshots++;
				lastPublish = f.SnapshotPublishUsec;
				drainSkipped += f.DrainSkipped;
			}
			if (fn > 0) {
				w.AppendLine();
				w.Append("frames: ").AppendInt(fn).Append(", frames that re-rendered the previous snapshot: ").AppendInt(staleSnapshots)
					.Append(", event drains skipped: ").AppendInt(drainSkipped).AppendLine();
				AppendPercentiles(w, "frame delta us", b.FrameDelta, fn);
				AppendPercentiles(w, "snapshot age us", b.SnapshotAge, fn);
				AppendPercentiles(w, "physics update us", b.Update, fn);
			}

			// locks
			var lockEnd = Volatile.Read(ref s.LockCount);
			var ln = 0;
			for (var index = math.max(0, lockEnd - s.LockCapacity); index < lockEnd; index++) {
				if (!TryRead(s.Locks, s.LockSeq, s.LockCapacity, index, out LockRecord l)) {
					continue;
				}
				b.Waits[ln] = l.WaitUsec;
				b.Holds[ln] = l.HoldUsec;
				ln++;
			}
			if (ln > 0) {
				w.AppendLine();
				w.Append("main-thread physics lock acquisitions: ").AppendInt(ln).AppendLine();
				AppendPercentiles(w, "main lock wait us", b.Waits, ln);
				AppendPercentiles(w, "main lock hold us", b.Holds, ln);
			}
			return w.ToString();
		}

		/// <summary>
		/// Keeps the <see cref="TopCount"/> largest records by a key, largest first.
		/// </summary>
		private static void InsertTop(TickRecord[] top, TickRecord[] companions, ref int count, in TickRecord record, in TickRecord companion, Func<TickRecord, int> key)
		{
			var value = key(record);
			if (count == top.Length && value <= key(top[count - 1])) {
				return;
			}
			var at = count < top.Length ? count++ : count - 1;
			while (at > 0 && key(top[at - 1]) < value) {
				top[at] = top[at - 1];
				if (companions != null) {
					companions[at] = companions[at - 1];
				}
				at--;
			}
			top[at] = record;
			if (companions != null) {
				companions[at] = companion;
			}
		}

		/// <summary>
		/// Sorts the first <paramref name="count"/> values and appends their percentiles.
		/// </summary>
		private static void AppendPercentiles(StringBuilder w, string label, int[] values, int count)
		{
			Array.Sort(values, 0, count);
			int P(double p) => values[math.clamp((int)(p * (count - 1)), 0, count - 1)];
			w.Append(label).Append(' ', System.Math.Max(0, 24 - label.Length)).Append(" p50=").AppendPadded(P(0.5), 8).Append(" p90=").AppendPadded(P(0.9), 8)
				.Append(" p99=").AppendPadded(P(0.99), 8).Append(" p99.9=").AppendPadded(P(0.999), 8).Append(" max=").AppendPadded(values[count - 1], 8).AppendLine();
		}

		// StringBuilder.Append(long) allocates a string on Mono; these don't

		private static StringBuilder AppendInt(this StringBuilder sb, long value)
		{
			if (value < 0) {
				sb.Append('-');
				if (value == long.MinValue) {
					return sb.Append("9223372036854775808");
				}
				value = -value;
			}
			var divisor = 1L;
			while (value / divisor >= 10) {
				divisor *= 10;
			}
			for (; divisor > 0; divisor /= 10) {
				sb.Append((char)('0' + value / divisor % 10));
			}
			return sb;
		}

		private static StringBuilder AppendPadded(this StringBuilder sb, long value, int width)
		{
			var length = value < 0 ? 2 : 1;
			for (var rest = value < 0 ? -(value / 10) : value / 10; rest > 0; rest /= 10) {
				length++;
			}
			if (length < width) {
				sb.Append(' ', width - length);
			}
			return sb.AppendInt(value);
		}

		private static StringBuilder AppendFixed1(this StringBuilder sb, double value)
		{
			var tenths = (long)System.Math.Round(value * 10);
			if (tenths < 0) {
				sb.Append('-');
				tenths = -tenths;
			}
			return sb.AppendInt(tenths / 10).Append('.').Append((char)('0' + tenths % 10));
		}

		private static StringBuilder AppendUtc(this StringBuilder sb, DateTime time)
		{
			return sb.AppendTwo(time.Year / 100).AppendTwo(time.Year % 100).Append('-').AppendTwo(time.Month).Append('-').AppendTwo(time.Day)
				.Append(' ').AppendTwo(time.Hour).Append(':').AppendTwo(time.Minute).Append(':').AppendTwo(time.Second);
		}

		private static StringBuilder AppendTwo(this StringBuilder sb, int value) => sb.Append((char)('0' + value / 10 % 10)).Append((char)('0' + value % 10));

		#endregion

		#region Session

		/// <summary>
		/// The rings of one simulation run. Freed by whoever ends the run, never while a summary
		/// reads them (<see cref="ReaderLock"/>).
		/// </summary>
		private sealed unsafe class TraceSession
		{
			public readonly SimulationTraceMode Mode;
			public readonly int TickCapacity, FrameCapacity, LockCapacity;
			public readonly TickRecord* Ticks;
			public readonly FrameRecord* Frames;
			public readonly LockRecord* Locks;
			public readonly long* TickSeq, FrameSeq, LockSeq;
			public long TickCount, FrameCount, LockCount;
			public readonly object ReaderLock = new();
			public bool Freed;
			public volatile bool Ended;
			public bool FinalPublished;
			public long Generation;
			public string SummaryPath;
			public Timer Timer;
			public string Table;
			public string SessionId;
			public long BeginUsec;

			public long Bytes => (long)TickCapacity * (sizeof(TickRecord) + sizeof(long))
				+ (long)FrameCapacity * (sizeof(FrameRecord) + sizeof(long))
				+ (long)LockCapacity * (sizeof(LockRecord) + sizeof(long));

			public TraceSession(SimulationTraceMode mode, int ticks, int frames, int locks)
			{
				Mode = mode;
				TickCapacity = ticks;
				FrameCapacity = frames;
				LockCapacity = locks;
				Ticks = Alloc<TickRecord>(ticks);
				Frames = Alloc<FrameRecord>(frames);
				Locks = Alloc<LockRecord>(locks);
				TickSeq = Alloc<long>(ticks);
				FrameSeq = Alloc<long>(frames);
				LockSeq = Alloc<long>(locks);
				// a zero sequence is a slot never written
				UnsafeUtility.MemClear(TickSeq, (long)ticks * sizeof(long));
				UnsafeUtility.MemClear(FrameSeq, (long)frames * sizeof(long));
				UnsafeUtility.MemClear(LockSeq, (long)locks * sizeof(long));
			}

			public void Free()
			{
				lock (ReaderLock) {
					if (Freed) {
						return;
					}
					Freed = true;
					UnsafeUtility.Free(Ticks, Allocator.Persistent);
					UnsafeUtility.Free(Frames, Allocator.Persistent);
					UnsafeUtility.Free(Locks, Allocator.Persistent);
					UnsafeUtility.Free(TickSeq, Allocator.Persistent);
					UnsafeUtility.Free(FrameSeq, Allocator.Persistent);
					UnsafeUtility.Free(LockSeq, Allocator.Persistent);
				}
			}

			private static T* Alloc<T>(int count) where T : unmanaged
				=> (T*)UnsafeUtility.Malloc((long)count * sizeof(T), UnsafeUtility.AlignOf<T>(), Allocator.Persistent);
		}

		#endregion

		#region CSV

		private static unsafe void WriteTicks(TraceSession s, string path)
		{
			using var writer = new StreamWriter(path, false, Encoding.ASCII, 1 << 16);
			writer.WriteLine("index,start_us,target_us,late_us,sim_time_us,synced_clock_us,physics_time_us,clock_jump_us,dropped_us,wait_requested_us,wait_us,wait_mode,switches_us,input_us,outputs_us,lock_wait_us,kinematic_us,rebuild_us,execute_us,physics_advance_us,physics_us,plumb_us,fence_us,writer_us,diag_us,snapshot_lock_us,snapshot_us,total_us,balls,kinematic_updates,moving_items,gc0,iterations,hit_tests,ball_tests,contacts,max_ball_tests,max_ball_id,max_ball_x,max_ball_y,max_ball_z,tests_triangle,tests_line3d,tests_line,tests_point,tests_plane,tests_circle,tests_flipper,tests_other,ball_octree_refits,broad_phase_visits,deepest_dist,deepest_collider,deepest_item,deepest_type,deepest_contact,deepest_ball,deepest_nx,deepest_ny,deepest_nz,deepest_bx,deepest_by,deepest_bz,deepest_vx,deepest_vy,deepest_vz");
			var sb = new StringBuilder(512);
			for (var index = math.max(0, s.TickCount - s.TickCapacity); index < s.TickCount; index++) {
				ref var t = ref s.Ticks[index % s.TickCapacity];
				sb.Clear();
				sb.Append(t.Index).Append(',').Append(t.StartUsec).Append(',').Append(t.TargetUsec).Append(',')
					.Append(t.StartUsec - t.TargetUsec).Append(',')
					.Append(t.SimTimeUsec).Append(',').Append(t.SyncedClockUsec).Append(',').Append(t.PhysicsTimeUsec).Append(',')
					.Append(t.ClockJumpUsec).Append(',').Append(t.DroppedUsec).Append(',')
					.Append(t.WaitRequestedUsec).Append(',').Append(t.WaitUsec).Append(',').Append(t.WaitMode).Append(',')
					.Append(t.SwitchesUsec).Append(',').Append(t.InputUsec).Append(',').Append(t.OutputsUsec).Append(',')
					.Append(t.LockWaitUsec).Append(',').Append(t.KinematicUsec).Append(',').Append(t.RebuildUsec).Append(',')
					.Append(t.ExecuteUsec).Append(',').Append(t.PhysicsAdvanceUsec).Append(',').Append(t.PhysicsUsec).Append(',')
					.Append(t.PlumbUsec).Append(',').Append(t.FenceUsec).Append(',').Append(t.WriterUsec).Append(',').Append(t.DiagUsec).Append(',')
					.Append(t.SnapshotLockUsec).Append(',').Append(t.SnapshotUsec).Append(',').Append(t.TotalUsec).Append(',')
					.Append(t.BallCount).Append(',').Append(t.KinematicUpdates).Append(',').Append(t.MovingItems).Append(',').Append(t.Gc0);
				ref var c = ref t.Counters;
				sb.Append(',').Append(c.Iterations).Append(',').Append(c.HitTests).Append(',').Append(c.BallTests).Append(',').Append(c.Contacts).Append(',')
					.Append(c.MaxBallHitTests).Append(',').Append(c.MaxBallId).Append(',')
					.Append(c.MaxBallPosition.x.ToString("F1", CultureInfo.InvariantCulture)).Append(',')
					.Append(c.MaxBallPosition.y.ToString("F1", CultureInfo.InvariantCulture)).Append(',')
					.Append(c.MaxBallPosition.z.ToString("F1", CultureInfo.InvariantCulture)).Append(',')
					.Append(c.Triangle).Append(',').Append(c.Line3D).Append(',').Append(c.Line).Append(',').Append(c.Point).Append(',')
					.Append(c.Plane).Append(',').Append(c.Circle).Append(',').Append(c.Flipper).Append(',').Append(c.Other).Append(',')
					.Append(t.BallOctreeRefits).Append(',').Append(c.BroadPhaseVisits).Append(',')
					.Append(c.DeepestHitDistance == float.MaxValue ? "" : c.DeepestHitDistance.ToString("F3", CultureInfo.InvariantCulture)).Append(',')
					.Append(c.DeepestHitColliderId).Append(',').Append(c.DeepestHitItemId).Append(',').Append(c.DeepestHitType).Append(',')
					.Append(c.DeepestHitIsContact).Append(',').Append(c.DeepestHitBallId).Append(',')
					.Append(c.DeepestHitNormal.x.ToString("F3", CultureInfo.InvariantCulture)).Append(',')
					.Append(c.DeepestHitNormal.y.ToString("F3", CultureInfo.InvariantCulture)).Append(',')
					.Append(c.DeepestHitNormal.z.ToString("F3", CultureInfo.InvariantCulture)).Append(',')
					.Append(c.DeepestHitBallPosition.x.ToString("F2", CultureInfo.InvariantCulture)).Append(',')
					.Append(c.DeepestHitBallPosition.y.ToString("F2", CultureInfo.InvariantCulture)).Append(',')
					.Append(c.DeepestHitBallPosition.z.ToString("F2", CultureInfo.InvariantCulture)).Append(',')
					.Append(c.DeepestHitBallVelocity.x.ToString("F3", CultureInfo.InvariantCulture)).Append(',')
					.Append(c.DeepestHitBallVelocity.y.ToString("F3", CultureInfo.InvariantCulture)).Append(',')
					.Append(c.DeepestHitBallVelocity.z.ToString("F3", CultureInfo.InvariantCulture));
				writer.WriteLine(sb.ToString());
			}
		}

		private static unsafe void WriteFrames(TraceSession s, string path)
		{
			using var writer = new StreamWriter(path, false, Encoding.ASCII, 1 << 16);
			writer.WriteLine("frame,start_us,unity_time_us,realtime_us,unscaled_delta_us,delta_us,time_scale,gc0,snapshot_sim_time_us,snapshot_publish_us,snapshot_age_us,snapshot_balls,drain_us,drain_skipped,events_drained,actions_drained,scan_us,kinematic_changed,kinematic_stopped,apply_us,update_us,ball0_id,ball0_x,ball0_y,ball0_z,ball1_id,ball1_x,ball1_y,ball1_z,ball2_id,ball2_x,ball2_y,ball2_z,ball3_id,ball3_x,ball3_y,ball3_z");
			var sb = new StringBuilder(512);
			for (var index = math.max(0, s.FrameCount - s.FrameCapacity); index < s.FrameCount; index++) {
				ref var f = ref s.Frames[index % s.FrameCapacity];
				sb.Clear();
				sb.Append(f.Frame).Append(',').Append(f.StartUsec).Append(',').Append(f.UnityTimeUsec).Append(',').Append(f.RealtimeUsec).Append(',')
					.Append(f.UnscaledDeltaUsec).Append(',').Append(f.DeltaUsec).Append(',').Append(f.TimeScale.ToString("R", CultureInfo.InvariantCulture)).Append(',')
					.Append(f.Gc0).Append(',').Append(f.SnapshotSimTimeUsec).Append(',').Append(f.SnapshotPublishUsec).Append(',')
					.Append(f.SnapshotPublishUsec > 0 ? f.StartUsec - f.SnapshotPublishUsec : -1).Append(',').Append(f.SnapshotBallCount).Append(',')
					.Append(f.DrainUsec).Append(',').Append(f.DrainSkipped).Append(',').Append(f.EventsDrained).Append(',').Append(f.ActionsDrained).Append(',')
					.Append(f.ScanUsec).Append(',').Append(f.KinematicChanged).Append(',').Append(f.KinematicStopped).Append(',')
					.Append(f.ApplyUsec).Append(',').Append(f.UpdateUsec);
				AppendBall(sb, f.Ball0Id, f.Ball0);
				AppendBall(sb, f.Ball1Id, f.Ball1);
				AppendBall(sb, f.Ball2Id, f.Ball2);
				AppendBall(sb, f.Ball3Id, f.Ball3);
				writer.WriteLine(sb.ToString());
			}
		}

		private static void AppendBall(StringBuilder sb, int id, float3 position)
		{
			sb.Append(',').Append(id).Append(',')
				.Append(position.x.ToString("F3", CultureInfo.InvariantCulture)).Append(',')
				.Append(position.y.ToString("F3", CultureInfo.InvariantCulture)).Append(',')
				.Append(position.z.ToString("F3", CultureInfo.InvariantCulture));
		}

		private static unsafe void WriteLocks(TraceSession s, string path)
		{
			using var writer = new StreamWriter(path, false, Encoding.ASCII, 1 << 16);
			writer.WriteLine("start_us,site,wait_us,hold_us");
			for (var index = math.max(0, s.LockCount - s.LockCapacity); index < s.LockCount; index++) {
				ref var l = ref s.Locks[index % s.LockCapacity];
				writer.WriteLine($"{l.StartUsec},{l.Site},{l.WaitUsec},{l.HoldUsec}");
			}
		}

		#endregion
	}

	/// <summary>
	/// What <see cref="SimulationTrace"/> records.
	/// </summary>
	public enum SimulationTraceMode
	{
		/// <summary>Nothing.</summary>
		Off,
		/// <summary>The last minute, summarized every minute and when the simulation stops.</summary>
		Summary,
		/// <summary>The last ten minutes, written as CSV files and a summary when the simulation stops.</summary>
		Full,
	}
}
