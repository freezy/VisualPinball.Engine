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
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using NLog;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using VisualPinball.Unity.Simulation;
using Logger = NLog.Logger;

namespace VisualPinball.Unity.Profiling
{
	/// <summary>
	/// A trace of the seconds around an incident, as a Perfetto / Chrome trace-event file
	/// (<c>perf-trace.json.gz</c>, opens in ui.perfetto.dev).
	/// </summary>
	///
	/// <remarks>
	/// <para>
	/// The trace must not cause the stutter it reports. The only main-thread step is copying
	/// the rings into preallocated buffers, once the window after the incident is over; the
	/// frames after that copy are flagged as self-induced, so they're no incident. The JSON is
	/// written by hand into a preallocated buffer on a worker thread, then gzipped and saved to
	/// <c>logs/perf/</c> (the newest 20 are kept), and <see cref="Written"/> is raised there.
	/// </para>
	/// <para>
	/// One capture at a time: a request while one is pending or being written is dropped.
	/// </para>
	/// </remarks>
	public static unsafe class TraceCapture
	{
		private const int MaxFrames = 12 * 240;
		private const int MaxTicks = 12 * 1000;
		private const int MaxSlow = 8192;
		private const int MaxEvents = 1024;
		private const int MaxLocks = 4096;
		private const int JsonCapacity = 12 << 20;
		private const int KeptTraces = 20;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// A trace was written: the incident, the file, and the gzipped bytes. On a worker thread.
		/// </summary>
		public static event Action<Incident, string, byte[]> Written;

		/// <summary>Where the traces go; set by the host (next to its logs).</summary>
		public static string Directory { get; set; }

		// what's requested
		private static bool _pending;
		private static Incident _incident;
		private static long _fromTicks, _toTicks;
		private static volatile bool _busy;

		// the copy
		private static FrameSample* _frames;
		private static SimulationTrace.TickRecord* _ticks;
		private static SectionRecorder.SlowCall* _slow;
		private static EventSample* _events;
		private static SimulationTrace.LockRecord* _locks;
		private static int _frameCount, _tickCount, _slowCount, _eventCount, _lockCount;
		private static readonly byte[] Json = new byte[JsonCapacity];
		private static readonly JsonWriter Writer = new(Json);

		// context, refreshed (and redacted) on the main thread at table load
		private static string _table, _fingerprint, _version, _gpu, _cpu, _os, _api, _session, _settings;
		private static string[] _phaseNames = Array.Empty<string>();

		// the request's context, so a capture written later still describes its own table
		private static string _reqTable, _reqFingerprint, _reqVersion, _reqGpu, _reqCpu, _reqOs, _reqApi, _reqSession, _reqSettings;
		private static string[] _reqPhaseNames = Array.Empty<string>();

		// reused for every trace
		private static readonly MemoryStream Compressed = new(4 << 20);

		public static bool IsBusy => _pending || _busy;

		/// <summary>Whether a trace is being written on a worker right now.</summary>
		public static bool IsWriting => _busy;

		/// <summary>
		/// Captures the window around an incident once <paramref name="postSeconds"/> after it
		/// have been recorded. Main thread. False if another capture is under way.
		/// </summary>
		public static bool Request(in Incident incident, double preSeconds, double postSeconds)
		{
			if (IsBusy) {
				return false;
			}
			Allocate();
			_incident = incident;
			_fromTicks = incident.StartTicks - Ticks.FromSeconds(preSeconds);
			_toTicks = System.Math.Max(incident.EndTicks, incident.StartTicks) + Ticks.FromSeconds(postSeconds);
			_reqTable = _table;
			_reqFingerprint = _fingerprint;
			_reqVersion = _version;
			_reqGpu = _gpu;
			_reqCpu = _cpu;
			_reqOs = _os;
			_reqApi = _api;
			_reqSession = _session;
			_reqSettings = _settings;
			_reqPhaseNames = _phaseNames;
			_pending = true;
			return true;
		}

