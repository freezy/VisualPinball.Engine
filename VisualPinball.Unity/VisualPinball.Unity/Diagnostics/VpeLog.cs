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

// Log level policy (see Documentation~/developer-guide/logging.md):
//
//   Fatal  VPE can't continue (simulation thread died, renderer lost). Reported.
//   Error  A VPE bug or an impossible state. If a user can cause it by authoring
//          a table or misconfiguring something, it is NOT an error. Reported.
//   Warn   The user, the table author or the environment got something wrong
//          (missing ROM, unmapped switch, missing asset). VPE recovers or degrades.
//   Info   One-off lifecycle milestones. A few lines per minute of play at most.
//   Debug  Diagnostic detail, off by default.
//   Trace  Per-event detail (switch, coil, lamp, frame), off by default.
//
// Error and Fatal call sites use message templates ("Coil {CoilId} ..."), never
// interpolated strings, so reports of the same problem group together.
//
// Code that runs per frame, per tick or per game event checks a LogThrottle (or
// Logger.IsDebugEnabled) before logging, and passes only numbers, strings, booleans
// and enums as arguments. NLog formats an event that has any other object argument
// right when it's created, on the logging thread, to capture the object's state.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NLog;
using NLog.Config;
using NLog.Layouts;
using NLog.Targets;
using NLog.Targets.Wrappers;
using UnityEngine;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Owns VPE's NLog configuration.
	/// </summary>
	///
	/// <remarks>
	/// <para>
	/// All targets except the editor console sit behind a single asynchronous queue, so the
	/// thread that logs (simulation, gamelogic callbacks, audio) only pays for creating the
	/// event and enqueueing it. Formatting, file I/O and reporting happen on NLog's writer
	/// thread, in the order the events were logged.
	/// </para>
	/// <para>
	/// <see cref="Install"/> runs once per domain, <see cref="BeginSession"/> once per play
	/// session. With domain reload disabled, statics survive between play sessions, so the
	/// two are kept apart.
	/// </para>
	/// <para>
	/// The host (the player) adds what only it knows about through <see cref="Configure"/>
	/// (log folder, level) and <see cref="AddTarget"/> (error reporting). Both can be
	/// called at any time and in any order; the configuration is rebuilt each time.
	/// </para>
	/// </remarks>
	public static class VpeLog
	{
		/// <summary>
		/// Event property holding the name of the thread that logged the event.
		/// </summary>
		public const string ThreadProperty = "vpe.thread";

		/// <summary>
		/// Event property set on events logged by the crash hooks. Error reporting skips
		/// them, because the reporting SDK captures those exceptions itself.
		/// </summary>
		public const string HookProperty = "vpe.hook";

		/// <summary>
		/// Event property set on exceptions forwarded from Unity's console. Error reporting
		/// skips them for the same reason as <see cref="HookProperty"/>.
		/// </summary>
		public const string UnityExceptionProperty = "vpe.unity-exception";

		/// <summary>
		/// Name of the logger that receives messages logged through UnityEngine.Debug.
		/// </summary>
		public const string UnityLoggerName = "Unity";

		private const string LevelArgument = "-vpe-log-level";
		private const string LogFilePrefix = "vpe-";
		private const int MaxLogFiles = 10;

		private static readonly Logger Logger = LogManager.GetLogger("VisualPinball.Unity.VpeLog");
		private static readonly object Lock = new();
		private static readonly List<(Target target, LogLevel minLevel)> ExtraTargets = new();

		private static bool _installed;
		private static int _mainThreadId = -1;
		private static string _logDirectory;
		private static LogLevel _configuredLevel = LogLevel.Info;
		private static LogLevel _commandLineLevel;
		private static LogQueueTarget _queue;
		private static long _droppedEvents;
		private static bool _sessionActive;

		/// <summary>
		/// Short identifier of the current session. It appears in the log header, in every
		/// error report and in the reference shown to users after they submit a report.
		/// </summary>
		public static string SessionId { get; private set; } = NewSessionId();

		/// <summary>
		/// When the current session started (local time).
		/// </summary>
		public static DateTime SessionStart { get; private set; } = DateTime.Now;

		/// <summary>
		/// Path of the current session's log file, or null if no log folder is configured.
		/// </summary>
		public static string LogFile { get; private set; }

		/// <summary>
		/// Folder holding the session log files, or null if none is configured.
		/// </summary>
		public static string LogDirectory => _logDirectory;

		/// <summary>
		/// The lowest level currently logged.
		/// </summary>
		public static LogLevel MinLevel => _commandLineLevel ?? _configuredLevel;

		/// <summary>
		/// Raised on the main thread after a new session has started and its log file is
		/// set up. Diagnostics use it to write the session header.
		/// </summary>
		public static event Action SessionStarted;

		#region Lifecycle

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void OnSubsystemRegistration()
		{
			Install();
			BeginSession();
		}

		/// <summary>
		/// Sets up logging for this domain. Safe to call more than once.
		/// </summary>
		public static void Install()
		{
			lock (Lock) {
				if (_installed) {
					return;
				}
				try {
					// NLog otherwise scans its assembly's folder for extension DLLs on first
					// use, which fails in the editor (the "folder" is Unity.exe) and is
					// pointless in a build. Everything VPE uses is in NLog itself.
					ConfigurationItemFactory.Default = new ConfigurationItemFactory(typeof(LogManager).Assembly);

					_mainThreadId = Thread.CurrentThread.ManagedThreadId;
					_commandLineLevel = ParseCommandLineLevel(Environment.GetCommandLineArgs());

					if (!Application.isEditor) {
						// Our own targets already carry what's needed; Unity's stack trace for
						// every info and warning line in Player.log is expensive.
						Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
						Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
					}

					UnityLogBridge.Install();
					CrashHooks.Install();
					Application.quitting -= OnQuitting;
					Application.quitting += OnQuitting;
					// stops the writer thread before the editor reloads the domain
					AppDomain.CurrentDomain.DomainUnload -= OnDomainUnload;
					AppDomain.CurrentDomain.DomainUnload += OnDomainUnload;
					Rebuild();
					_installed = true;

				} catch (Exception e) {
					// without logging there's nothing else to tell
					UnityEngine.Debug.LogError($"[VisualPinball] Cannot set up logging: {e}");
				}
			}
		}

		/// <summary>
		/// Starts a new logging session: a new session id, a new log file, a cleared tail
		/// and reset throttles. Called on every player start and play mode entry.
		/// </summary>
		public static void BeginSession()
		{
			lock (Lock) {
				Install();
				// what the previous session logged still goes to the previous session's file
				_queue?.Drain();
				SessionId = NewSessionId();
				SessionStart = DateTime.Now;
				LogThrottle.ResetAll();
				LogOncePerKeyVersion.Next();
				LogTail.Clear();
				Interlocked.Exchange(ref _droppedEvents, 0);
				LogFile = null;
				_sessionActive = true;
				StartLogFile();
				Rebuild();
			}
			Logger.Info("Session {SessionId:l} started", SessionId);
			PublishSessionDiagnostics();
			SessionStarted?.Invoke();
		}

		/// <summary>
		/// The session header: system and build sections, which don't change during a session.
		/// </summary>
		private static void PublishSessionDiagnostics()
		{
			try {
				DiagnosticsContext.Clear();
				LoadAttempt.Reset();
				BuildInfo.Invalidate();
				var system = new VisualPinball.Plugins.DiagnosticsWriter().Add("session", SessionId);
				SystemSpecs.Describe(system);
				DiagnosticsContext.Set(DiagnosticsContext.System, system);
				var build = new VisualPinball.Plugins.DiagnosticsWriter();
				BuildInfo.Current.Describe(build);
				DiagnosticsContext.Set(DiagnosticsContext.Build, build);

			} catch (Exception e) {
				// diagnostics must never keep the game from starting
				Logger.Error(e, "Cannot collect the session diagnostics");
			}
		}

		/// <summary>
		/// Applies host settings. Settings left null keep their current value.
		/// </summary>
		/// <param name="logDirectory">Folder for session log files. Logging to a file
		/// starts with this call; lines logged before it are copied from the tail. An
		/// empty string stops logging to a file.</param>
		/// <param name="level">Lowest level to log. The <c>-vpe-log-level</c> command
		/// line argument overrides it.</param>
		public static void Configure(string logDirectory = null, LogLevel level = null)
		{
			lock (Lock) {
				Install();
				if (level != null) {
					_configuredLevel = level;
				}
				if (logDirectory != null && logDirectory != (_logDirectory ?? string.Empty)) {
					_logDirectory = logDirectory.Length == 0 ? null : logDirectory;
					LogFile = null;
					// the file starts with the tail, so get everything queued into it first
					_queue?.Drain();
					// before the session starts, BeginSession() opens the file
					if (_sessionActive) {
						StartLogFile();
					}
				}
				Rebuild();
			}
		}

		/// <summary>
		/// Adds a target behind the shared queue, e.g. error reporting.
		/// </summary>
		public static void AddTarget(Target target, LogLevel minLevel)
		{
			if (target == null) {
				throw new ArgumentNullException(nameof(target));
			}
			lock (Lock) {
				Install();
				if (ExtraTargets.Any(t => t.target == target)) {
					return;
				}
				ExtraTargets.Add((target, minLevel));
				Rebuild();
			}
		}

		/// <summary>
		/// Removes a target added with <see cref="AddTarget"/>.
		/// </summary>
		public static void RemoveTarget(Target target)
		{
			lock (Lock) {
				if (ExtraTargets.RemoveAll(t => t.target == target) > 0) {
					Rebuild();
				}
			}
		}

		/// <summary>
		/// Writes everything queued so far. Blocks for at most <paramref name="timeout"/>.
		/// </summary>
		public static void Flush(TimeSpan timeout)
		{
			try {
				LogManager.Flush(timeout);
			} catch (Exception) {
				// flushing is best effort; a failure here must not mask the original problem
			}
		}

		/// <summary>
		/// Returns the last lines logged in this session, oldest first.
		/// </summary>
		public static string GetTail(int maxChars = LogTail.DefaultMaxChars) => LogTail.Snapshot(maxChars);

		/// <summary>
		/// Number of events dropped because the queue was full, this session.
		/// </summary>
		public static long DroppedEvents => Interlocked.Read(ref _droppedEvents);

		/// <summary>
		/// The name logged for the calling thread.
		/// </summary>
		public static string CurrentThreadName()
		{
			var thread = Thread.CurrentThread;
			if (thread.ManagedThreadId == _mainThreadId) {
				return "main";
			}
			// named threads may get their name late, so only the fallback is cached
			return thread.Name ?? (_unnamedThreadName ??= (thread.IsThreadPoolThread ? "pool-" : "thread-") + thread.ManagedThreadId);
		}

		[ThreadStatic]
		private static string _unnamedThreadName;

		private static void OnDomainUnload(object sender, EventArgs e)
		{
			_queue?.Stop(TimeSpan.FromSeconds(2));
			LogManager.Shutdown();
		}

		private static void OnQuitting()
		{
			Logger.Info("Session {SessionId:l} ending", SessionId);
			Flush(TimeSpan.FromSeconds(2));
			// in the editor this is the end of play mode; the next one starts a new session
			_sessionActive = false;
		}

		#endregion

		#region Configuration

		private const string FileLayout = "${date:format=HH\\:mm\\:ss.fff}|${level:uppercase=true:padding=-5}|${event-properties:item=" + ThreadProperty + "}|${logger:shortName=true}|${message}${onexception:${newline}${exception:format=tostring}}";
		private const string ConsoleLayout = "${logger:shortName=true}|${message}${onexception:${newline}${exception:format=tostring}}";

		private static void Rebuild()
		{
			var config = new LoggingConfiguration();
			var minLevel = MinLevel;
			var notFromUnity = "logger != '" + UnityLoggerName + "'";

			var children = new List<Target> {
				new LogTailTarget { Name = "tail", Layout = FileLayout },
			};
			if (LogFile != null) {
				children.Add(new FileTarget("file") {
					FileName = LogFile,
					Layout = FileLayout,
					KeepFileOpen = true,
					// a native crash takes the process down without flushing anything
					AutoFlush = true,
					ConcurrentWrites = false,
					Encoding = System.Text.Encoding.UTF8,
					WriteBom = false,
					LineEnding = LineEndingMode.LF,
				});
			}

			var console = new UnityTarget { Name = "unity", Layout = ConsoleLayout };
			if (Application.isEditor) {
				// synchronous in the editor, so double-clicking a console line still works,
				// and never below Info: debug output from other threads would block on it
				var consoleLevel = minLevel < LogLevel.Info ? LogLevel.Info : minLevel;
				config.AddRule(consoleLevel, LogLevel.Fatal, new FilteringTargetWrapper("unity-filter", console, notFromUnity));
			} else {
				children.Add(new FilteringTargetWrapper("unity-filter", console, "level >= LogLevel.Warn and " + notFromUnity));
			}

			foreach (var (target, targetLevel) in ExtraTargets) {
				children.Add(new FilteringTargetWrapper(target.Name + "-filter", target, "level >= LogLevel." + targetLevel.Name));
			}

			if (_queue != null) {
				_queue.Dropped -= OnLogEventDropped;
				// stop the old writer here, not under NLog's lock when the old configuration closes
				_queue.Stop(TimeSpan.FromSeconds(2));
			}
			_queue = new LogQueueTarget("queue", new SplitGroupTarget("all", children.ToArray()), 10000);
			_queue.Dropped += OnLogEventDropped;
			config.AddRule(minLevel, LogLevel.Fatal, _queue);

			// replacing the configuration closes the old queue, which writes what came in since
			LogManager.Configuration = config;
		}

		private static void OnLogEventDropped()
		{
			if (Interlocked.Increment(ref _droppedEvents) == 1) {
				// the queue is full, so this is best effort too
				UnityEngine.Debug.unityLogger.LogWarning("VisualPinball", "Log queue overflowed, dropping log lines.");
			}
		}

		private static void StartLogFile()
		{
			if (_logDirectory == null) {
				return;
			}
			try {
				Directory.CreateDirectory(_logDirectory);
				var path = Path.Combine(_logDirectory, $"{LogFilePrefix}{SessionStart:yyyyMMdd-HHmmss}-{SessionId}.log");

				// lines logged before the log folder was known are in the tail
				File.WriteAllText(path, LogTail.Snapshot(int.MaxValue));
				LogFile = path;
				DeleteOldLogFiles();

			} catch (Exception e) {
				LogFile = null;
				UnityEngine.Debug.unityLogger.LogWarning("VisualPinball", $"Cannot write log file to {_logDirectory}: {e.Message}");
			}
		}

		private static void DeleteOldLogFiles()
		{
			var files = new DirectoryInfo(_logDirectory)
				.GetFiles(LogFilePrefix + "*.log")
				.OrderByDescending(f => f.CreationTimeUtc)
				.Skip(MaxLogFiles);
			foreach (var file in files) {
				try {
					file.Delete();
				} catch (IOException) {
					// still open by another instance
				} catch (UnauthorizedAccessException) {
					// not ours to delete
				}
			}
		}

		#endregion

		#region Helpers

		/// <summary>
		/// Parses a level name as used in the config file and on the command line.
		/// </summary>
		/// <returns>The level, or null if the name isn't a level.</returns>
		public static LogLevel ParseLevel(string name)
		{
			if (string.IsNullOrWhiteSpace(name)) {
				return null;
			}
			switch (name.Trim().ToLowerInvariant()) {
				case "trace": return LogLevel.Trace;
				case "debug": return LogLevel.Debug;
				case "info": return LogLevel.Info;
				case "warn":
				case "warning": return LogLevel.Warn;
				case "error": return LogLevel.Error;
				case "fatal": return LogLevel.Fatal;
				default: return null;
			}
		}

		/// <summary>
		/// Reads <c>-vpe-log-level=debug</c> or <c>-vpe-log-level debug</c>.
		/// </summary>
		public static LogLevel ParseCommandLineLevel(IReadOnlyList<string> args)
		{
			if (args == null) {
				return null;
			}
			for (var i = 0; i < args.Count; i++) {
				var arg = args[i];
				if (arg == null || !arg.StartsWith(LevelArgument, StringComparison.OrdinalIgnoreCase)) {
					continue;
				}
				if (arg.Length > LevelArgument.Length && arg[LevelArgument.Length] == '=') {
					return ParseLevel(arg.Substring(LevelArgument.Length + 1));
				}
				if (arg.Length == LevelArgument.Length && i + 1 < args.Count) {
					return ParseLevel(args[i + 1]);
				}
			}
			return null;
		}

		private static string NewSessionId() => Guid.NewGuid().ToString("N").Substring(0, 8);

		#endregion
	}
}
