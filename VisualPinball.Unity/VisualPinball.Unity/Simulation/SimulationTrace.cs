// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// https://github.com/freezy/VisualPinball.Engine
//
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
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
	/// preallocated ring buffers, and writes them as CSV (plus a summary) when the
	/// simulation thread stops.
	/// </summary>
	/// <remarks>
	/// Inactive unless <see cref="Enabled"/> is set before the simulation thread
	/// starts. While recording, the hot path costs a handful of timestamp reads per
	/// tick. All timestamps are Stopwatch-based microseconds so tick, frame and lock
	/// records share one clock.
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

		private static readonly double TicksToUsecFactor = 1e6 / Stopwatch.Frequency;

		/// <summary>
		/// Whether the next simulation run is recorded. Set by
		/// <see cref="SimulationThreadComponent"/> from its inspector toggle, unless
		/// <see cref="CommandLineOverride"/> says otherwise.
		/// </summary>
		public static bool Enabled { get; set; }

		/// <summary>Where the files go. See <see cref="DefaultOutputDirectory"/>.</summary>
		public static string OutputDirectory { get; set; }

		/// <summary>
		/// Length of the recorded window. Older records are overwritten, so a long
		/// session keeps its last minutes; memory and per-tick cost do not depend on
		/// the session length.
		/// </summary>
		public const int WindowSeconds = 600;

		/// <summary>Sessions kept in the output folder; older files are deleted when a new one is written.</summary>
		public const int KeptSessions = 3;

		private const int TickCapacity = WindowSeconds * 1000;  // 1 kHz
		private const int FrameCapacity = WindowSeconds * 240;  // up to 240 fps
		private const int LockCapacity = 1 << 16;

		private static volatile bool _recording;
		private static unsafe TickRecord* _ticks;
		private static unsafe FrameRecord* _frames;
		private static unsafe LockRecord* _locks;
		private static int _tickHead, _frameHead, _lockHead;
		private static long _tickCount, _frameCount, _lockCount;
		private static long _beginUsec;

		/// <summary>
		/// Command-line override for player builds: <c>-simulation-trace</c> records,
		/// <c>-no-simulation-trace</c> does not. Null when neither is given.
		/// </summary>
		public static bool? CommandLineOverride()
		{
			var result = (bool?)null;
			foreach (var arg in Environment.GetCommandLineArgs()) {
				if (string.Equals(arg, "-simulation-trace", StringComparison.OrdinalIgnoreCase)) {
					result = true;
				} else if (string.Equals(arg, "-no-simulation-trace", StringComparison.OrdinalIgnoreCase)) {
					result = false;
				}
			}
			return result;
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

		internal static long NowUsec() => (long)(Stopwatch.GetTimestamp() * TicksToUsecFactor);

		internal static long TicksToUsec(long ticks) => (long)(ticks * TicksToUsecFactor);

		internal static int ElapsedUsec(long startTicks, long endTicks)
		{
			var elapsed = endTicks - startTicks;
			return elapsed <= 0 ? 0 : (int)(elapsed * TicksToUsecFactor);
		}

		/// <summary>
		/// Allocates the ring buffers in native memory (outside the managed heap, so
		/// the collector never scans or moves them) and starts recording. Main
		/// thread, before the simulation thread starts.
		/// </summary>
		internal static unsafe void Begin()
		{
			if (_recording) {
				return;
			}
			_ticks = (TickRecord*)UnsafeUtility.Malloc((long)TickCapacity * sizeof(TickRecord), UnsafeUtility.AlignOf<TickRecord>(), Allocator.Persistent);
			_frames = (FrameRecord*)UnsafeUtility.Malloc((long)FrameCapacity * sizeof(FrameRecord), UnsafeUtility.AlignOf<FrameRecord>(), Allocator.Persistent);
			_locks = (LockRecord*)UnsafeUtility.Malloc((long)LockCapacity * sizeof(LockRecord), UnsafeUtility.AlignOf<LockRecord>(), Allocator.Persistent);
			_tickHead = _frameHead = _lockHead = 0;
			_tickCount = _frameCount = _lockCount = 0;
			Tick = default;
			Frame = default;
			_beginUsec = NowUsec();
			_recording = true;
			Logger.Info($"[SimulationTrace] Recording the last {WindowSeconds / 60} minutes ({(TickCapacity * sizeof(TickRecord) + FrameCapacity * sizeof(FrameRecord)) >> 20} MB).");
		}

		/// <summary>
		/// Stops recording, writes the files and frees the buffers. Main thread,
		/// after the simulation thread has exited.
		/// </summary>
		internal static unsafe void End()
		{
			if (!_recording) {
				return;
			}
			_recording = false;
			try {
				var directory = string.IsNullOrEmpty(OutputDirectory) ? "SimulationTrace" : OutputDirectory;
				Directory.CreateDirectory(directory);
				var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
				WriteTicks(Path.Combine(directory, $"ticks-{stamp}.csv"));
				WriteFrames(Path.Combine(directory, $"frames-{stamp}.csv"));
				WriteLocks(Path.Combine(directory, $"locks-{stamp}.csv"));
				WriteSummary(Path.Combine(directory, $"summary-{stamp}.txt"));
				DeleteOldSessions(directory, stamp);
				Logger.Info($"[SimulationTrace] Wrote {_tickCount} ticks, {_frameCount} frames, {_lockCount} lock records to {directory} (stamp {stamp}).");
				UnityEngine.Debug.Log($"[SimulationTrace] Wrote trace to {directory} (stamp {stamp}).");
			} catch (Exception e) {
				Logger.Error(e, "[SimulationTrace] Failed to write trace.");
			} finally {
				UnsafeUtility.Free(_ticks, Allocator.Persistent);
				UnsafeUtility.Free(_frames, Allocator.Persistent);
				UnsafeUtility.Free(_locks, Allocator.Persistent);
				_ticks = null;
				_frames = null;
				_locks = null;
			}
		}

		/// <summary>Keeps the newest <see cref="KeptSessions"/> sessions in the folder.</summary>
		private static void DeleteOldSessions(string directory, string currentStamp)
		{
			var stamps = new System.Collections.Generic.List<string>();
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

		internal static unsafe void CommitTick()
		{
			if (!_recording) {
				return;
			}
			_ticks[_tickHead] = Tick;
			_tickHead = (_tickHead + 1) % TickCapacity;
			_tickCount++;
		}

		internal static void BeginFrame()
		{
			Frame = default;
		}

		internal static unsafe void CommitFrame()
		{
			if (!_recording) {
				return;
			}
			_frames[_frameHead] = Frame;
			_frameHead = (_frameHead + 1) % FrameCapacity;
			_frameCount++;
		}

		internal static unsafe void RecordLock(LockSite site, long startUsec, long acquiredUsec, long releasedUsec)
		{
			if (!_recording) {
				return;
			}
			_locks[_lockHead] = new LockRecord {
				StartUsec = startUsec,
				Site = site,
				WaitUsec = (int)(acquiredUsec - startUsec),
				HoldUsec = (int)(releasedUsec - acquiredUsec),
			};
			_lockHead = (_lockHead + 1) % LockCapacity;
			_lockCount++;
		}

		internal static LockScope Lock(object lockObject, LockSite site) => new(lockObject, site);

		#region Output

		private static int StoredCount(long count, int capacity) => (int)math.min(count, capacity);

		private static int FirstIndex(long count, int capacity, int head) => count < capacity ? 0 : head;

		private static unsafe void WriteTicks(string path)
		{
			using var writer = new StreamWriter(path, false, Encoding.ASCII, 1 << 16);
			writer.WriteLine("index,start_us,target_us,late_us,sim_time_us,synced_clock_us,physics_time_us,clock_jump_us,dropped_us,wait_requested_us,wait_us,wait_mode,switches_us,input_us,outputs_us,lock_wait_us,kinematic_us,rebuild_us,execute_us,physics_advance_us,physics_us,plumb_us,fence_us,writer_us,diag_us,snapshot_lock_us,snapshot_us,total_us,balls,kinematic_updates,moving_items,gc0,iterations,hit_tests,ball_tests,contacts,max_ball_tests,max_ball_id,max_ball_x,max_ball_y,max_ball_z,tests_triangle,tests_line3d,tests_line,tests_point,tests_plane,tests_circle,tests_flipper,tests_other,ball_octree_refits,broad_phase_visits");
			var sb = new StringBuilder(512);
			var n = StoredCount(_tickCount, TickCapacity);
			var i = FirstIndex(_tickCount, TickCapacity, _tickHead);
			for (var k = 0; k < n; k++, i = (i + 1) % TickCapacity) {
				ref var t = ref _ticks[i];
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
					.Append(t.BallOctreeRefits).Append(',').Append(c.BroadPhaseVisits);
				writer.WriteLine(sb.ToString());
			}
		}

		private static unsafe void WriteFrames(string path)
		{
			using var writer = new StreamWriter(path, false, Encoding.ASCII, 1 << 16);
			writer.WriteLine("frame,start_us,unity_time_us,realtime_us,unscaled_delta_us,delta_us,time_scale,gc0,snapshot_sim_time_us,snapshot_publish_us,snapshot_age_us,snapshot_balls,drain_us,drain_skipped,events_drained,actions_drained,scan_us,kinematic_changed,kinematic_stopped,apply_us,update_us,ball0_id,ball0_x,ball0_y,ball0_z,ball1_id,ball1_x,ball1_y,ball1_z,ball2_id,ball2_x,ball2_y,ball2_z,ball3_id,ball3_x,ball3_y,ball3_z");
			var sb = new StringBuilder(512);
			var n = StoredCount(_frameCount, FrameCapacity);
			var i = FirstIndex(_frameCount, FrameCapacity, _frameHead);
			for (var k = 0; k < n; k++, i = (i + 1) % FrameCapacity) {
				ref var f = ref _frames[i];
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

		private static unsafe void WriteLocks(string path)
		{
			using var writer = new StreamWriter(path, false, Encoding.ASCII, 1 << 16);
			writer.WriteLine("start_us,site,wait_us,hold_us");
			var n = StoredCount(_lockCount, LockCapacity);
			var i = FirstIndex(_lockCount, LockCapacity, _lockHead);
			for (var k = 0; k < n; k++, i = (i + 1) % LockCapacity) {
				ref var l = ref _locks[i];
				writer.WriteLine($"{l.StartUsec},{l.Site},{l.WaitUsec},{l.HoldUsec}");
			}
		}

		private static unsafe void WriteSummary(string path)
		{
			using var writer = new StreamWriter(path, false, Encoding.ASCII);
			var n = StoredCount(_tickCount, TickCapacity);
			var first = FirstIndex(_tickCount, TickCapacity, _tickHead);
			writer.WriteLine($"ticks recorded: {_tickCount} (stored {n}), frames: {_frameCount}, locks: {_lockCount}");
			if (n == 0) {
				return;
			}

			var total = new int[n];
			var execute = new int[n];
			var lockWait = new int[n];
			var snapshotLock = new int[n];
			var late = new int[n];
			var gap = new int[n];
			long droppedTotal = 0, jumpTotal = 0, physicsTotal = 0, gaps5 = 0, gaps20 = 0, gaps100 = 0, multiStep = 0, rebuilds = 0;
			var gcFirst = _ticks[first].Gc0;
			var gcLast = gcFirst;
			var i = first;
			for (var k = 0; k < n; k++, i = (i + 1) % TickCapacity) {
				ref var t = ref _ticks[i];
				total[k] = t.TotalUsec;
				execute[k] = t.ExecuteUsec;
				lockWait[k] = t.LockWaitUsec;
				snapshotLock[k] = t.SnapshotLockUsec;
				late[k] = (int)(t.StartUsec - t.TargetUsec);
				gap[k] = t.WaitUsec;
				droppedTotal += t.DroppedUsec;
				jumpTotal += t.ClockJumpUsec;
				physicsTotal += t.PhysicsUsec;
				if (t.WaitUsec > 5_000) gaps5++;
				if (t.WaitUsec > 20_000) gaps20++;
				if (t.WaitUsec > 100_000) gaps100++;
				if (t.PhysicsAdvanceUsec > 1_000) multiStep++;
				if (t.RebuildUsec > 0) rebuilds++;
				gcLast = t.Gc0;
			}
			var firstTick = _ticks[first];
			var lastTick = _ticks[(first + n - 1) % TickCapacity];
			var wallUsec = lastTick.StartUsec - firstTick.StartUsec;
			var simUsec = lastTick.SimTimeUsec - firstTick.SimTimeUsec;
			writer.WriteLine($"wall time: {wallUsec / 1000} ms, sim time advanced: {simUsec / 1000} ms, ticks/s: {(wallUsec > 0 ? n * 1_000_000.0 / wallUsec : 0):F1}");
			writer.WriteLine($"sim thread busy: {physicsTotal * 100.0 / math.max(1, wallUsec):F1}% in physics, dropped backlog total: {droppedTotal / 1000} ms, clock jumps total: {jumpTotal / 1000} ms");
			writer.WriteLine($"ticks with >1 sub step: {multiStep}, octree rebuilds: {rebuilds}, gc0 collections during run: {gcLast - gcFirst}");
			writer.WriteLine($"wait gaps >5 ms: {gaps5}, >20 ms: {gaps20}, >100 ms: {gaps100}");
			WritePercentiles(writer, "tick total us", total);
			WritePercentiles(writer, "execute us", execute);
			WritePercentiles(writer, "lock wait us", lockWait);
			WritePercentiles(writer, "snapshot lock wait us", snapshotLock);
			WritePercentiles(writer, "lateness us", late);
			WritePercentiles(writer, "wait gap us", gap);

			// largest gaps between consecutive ticks, with what preceded them
			writer.WriteLine();
			writer.WriteLine("largest wait gaps (gap = end of previous tick to start of this tick):");
			writer.WriteLine("  start_ms_rel  gap_us  requested_us  mode  gc0_delta  prev_total_us  prev_physics_us  prev_snapshot_us  prev_lock_wait_us  clock_jump_us  dropped_us  balls");
			var order = new int[n];
			for (var k = 0; k < n; k++) order[k] = k;
			Array.Sort(order, (a, b) => gap[b].CompareTo(gap[a]));
			for (var k = 0; k < math.min(40, n); k++) {
				var idx = order[k];
				var cur = _ticks[(first + idx) % TickCapacity];
				var prev = idx > 0 ? _ticks[(first + idx - 1) % TickCapacity] : default;
				writer.WriteLine($"  {(cur.StartUsec - _beginUsec) / 1000,12}  {cur.WaitUsec,6}  {cur.WaitRequestedUsec,12}  {cur.WaitMode,4}  {cur.Gc0 - prev.Gc0,9}  {prev.TotalUsec,13}  {prev.PhysicsUsec,15}  {prev.SnapshotUsec,16}  {prev.LockWaitUsec,17}  {cur.ClockJumpUsec,13}  {cur.DroppedUsec,10}  {cur.BallCount,5}");
			}

			// slowest ticks
			writer.WriteLine();
			writer.WriteLine("slowest ticks:");
			writer.WriteLine("  start_ms_rel  total_us  lock_wait_us  kinematic_us  rebuild_us  execute_us  advance_us  plumb_us  fence_us  writer_us  diag_us  snapshot_lock_us  snapshot_us  switches_us  input_us  outputs_us  balls  iterations  hit_tests  ball_tests  contacts  max_ball_tests  triangle  line3d  line  point  plane  circle  flipper  other  bp_visits  max_ball_pos");
			Array.Sort(order, (a, b) => total[b].CompareTo(total[a]));
			for (var k = 0; k < math.min(40, n); k++) {
				var cur = _ticks[(first + order[k]) % TickCapacity];
				var c = cur.Counters;
				writer.WriteLine($"  {(cur.StartUsec - _beginUsec) / 1000,12}  {cur.TotalUsec,8}  {cur.LockWaitUsec,12}  {cur.KinematicUsec,12}  {cur.RebuildUsec,10}  {cur.ExecuteUsec,10}  {cur.PhysicsAdvanceUsec,10}  {cur.PlumbUsec,8}  {cur.FenceUsec,8}  {cur.WriterUsec,9}  {cur.DiagUsec,7}  {cur.SnapshotLockUsec,16}  {cur.SnapshotUsec,11}  {cur.SwitchesUsec,11}  {cur.InputUsec,8}  {cur.OutputsUsec,10}  {cur.BallCount,5}  {c.Iterations,10}  {c.HitTests,9}  {c.BallTests,10}  {c.Contacts,8}  {c.MaxBallHitTests,14}  {c.Triangle,8}  {c.Line3D,6}  {c.Line,4}  {c.Point,5}  {c.Plane,5}  {c.Circle,6}  {c.Flipper,7}  {c.Other,5}  {c.BroadPhaseVisits,9}  ({c.MaxBallPosition.x:F0},{c.MaxBallPosition.y:F0},{c.MaxBallPosition.z:F0})");
			}

			// frames
			var fn = StoredCount(_frameCount, FrameCapacity);
			if (fn > 0) {
				var ff = FirstIndex(_frameCount, FrameCapacity, _frameHead);
				var frameDelta = new int[fn];
				var snapshotAge = new int[fn];
				var update = new int[fn];
				long staleSnapshots = 0, drainSkipped = 0;
				long lastPublish = -1;
				var fi = ff;
				for (var k = 0; k < fn; k++, fi = (fi + 1) % FrameCapacity) {
					ref var f = ref _frames[fi];
					frameDelta[k] = f.UnscaledDeltaUsec;
					snapshotAge[k] = f.SnapshotPublishUsec > 0 ? (int)(f.StartUsec - f.SnapshotPublishUsec) : 0;
					update[k] = f.UpdateUsec;
					if (f.SnapshotPublishUsec == lastPublish) staleSnapshots++;
					lastPublish = f.SnapshotPublishUsec;
					drainSkipped += f.DrainSkipped;
				}
				writer.WriteLine();
				writer.WriteLine($"frames: {fn}, frames that re-rendered the previous snapshot: {staleSnapshots}, event drains skipped: {drainSkipped}");
				WritePercentiles(writer, "frame delta us", frameDelta);
				WritePercentiles(writer, "snapshot age us", snapshotAge);
				WritePercentiles(writer, "physics update us", update);
			}

			// locks
			var ln = StoredCount(_lockCount, LockCapacity);
			if (ln > 0) {
				var lf = FirstIndex(_lockCount, LockCapacity, _lockHead);
				var waits = new int[ln];
				var holds = new int[ln];
				var li = lf;
				for (var k = 0; k < ln; k++, li = (li + 1) % LockCapacity) {
					waits[k] = _locks[li].WaitUsec;
					holds[k] = _locks[li].HoldUsec;
				}
				writer.WriteLine();
				writer.WriteLine($"main-thread physics lock acquisitions: {ln}");
				WritePercentiles(writer, "main lock wait us", waits);
				WritePercentiles(writer, "main lock hold us", holds);
			}
		}

		private static void WritePercentiles(TextWriter writer, string label, int[] values)
		{
			var sorted = (int[])values.Clone();
			Array.Sort(sorted);
			var n = sorted.Length;
			int P(double p) => sorted[math.clamp((int)(p * (n - 1)), 0, n - 1)];
			writer.WriteLine($"{label,-24} p50={P(0.5),8} p90={P(0.9),8} p99={P(0.99),8} p99.9={P(0.999),8} max={sorted[n - 1],8}");
		}

		#endregion
	}
}