		/// <summary>
		/// Writes a trace that's still waiting for its post-window now, on this thread, with
		/// what there is: the table is going away or the player quits. Main thread.
		/// </summary>
		public static void Flush()
		{
			if (_pending) {
				Update(Stopwatch.GetTimestamp(), quitting: true);
			}
			// a trace being written on a worker: give it a moment to be saved and reported
			WaitForWriter(TimeSpan.FromSeconds(3));
		}

		/// <summary>
		/// Waits until no trace is being written, at most the given time. True if none is.
		/// </summary>
		internal static bool WaitForWriter(TimeSpan timeout)
		{
			var until = Stopwatch.GetTimestamp() + (long)(timeout.TotalSeconds * Stopwatch.Frequency);
			while (_busy && Stopwatch.GetTimestamp() < until) {
				System.Threading.Thread.Sleep(10);
			}
			return !_busy;
		}

		/// <summary>
		/// Frees the buffers, before a domain reload. A trace being written keeps them.
		/// </summary>
		internal static void Free()
		{
			_pending = false;
			// the worker reads the buffers; after a timeout they're leaked rather than freed under it
			if (!WaitForWriter(TimeSpan.FromSeconds(5)) || _frames == null) {
				return;
			}
			UnsafeUtility.Free(_frames, Allocator.Persistent);
			UnsafeUtility.Free(_ticks, Allocator.Persistent);
			UnsafeUtility.Free(_slow, Allocator.Persistent);
			UnsafeUtility.Free(_events, Allocator.Persistent);
			UnsafeUtility.Free(_locks, Allocator.Persistent);
			_frames = null;
			_ticks = null;
			_slow = null;
			_events = null;
			_locks = null;
		}

		/// <summary>
		/// Gathers the data for a trace, once the incident's window is over and its frames got
		/// their timings. Called by the <see cref="FrameRecorder"/> at the end of every frame.
		/// </summary>
		internal static void Update(long nowTicks, bool quitting = false)
		{
			if (!_pending || _frames == null) {
				return;
			}
			if (!quitting && FrameRecorder.FinalizedThroughTicks < _toTicks) {
				return;
			}
			_pending = false;
			var started = Stopwatch.GetTimestamp();
			Copy(System.Math.Min(_toTicks, nowTicks));
			FrameRecorder.MarkSelfInduced(2);
			_busy = true;
			var copyMs = Ticks.ToMs(Stopwatch.GetTimestamp() - started);
			if (quitting) {
				// the process may be gone before a worker would run
				WriteTrace(copyMs);
			} else {
				ThreadPool.QueueUserWorkItem(_ => WriteTrace(copyMs));
			}
		}

		/// <summary>
		/// What goes into the trace header; main thread, at table load, so the trace doesn't have
		/// to ask for it (those getters allocate).
		/// </summary>
		public static void RefreshContext()
		{
			var load = LoadAttempt.Current;
			// a table title comes from its package; it's not supposed to hold a path, but may
			_table = Redaction.Redact(load?.Title);
			_fingerprint = load?.Fingerprint;
			_version = BuildInfo.Current.version + " (" + BuildInfo.Flavor + ")";
			_gpu = SystemInfo.graphicsDeviceName + " (" + SystemInfo.graphicsMemorySize + " MB)";
			_cpu = SystemInfo.processorType + " (" + SystemInfo.processorCount + " threads)";
			_os = SystemInfo.operatingSystem;
			_api = SystemInfo.graphicsDeviceType.ToString();
			_session = VpeLog.SessionId;
			_settings = Screen.width + "x" + Screen.height + " " + Screen.fullScreenMode + " @ " + Screen.currentResolution.refreshRateRatio.value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
				+ " Hz, vsync " + QualitySettings.vSyncCount + ", target fps " + Application.targetFrameRate + ", quality " + QualitySettings.names[QualitySettings.GetQualityLevel()]
				+ ", displays " + Display.displays.Length;
			_phaseNames = FrameRecorder.PhaseNames;
		}

		private static void Allocate()
		{
			if (_frames != null) {
				return;
			}
			_frames = Alloc<FrameSample>(MaxFrames);
			_ticks = Alloc<SimulationTrace.TickRecord>(MaxTicks);
			_slow = Alloc<SectionRecorder.SlowCall>(MaxSlow);
			_events = Alloc<EventSample>(MaxEvents);
			_locks = Alloc<SimulationTrace.LockRecord>(MaxLocks);
		}

		private static T* Alloc<T>(int count) where T : unmanaged
			=> (T*)UnsafeUtility.Malloc((long)count * sizeof(T), UnsafeUtility.AlignOf<T>(), Allocator.Persistent);

		#region Copy (main thread)

		private static void Copy(long toTicks)
		{
			var from = _fromTicks;
			var fromUsec = VpeClock.TicksToUsec(from);
			var toUsec = VpeClock.TicksToUsec(toTicks);

			// each ring: find the window from the newest end, keep its newest part if it doesn't
			// fit (that's where the incident and its post-window are), copy oldest first

			// frames
			var count = FrameRecorder.Count;
			var oldest = System.Math.Max(0, count - FrameRecorder.Capacity);
			var hi = count;
			while (hi > oldest && FrameRecorder.Frames[(hi - 1) % FrameRecorder.Capacity].StartTicks > toTicks) {
				hi--;
			}
			var lo = hi;
			while (lo > oldest && hi - lo < MaxFrames && FrameRecorder.Frames[(lo - 1) % FrameRecorder.Capacity].EndTicks >= from) {
				lo--;
			}
			_frameCount = 0;
			for (var i = lo; i < hi; i++) {
				_frames[_frameCount++] = FrameRecorder.Frames[i % FrameRecorder.Capacity];
			}

			// sections slower than 0.5 ms
			var slow = SectionRecorder.SlowCount;
			oldest = System.Math.Max(0, slow - SectionRecorder.SlowCapacity);
			hi = slow;
			while (hi > oldest && SectionRecorder.SlowCalls[(hi - 1) % SectionRecorder.SlowCapacity].StartTicks > toTicks) {
				hi--;
			}
			lo = hi;
			while (lo > oldest && hi - lo < MaxSlow && SectionRecorder.SlowCalls[(lo - 1) % SectionRecorder.SlowCapacity].EndTicks >= from) {
				lo--;
			}
			_slowCount = 0;
			for (var i = lo; i < hi; i++) {
				_slow[_slowCount++] = SectionRecorder.SlowCalls[i % SectionRecorder.SlowCapacity];
			}

			// events
			var events = FrameRecorder.EventCount;
			oldest = System.Math.Max(0, events - FrameRecorder.EventCapacity);
			hi = events;
			while (hi > oldest && FrameRecorder.Events[(hi - 1) % FrameRecorder.EventCapacity].Ticks > toTicks) {
				hi--;
			}
			lo = hi;
			while (lo > oldest && hi - lo < MaxEvents && FrameRecorder.Events[(lo - 1) % FrameRecorder.EventCapacity].Ticks >= from) {
				lo--;
			}
			_eventCount = 0;
			for (var i = lo; i < hi; i++) {
				_events[_eventCount++] = FrameRecorder.Events[i % FrameRecorder.EventCapacity];
			}

			// simulation ticks and lock waits, via the trace's seqlock; a torn or lapped record
			// is skipped
			var ticks = SimulationTrace.TickCount;
			oldest = System.Math.Max(0, ticks - SimulationTrace.SessionTickCapacity);
			hi = ticks;
			while (hi > oldest && (!SimulationTrace.TryReadTick(hi - 1, out var t) || t.StartUsec > toUsec)) {
				hi--;
			}
			lo = hi;
			while (lo > oldest && hi - lo < MaxTicks && (!SimulationTrace.TryReadTick(lo - 1, out var t) || t.StartUsec >= fromUsec)) {
				lo--;
			}
			_tickCount = 0;
			for (var i = lo; i < hi; i++) {
				if (SimulationTrace.TryReadTick(i, out var t) && t.StartUsec >= fromUsec && t.StartUsec <= toUsec) {
					_ticks[_tickCount++] = t;
				}
			}
			var locks = SimulationTrace.LockCount;
			oldest = System.Math.Max(0, locks - SimulationTrace.SessionLockCapacity);
			hi = locks;
			while (hi > oldest && (!SimulationTrace.TryReadLock(hi - 1, out var l) || l.StartUsec > toUsec)) {
				hi--;
			}
			lo = hi;
			while (lo > oldest && hi - lo < MaxLocks && (!SimulationTrace.TryReadLock(lo - 1, out var l) || l.StartUsec >= fromUsec)) {
				lo--;
			}
			_lockCount = 0;
			for (var i = lo; i < hi; i++) {
				if (SimulationTrace.TryReadLock(i, out var l) && l.StartUsec >= fromUsec && l.StartUsec <= toUsec) {
					_locks[_lockCount++] = l;
				}
			}
		}

		#endregion

		#region Write (worker)

		private static void WriteTrace(double copyMs)
		{
			try {
				var w = Writer;
				w.Reset();
				Serialize(w, copyMs);
				var directory = Directory;
				if (string.IsNullOrEmpty(directory)) {
					return;
				}
				System.IO.Directory.CreateDirectory(directory);
				var name = $"perf-{DateTime.Now:yyyyMMdd-HHmmss}-{Incident.Name(_incident.Type)}-{Incident.Name(_incident.Cause)}.json.gz";
				var path = Path.Combine(directory, name);
				Compressed.SetLength(0);
				using (var gzip = new GZipStream(Compressed, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true)) {
					gzip.Write(Json, 0, w.Length);
				}
				// the attachment needs its own array
				var gz = Compressed.ToArray();
				File.WriteAllBytes(path, gz);
				DeleteOld(directory);
				Logger.Info("Perf trace of the {Type:l} ({Cause:l}) written: {File:l}, {Kb} KB ({RawKb} KB raw), copied in {CopyMs:0.00} ms",
					Incident.Name(_incident.Type), Incident.Name(_incident.Cause), name, gz.Length / 1024, w.Length / 1024, copyMs);
				Written?.Invoke(_incident, path, gz);
			} catch (Exception e) {
				Logger.Warn(e, "Cannot write the perf trace.");
			} finally {
				_busy = false;
			}
		}

		private static void DeleteOld(string directory)
		{
			var files = new DirectoryInfo(directory).GetFiles("perf-*.json.gz").OrderByDescending(f => f.CreationTimeUtc).Skip(KeptTraces);
			foreach (var file in files) {
				try {
					file.Delete();
				} catch (IOException) {
					// in use
				}
			}
		}

		private const int Pid = 1;
		private const int MainTid = 1, SimTid = 2, GpuTid = 3;

		private static void Serialize(JsonWriter w, double copyMs)
		{
			var origin = _fromTicks;
			w.Raw("{\"displayTimeUnit\":\"ms\",\"metadata\":{");
			w.Raw("\"producer\":\"VPE profile build\",\"version\":").Str(_reqVersion);
			w.Raw(",\"session\":").Str(_reqSession).Raw(",\"table\":").Str(_reqTable).Raw(",\"fingerprint\":").Str(_reqFingerprint);
			w.Raw(",\"gpu\":").Str(_reqGpu).Raw(",\"cpu\":").Str(_reqCpu).Raw(",\"os\":").Str(_reqOs).Raw(",\"api\":").Str(_reqApi).Raw(",\"display\":").Str(_reqSettings);
			w.Raw(",\"incident\":{\"type\":").Str(Incident.Name(_incident.Type)).Raw(",\"cause\":").Str(Incident.Name(_incident.Cause));
			w.Raw(",\"secondary\":[");
			var first = true;
			for (var i = 1; i < 32; i++) {
				if ((_incident.SecondaryCauses & (1u << i)) != 0) {
					if (!first) {
						w.Raw(",");
					}
					first = false;
					w.Str(Incident.Name((IncidentCause)i));
				}
			}
			w.Raw("],\"severity_ms\":").Float(_incident.SeverityMs).Raw(",\"frame_ms\":").Float(_incident.FrameMs).Raw(",\"median_ms\":").Float(_incident.MedianMs)
				.Raw(",\"target_ms\":").Float(_incident.TargetMs).Raw(",\"unexplained_ms\":").Float(_incident.UnexplainedMs)
				.Raw(",\"gc\":").Int(_incident.GcDelta).Raw(",\"tick_gap_us\":").Int(_incident.MaxTickGapUsec).Raw(",\"tick_work_us\":").Int(_incident.MaxTickWorkUsec)
				.Raw(",\"lock_wait_us\":").Int(_incident.LockWaitUsec).Raw(",\"seconds_into_play\":").Float(_incident.SecondsIntoPlay);
			if (_incident.Cause == IncidentCause.Section) {
				w.Raw(",\"section\":").Str(SectionRecorder.Name(_incident.Section)).Raw(",\"section_us\":").Int(_incident.SectionUsec);
			}
			w.Raw(",\"ts\":").Ts(_incident.StartTicks, origin).Raw("}");
			w.Raw(",\"clock\":{\"stopwatch_hz\":").Int(Stopwatch.Frequency).Raw(",\"origin_ticks\":").Int(origin).Raw(",\"origin_monotonic\":").Int(origin - FrameRecorder.MonotonicOffset).Raw(",\"unity_timer_to_stopwatch\":").Float((float)FrameRecorder.TimerToStopwatch).Raw("}");
			w.Raw(",\"copy_ms\":").Float((float)copyMs);
			w.Raw("},\"traceEvents\":[");

			// names
			w.Raw("{\"ph\":\"M\",\"pid\":1,\"name\":\"process_name\",\"args\":{\"name\":\"VPE Player\"}}");
			Thread(w, MainTid, "Main thread");
			Thread(w, SimTid, "Simulation thread");
			Thread(w, GpuTid, "GPU (Unity timing)");

			// frames, their PlayerLoop phases, and Unity's timings
			for (var i = 0; i < _frameCount && !w.Full; i++) {
				ref var f = ref _frames[i];
				var interval = i + 1 < _frameCount ? Ticks.ToMs(_frames[i + 1].StartTicks - f.StartTicks) : f.UnscaledDeltaMs;
				w.Raw(",{\"ph\":\"X\",\"pid\":1,\"tid\":1,\"name\":\"Frame\",\"ts\":").Ts(f.StartTicks, origin).Raw(",\"dur\":").Dur(f.EndTicks - f.StartTicks)
					.Raw(",\"args\":{\"frame\":").Int(f.Frame).Raw(",\"interval_ms\":").Float((float)interval).Raw(",\"balls\":").Int(f.Balls)
					.Raw(",\"snapshot_age_us\":").Int(f.SnapshotAgeUsec).Raw(",\"gc_used_mb\":").Float(f.GcUsedBytes / 1048576f).Raw("}}");
				for (var p = 0; p < FrameSample.Phases - 1; p++) {
					var duration = f.Phase[p + 1] - f.Phase[p];
					if (duration <= 0) {
						continue;
					}
					w.Raw(",{\"ph\":\"X\",\"pid\":1,\"tid\":1,\"name\":").Str(p < _reqPhaseNames.Length ? _reqPhaseNames[p] : "Phase").Raw(",\"ts\":").Ts(f.Phase[p], origin).Raw(",\"dur\":").Dur(duration).Raw("}");
				}
				if (f.Timing == TimingState.Matched) {
					w.Raw(",{\"ph\":\"C\",\"pid\":1,\"name\":\"Unity frame timing (ms)\",\"ts\":").Ts(f.StartTicks, origin)
						.Raw(",\"args\":{\"cpu_main\":").Float(f.CpuMainMs).Raw(",\"cpu_render\":").Float(f.CpuRenderMs).Raw(",\"gpu\":").Float(f.GpuMs)
						.Raw(",\"present_wait\":").Float(f.PresentWaitMs).Raw("}}");
					if (f.GpuMs > 0 && f.FirstSubmitTicks > 0) {
						w.Raw(",{\"ph\":\"X\",\"pid\":1,\"tid\":3,\"name\":\"GPU\",\"ts\":").Ts(f.FirstSubmitTicks, origin).Raw(",\"dur\":").Dur(Ticks.FromSeconds(f.GpuMs / 1000.0)).Raw("}");
					}
					if (f.PresentCalledTicks > 0) {
						w.Raw(",{\"ph\":\"i\",\"s\":\"t\",\"pid\":1,\"tid\":1,\"name\":\"Present\",\"ts\":").Ts(f.PresentCalledTicks, origin).Raw("}");
					}
				}
				// what each part of VPE cost in the frame
				w.Raw(",{\"ph\":\"C\",\"pid\":1,\"name\":\"VPE sections (us)\",\"ts\":").Ts(f.StartTicks, origin).Raw(",\"args\":{");
				var any = false;
				var n = System.Math.Min(SectionRecorder.Count, FrameSample.MaxSections);
				for (var s = 0; s < n; s++) {
					if (f.SectionUsec[s] == 0) {
						continue;
					}
					if (any) {
						w.Raw(",");
					}
					any = true;
					w.Str(SectionRecorder.Name((ProfileSection)s)).Raw(":").Int(f.SectionUsec[s]);
				}
				w.Raw("}}");
				if (i > 0 && f.GcCount != _frames[i - 1].GcCount) {
					w.Raw(",{\"ph\":\"i\",\"s\":\"p\",\"pid\":1,\"tid\":1,\"name\":\"GC collection\",\"ts\":").Ts(f.StartTicks, origin).Raw("}");
				}
			}

			// sections that took 0.5 ms or more
			for (var i = 0; i < _slowCount && !w.Full; i++) {
				ref var s = ref _slow[i];
				w.Raw(",{\"ph\":\"X\",\"pid\":1,\"tid\":1,\"name\":").Str(SectionRecorder.Name(s.Section)).Raw(",\"ts\":").Ts(s.StartTicks, origin).Raw(",\"dur\":").Dur(s.EndTicks - s.StartTicks).Raw("}");
			}

			// simulation ticks, with the details of stalled ones
			var originUsec = VpeClock.TicksToUsec(origin);
			for (var i = 0; i < _tickCount && !w.Full; i++) {
				ref var t = ref _ticks[i];
				w.Raw(",{\"ph\":\"X\",\"pid\":1,\"tid\":2,\"name\":\"Tick\",\"ts\":").UsecTs(t.StartUsec - originUsec).Raw(",\"dur\":").Int(System.Math.Max(1, t.TotalUsec));
				var gap = t.WaitUsec - System.Math.Max(0, t.WaitRequestedUsec);
				if (gap > 2000 || t.TotalUsec > 1000 || t.DroppedUsec > 0) {
					w.Raw(",\"args\":{\"wait_gap_us\":").Int(t.WaitUsec).Raw(",\"wait_requested_us\":").Int(t.WaitRequestedUsec)
						.Raw(",\"lock_wait_us\":").Int(t.LockWaitUsec).Raw(",\"rebuild_us\":").Int(t.RebuildUsec).Raw(",\"execute_us\":").Int(t.ExecuteUsec)
						.Raw(",\"snapshot_us\":").Int(t.SnapshotUsec).Raw(",\"dropped_us\":").Int(t.DroppedUsec).Raw(",\"gc0\":").Int(t.Gc0).Raw(",\"balls\":").Int(t.BallCount).Raw("}");
				}
				w.Raw("}");
			}

			// main-thread waits for the physics lock
			for (var i = 0; i < _lockCount && !w.Full; i++) {
				ref var l = ref _locks[i];
				if (l.WaitUsec < 100) {
					continue;
				}
				w.Raw(",{\"ph\":\"X\",\"pid\":1,\"tid\":1,\"name\":\"PhysicsLock wait\",\"ts\":").UsecTs(l.StartUsec - originUsec).Raw(",\"dur\":").Int(l.WaitUsec).Raw("}");
			}

			// what happened
			for (var i = 0; i < _eventCount && !w.Full; i++) {
				ref var e = ref _events[i];
				w.Raw(",{\"ph\":\"i\",\"s\":\"g\",\"pid\":1,\"tid\":1,\"name\":").Str(EventName(e.Event)).Raw(",\"ts\":").Ts(e.Ticks, origin)
					.Raw(",\"args\":{\"value\":").Int(e.Value).Raw("}}");
			}
			w.Raw(",{\"ph\":\"i\",\"s\":\"g\",\"pid\":1,\"tid\":1,\"name\":").Raw("\"Incident: ").Raw(Incident.Name(_incident.Type)).Raw("\"").Raw(",\"ts\":").Ts(_incident.StartTicks, origin).Raw("}");
			w.Raw("]}");
		}

		private static void Thread(JsonWriter w, int tid, string name)
		{
			w.Raw(",{\"ph\":\"M\",\"pid\":1,\"tid\":").Int(tid).Raw(",\"name\":\"thread_name\",\"args\":{\"name\":").Str(name).Raw("}}");
		}

		private static string EventName(ProfileEvent evt) => evt switch {
			ProfileEvent.Focus => "Focus",
			ProfileEvent.DisplayModeChanged => "Display mode changed",
			ProfileEvent.DisplaysChanged => "Displays changed",
			ProfileEvent.TableLoad => "Table load",
			ProfileEvent.TableUnload => "Table unload",
			ProfileEvent.Pause => "Pause",
			ProfileEvent.QualityChanged => "Quality changed",
			ProfileEvent.VramOverBudget => "VRAM over budget",
			ProfileEvent.SelfInduced => "Profiler",
			ProfileEvent.FirstUseLight => "First use: light",
			ProfileEvent.FirstUseFlasher => "First use: flasher",
			ProfileEvent.FirstUseMaterial => "First use: material",
			ProfileEvent.FirstUseMultiball => "First use: multiball",
			ProfileEvent.FirstUseServiceMenu => "First use: service menu",
			ProfileEvent.FirstUseDrain => "First use: drain",
			ProfileEvent.UserMarker => "User marker",
			_ => "Event",
		};

		#endregion
	}

	/// <summary>
	/// JSON into a fixed byte buffer, without allocating. Stops writing (and says
	/// <see cref="Full"/>) when the buffer is nearly full, so the caller can close the document.
	/// </summary>
	internal sealed class JsonWriter
	{
		private const int Reserve = 4096;
		private readonly byte[] _buffer;
		private int _length;

		public JsonWriter(byte[] buffer)
		{
			_buffer = buffer;
		}

		public void Reset() => _length = 0;

		public int Length => _length;
		public bool Full => _length > _buffer.Length - Reserve;

		/// <summary>ASCII text as is; for constants.</summary>
		public JsonWriter Raw(string ascii)
		{
			if (_length + ascii.Length > _buffer.Length) {
				return this;
			}
			for (var i = 0; i < ascii.Length; i++) {
				_buffer[_length++] = (byte)ascii[i];
			}
			return this;
		}

		/// <summary>A JSON string, escaped; null as null.</summary>
		public JsonWriter Str(string value)
		{
			if (value == null) {
				return Raw("null");
			}
			if (_length + value.Length * 6 + 2 > _buffer.Length) {
				return this;
			}
			_buffer[_length++] = (byte)'"';
			for (var i = 0; i < value.Length; i++) {
				var c = value[i];
				if (c == '"' || c == '\\') {
					_buffer[_length++] = (byte)'\\';
					_buffer[_length++] = (byte)c;
				} else if (c < 0x20) {
					_buffer[_length++] = (byte)'\\';
					_buffer[_length++] = (byte)'u';
					_buffer[_length++] = (byte)'0';
					_buffer[_length++] = (byte)'0';
					_buffer[_length++] = Hex(c >> 4);
					_buffer[_length++] = Hex(c & 0xF);
				} else if (c < 0x80) {
					_buffer[_length++] = (byte)c;
				} else if (c < 0x800) {
					_buffer[_length++] = (byte)(0xC0 | c >> 6);
					_buffer[_length++] = (byte)(0x80 | c & 0x3F);
				} else if (char.IsSurrogate(c)) {
					// rare in a table name; replaced rather than decoded
					_buffer[_length++] = (byte)'?';
				} else {
					_buffer[_length++] = (byte)(0xE0 | c >> 12);
					_buffer[_length++] = (byte)(0x80 | c >> 6 & 0x3F);
					_buffer[_length++] = (byte)(0x80 | c & 0x3F);
				}
			}
			_buffer[_length++] = (byte)'"';
			return this;
		}

		public JsonWriter Int(long value)
		{
			if (_length + 21 > _buffer.Length) {
				return this;
			}
			if (value < 0) {
				_buffer[_length++] = (byte)'-';
				if (value == long.MinValue) {
					return Raw("9223372036854775808");
				}
				value = -value;
			}
			var divisor = 1L;
			while (value / divisor >= 10) {
				divisor *= 10;
			}
			for (; divisor > 0; divisor /= 10) {
				_buffer[_length++] = (byte)('0' + value / divisor % 10);
			}
			return this;
		}

		/// <summary>A number with three decimals; NaN and infinities as 0.</summary>
		public JsonWriter Float(float value)
		{
			if (float.IsNaN(value) || float.IsInfinity(value)) {
				return Raw("0");
			}
			var thousandths = (long)System.Math.Round(value * 1000.0);
			return Fixed3(thousandths);
		}

		/// <summary>A timestamp in µs relative to the origin, from stopwatch ticks.</summary>
		public JsonWriter Ts(long ticks, long originTicks) => Fixed3((ticks - originTicks) * 1_000_000_000L / Stopwatch.Frequency);

		/// <summary>A duration in µs from stopwatch ticks.</summary>
		public JsonWriter Dur(long ticks) => Fixed3(System.Math.Max(0, ticks) * 1_000_000_000L / Stopwatch.Frequency);

		/// <summary>A timestamp already in µs.</summary>
		public JsonWriter UsecTs(long usec) => Int(usec);

		private JsonWriter Fixed3(long thousandths)
		{
			if (thousandths < 0) {
				Raw("-");
				thousandths = -thousandths;
			}
			Int(thousandths / 1000);
			var rest = thousandths % 1000;
			if (rest == 0 || _length + 4 > _buffer.Length) {
				return this;
			}
			_buffer[_length++] = (byte)'.';
			_buffer[_length++] = (byte)('0' + rest / 100);
			_buffer[_length++] = (byte)('0' + rest / 10 % 10);
			_buffer[_length++] = (byte)('0' + rest % 10);
			return this;
		}

		private static byte Hex(int value) => (byte)(value < 10 ? '0' + value : 'a' + value - 10);
	}
}
#endif
