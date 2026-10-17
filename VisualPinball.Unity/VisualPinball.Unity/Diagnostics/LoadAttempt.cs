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
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using NLog;
using UnityEngine;
using VisualPinball.Plugins;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	/// <summary>
	/// The table being loaded or played, and how far loading got.
	/// </summary>
	///
	/// <remarks>
	/// <para>
	/// Created before anything is read, so a report about a table that failed to load still
	/// says which table it was and in which phase it failed, also after the plugins that knew
	/// more have been torn down. Main thread only.
	/// </para>
	/// <para>
	/// It also times the load: every phase and the reader's steps are milestones of a
	/// <see cref="Timeline"/>. The table is <i>playable</i> at the first frame rendered after
	/// <see cref="Succeed"/> and, with a gamelogic engine, after the engine said it started
	/// (<see cref="GamelogicStarted"/>). It's <i>settled</i> at the end of the first 3 s window in
	/// which no frame took more than 1.5 times that window's median. The load finishes as
	/// settled, timeout (the engine didn't start, or the frames didn't settle, within 60 s),
	/// failed or cancelled, logs a line, and raises <see cref="Finished"/>.
	/// </para>
	/// </remarks>
	public sealed class LoadAttempt
	{
		public const string PhaseRead = "read";
		public const string PhaseImport = "import";
		public const string PhasePlugins = "plugins";
		public const string PhaseGamelogic = "gle-init";
		public const string PhasePhysics = "physics";
		public const string PhasePlaying = "playing";
		public const string PhaseFailed = "failed";

		public const string ResultSettled = "settled";
		public const string ResultTimeout = "timeout";
		public const string ResultFailed = "failed";
		public const string ResultCancelled = "cancelled";

		private const double SettleWindowSeconds = 3;
		private const double SettleTimeoutSeconds = 60;
		private const float SettleMaxOverMedian = 1.5f;
		private const int MaxSettleFrames = 2048;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// The current attempt, or null when no table was loaded in this session.
		/// </summary>
		public static LoadAttempt Current { get; private set; }

		/// <summary>
		/// Raised once per attempt when its load finished, see <see cref="Result"/>.
		/// </summary>
		public static event Action<LoadAttempt> Finished;

		// per session: which tables were loaded, and how many loads there were
		private static readonly HashSet<string> LoadedThisSession = new(StringComparer.OrdinalIgnoreCase);
		private static int _sessionLoads;
		private static readonly float[] SettleFrames = new float[MaxSettleFrames];

		public string Path { get; }
		public string Title { get; private set; }
		public long Size { get; }
		public string Fingerprint { get; private set; }
		public string Phase { get; private set; }
		public string FailedPhase { get; private set; }
		public string Error { get; private set; }

		public string Manufacturer { get; private set; }
		public int? Year { get; private set; }
		public string Authors { get; private set; }
		public int? FormatVersion { get; private set; }
		public string WrittenBy { get; private set; }
		public int? ComponentTypes { get; private set; }
		public string GameId { get; private set; }

		/// <summary>
		/// Where the load's time went: the phases and the reader's steps, plus facts like
		/// whether the texture cache was hit.
		/// </summary>
		public Timeline Timeline { get; }

		/// <summary>
		/// How the load finished (<see cref="ResultSettled"/>, <see cref="ResultTimeout"/>,
		/// <see cref="ResultFailed"/> or <see cref="ResultCancelled"/>), or null while it runs.
		/// </summary>
		public string Result { get; private set; }

		/// <summary>
		/// Wall time from the start to the first frame after the table was ready, in ms, or -1.
		/// </summary>
		public double PlayableMs { get; private set; } = -1;

		/// <summary>
		/// Wall time from the start to settled, in ms, or -1 if it didn't settle.
		/// </summary>
		public double SettledMs { get; private set; } = -1;

		private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
		private long _loadMs = -1;

		// set by Succeed (and the engine's start), cleared by the next frame
		private bool _awaitingFrame;
		private bool _awaitingGamelogic;
		private bool _gamelogicStarted;
		private double _succeededAt;
		private float _windowMaxMs;
		private bool _settling;
		private double _playableAt;
		private double _lastFrameAt;
		private double _windowStart;
		private int _windowFrames;

		private LoadAttempt(string path, string title)
		{
			Path = path;
			Title = title;
			try {
				Size = new FileInfo(path).Length;
			} catch (Exception) {
				Size = -1;
			}
			Phase = PhaseRead;
			Timeline = new Timeline("table-load", "begin");
			_sessionLoads++;
			Timeline.Fact("load", (LoadedThisSession.Add(path ?? "") ? "first in session" : "reload")
				+ ", #" + _sessionLoads.ToString(CultureInfo.InvariantCulture) + " in session");
		}

		/// <summary>
		/// Starts tracking a table load.
		/// </summary>
		public static LoadAttempt Begin(string path, string title)
		{
			// a load that never finished, e.g. because it was replaced by this one
			Current?.Finish(ResultCancelled);
			var attempt = new LoadAttempt(path, title);
			Current = attempt;
			Logger.Info("Loading table {Title} from {File} ({SizeMb:0.0} MB)", title, System.IO.Path.GetFileName(path), attempt.Size / 1048576.0);
			attempt.Publish(log: false);
			attempt.ComputeFingerprintAsync();
			return attempt;
		}

		/// <summary>
		/// Forgets the table, e.g. after it was exited normally.
		/// </summary>
		public static void End()
		{
			Current?.Finish(ResultCancelled);
			Current = null;
			DiagnosticsContext.Remove(DiagnosticsContext.Table);
		}

		/// <summary>
		/// Forgets the attempt without touching the diagnostics, which a new session clears
		/// anyway. With domain reload off, the last play session's attempt would linger.
		/// </summary>
		internal static void Reset()
		{
			Current = null;
			LoadedThisSession.Clear();
			_sessionLoads = 0;
		}

		public void SetPhase(string phase)
		{
			if (FailedPhase != null || Phase == phase) {
				return;
			}
			// a milestone says what finished
			Mark(Phase);
			Phase = phase;
			Logger.Debug("Table load phase: {Phase:l}", phase);
			Publish(log: false);
		}

		public void SetMetadata(VpeTableMetadataSummary metadata)
		{
			if (metadata == null) {
				return;
			}
			Title = string.IsNullOrEmpty(metadata.Title) ? Title : metadata.Title;
			Manufacturer = metadata.Manufacturer;
			Year = metadata.OriginalReleaseYear;
			Authors = metadata.PrimaryAuthors != null ? string.Join(", ", metadata.PrimaryAuthors) : null;
			Publish(log: false);
		}

		public void SetPackage(int formatVersion, string writtenBy, int componentTypes)
		{
			FormatVersion = formatVersion;
			WrittenBy = writtenBy;
			ComponentTypes = componentTypes;
			Publish(log: false);
		}

		/// <summary>
		/// Records that a step of the load finished. Ignored once the table is playable.
		/// </summary>
		public void Mark(string milestone)
		{
			if (Result != null || PlayableMs >= 0 || _awaitingFrame || _awaitingGamelogic) {
				return;
			}
			Timeline.Mark(milestone);
		}

		/// <summary>
		/// The gamelogic engine said it started (<see cref="IGamelogicEngine.OnStarted"/>), which
		/// may be after <see cref="Succeed"/>: the table is playable from the next frame on.
		/// </summary>
		public void GamelogicStarted()
		{
			if (Result != null || _gamelogicStarted) {
				return;
			}
			_gamelogicStarted = true;
			if (_awaitingGamelogic) {
				_awaitingGamelogic = false;
				Timeline.Mark("gle-started");
				_awaitingFrame = true;
			} else {
				Mark("gle-started");
			}
		}

		/// <summary>
		/// Records something observed about the load, like whether the texture cache was hit.
		/// </summary>
		public void Fact(string key, string value)
		{
			Timeline.Fact(key, value);
		}

		public void SetGameId(string gameId)
		{
			GameId = gameId;
			Publish(log: false);
		}

		/// <summary>
		/// The table finished loading and runs.
		/// </summary>
		/// <param name="waitForGamelogic">Whether the table is only playable once the gamelogic
		/// engine said it started, see <see cref="GamelogicStarted"/>.</param>
		public void Succeed(bool waitForGamelogic = false)
		{
			// a failure is final: the player may still start a table whose loading failed later
			if (FailedPhase != null) {
				return;
			}
			_loadMs = _stopwatch.ElapsedMilliseconds;
			Mark(Phase);
			Phase = PhasePlaying;
			Logger.Info("Table {Title} loaded in {LoadMs} ms", Title, _loadMs);
			Publish(log: true);
			if (Result == null) {
				_succeededAt = Time.realtimeSinceStartupAsDouble;
				if (waitForGamelogic && !_gamelogicStarted) {
					_awaitingGamelogic = true;
				} else {
					_awaitingFrame = true;
				}
			}
		}

		/// <summary>
		/// Called at the end of every frame by <see cref="PerformanceMonitor"/>: notes when the
		/// table became playable, then whether it settled.
		/// </summary>
		internal static void OnFrameEnd()
		{
			var attempt = Current;
			if (attempt == null || attempt.Result != null || !attempt._awaitingFrame && !attempt._settling && !attempt._awaitingGamelogic) {
				return;
			}
			var now = Time.realtimeSinceStartupAsDouble;
			if (attempt._awaitingGamelogic) {
				if (now - attempt._succeededAt >= SettleTimeoutSeconds) {
					Logger.Info("Table {Title} loaded, but its gamelogic engine didn't start within {Timeout} s", attempt.Title, (int)SettleTimeoutSeconds);
					attempt.Finish(ResultTimeout);
				}
				return;
			}
			if (attempt._awaitingFrame) {
				attempt.OnPlayable(now);
				return;
			}
			attempt.TrackSettling(now);
		}

		private void OnPlayable(double now)
		{
			_awaitingFrame = false;
			Timeline.Mark("first-frame");
			PlayableMs = Timeline.TotalMs;
			Timeline.Fact("ram.playable", RamAtPlayable());
			Logger.Info("Table {Title} playable after {Total:l}: {Steps:l}{Facts:l}",
				Title, Seconds(PlayableMs), Timeline.Format(minMs: 20), FormatFacts());
			_settling = true;
			_playableAt = _lastFrameAt = _windowStart = now;
			_windowFrames = 0;
			_windowMaxMs = 0;
			Publish(log: false);
		}

		private void TrackSettling(double now)
		{
			var frameMs = (float)((now - _lastFrameAt) * 1000);
			_lastFrameAt = now;
			if (Time.timeScale == 0f) {
				// paused: the frames say nothing about the table, start over afterwards
				_windowStart = now;
				_windowFrames = 0;
				_windowMaxMs = 0;
			} else {
				// beyond the buffer (fast frame rates), the newest frames replace the oldest for
				// the median, while the maximum still sees every frame
				SettleFrames[_windowFrames % MaxSettleFrames] = frameMs;
				_windowFrames++;
				if (frameMs > _windowMaxMs) {
					_windowMaxMs = frameMs;
				}
			}

			if (now - _windowStart >= SettleWindowSeconds && _windowFrames > 0) {
				if (IsStable(SettleFrames, System.Math.Min(_windowFrames, MaxSettleFrames), _windowMaxMs)) {
					SettledMs = PlayableMs + (now - _playableAt) * 1000;
					Finish(ResultSettled);
					return;
				}
				_windowStart = now;
				_windowFrames = 0;
				_windowMaxMs = 0;
			}
			if (now - _playableAt >= SettleTimeoutSeconds) {
				Finish(ResultTimeout);
			}
		}

		/// <summary>
		/// No frame took more than 1.5 times the median of the given frames. Sorts them.
		/// </summary>
		/// <param name="maxMs">The longest frame of the window, which may be one that's no
		/// longer in the buffer.</param>
		internal static bool IsStable(float[] frames, int count, float maxMs)
		{
			Array.Sort(frames, 0, count);
			var median = (count & 1) == 1 ? frames[count / 2] : (frames[count / 2 - 1] + frames[count / 2]) * 0.5f;
			return System.Math.Max(maxMs, frames[count - 1]) <= median * SettleMaxOverMedian;
		}

		private void Finish(string result)
		{
			if (Result != null) {
				return;
			}
			Result = result;
			_awaitingFrame = false;
			_awaitingGamelogic = false;
			_settling = false;
			Timeline.Fact("result", result);
			var peak = PerformanceMonitor.Last.PeakRamBytes;
			if (peak > 0 && PlayableMs >= 0) {
				Timeline.Fact("ram.peak-sampled", (peak / 1048576).ToString(CultureInfo.InvariantCulture) + " MB");
			}
			switch (result) {
				case ResultSettled:
					Logger.Info("Table {Title} settled {After:l} after it became playable", Title, Seconds(SettledMs - PlayableMs));
					break;
				case ResultTimeout:
					Logger.Info("Table {Title} didn't settle within {Timeout} s after it became playable: frame times keep jumping", Title, (int)SettleTimeoutSeconds);
					break;
				default:
					if (PlayableMs < 0) {
						Logger.Info("Table load {Result:l} after {Total:l}: {Steps:l}{Facts:l}",
							result, Seconds(_stopwatch.Elapsed.TotalMilliseconds), Timeline.Format(minMs: 20), FormatFacts());
					}
					break;
			}
			Publish(log: false);
			try {
				Finished?.Invoke(this);
			} catch (Exception e) {
				Logger.Error(e, "A table load completion handler failed");
			}
		}

		private string FormatFacts()
		{
			var facts = "";
			foreach (var fact in Timeline.Facts) {
				if (fact.Key == "ram.playable" || fact.Key == "result") {
					continue;
				}
				facts += "; " + fact.Key + " " + fact.Value;
			}
			return facts;
		}

		private static string RamAtPlayable()
		{
			try {
				using var process = Process.GetCurrentProcess();
				return (process.WorkingSet64 / 1048576).ToString(CultureInfo.InvariantCulture) + " MB";
			} catch (Exception) {
				return "n/a";
			}
		}

		private static string Seconds(double ms) => (ms / 1000).ToString("0.0", CultureInfo.InvariantCulture) + " s";

		/// <summary>
		/// Loading failed. The attempt stays current and keeps describing the table.
		/// </summary>
		public void Fail(Exception exception) => Fail(exception?.GetType().Name + ": " + exception?.Message);

		/// <summary>
		/// Loading failed for a reason the user can fix, like a missing ROM.
		/// </summary>
		public void Fail(string reason)
		{
			if (FailedPhase != null) {
				return;
			}
			FailedPhase = Phase;
			Mark(Phase);
			Phase = PhaseFailed;
			Error = reason;
			Publish(log: true);
			Finish(ResultFailed);
		}

		public void Describe(DiagnosticsWriter w)
		{
			w.Add("title", Title);
			w.Add("file", System.IO.Path.GetFileName(Path));
			w.Add("path", Path);
			w.Add("size-mb", Size / 1048576.0);
			if (Fingerprint != null) {
				w.Add("fingerprint", Fingerprint);
			}
			w.Add("phase", Phase);
			if (FailedPhase != null) {
				w.Add("failed-phase", FailedPhase);
				w.Add("error", Error);
			}
			if (Manufacturer != null) {
				w.Add("manufacturer", Manufacturer);
			}
			if (Year != null) {
				w.Add("year", Year.Value);
			}
			if (!string.IsNullOrEmpty(Authors)) {
				w.Add("authors", Authors);
			}
			if (FormatVersion != null) {
				w.Add("package.format", FormatVersion.Value);
			}
			if (WrittenBy != null) {
				w.Add("package.written-by", WrittenBy);
			}
			if (ComponentTypes != null) {
				w.Add("package.component-types", ComponentTypes.Value);
			}
			if (GameId != null) {
				w.Add("game-id", GameId);
			}
			if (_loadMs >= 0) {
				w.Add("load-ms", _loadMs);
			}
			if (PlayableMs >= 0) {
				w.Add("playable-ms", System.Math.Round(PlayableMs));
			}
			if (SettledMs >= 0) {
				w.Add("settled-ms", System.Math.Round(SettledMs));
			}
			Timeline.Describe(w, "timeline");
		}

		private void Publish(bool log)
		{
			if (Current != this) {
				return;
			}
			var writer = new DiagnosticsWriter();
			Describe(writer);
			DiagnosticsContext.Set(DiagnosticsContext.Table, writer, log);
		}

		/// <summary>
		/// Tells packages apart without hashing hundreds of megabytes: size, modification
		/// time and the first and last megabyte.
		/// </summary>
		private async void ComputeFingerprintAsync()
		{
			try {
				var path = Path;
				var fingerprint = await Task.Run(() => ComputeFingerprint(path));
				// back on the main thread (Unity's synchronization context)
				Fingerprint = fingerprint;
				Publish(log: false);
			} catch (Exception e) {
				Logger.Debug(e, "Cannot fingerprint {File:l}.", System.IO.Path.GetFileName(Path));
			}
		}

		internal static string ComputeFingerprint(string path)
		{
			const int chunk = 1 << 20;
			using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
			using var sha = SHA256.Create();
			var header = BitConverter.GetBytes(stream.Length);
			var time = BitConverter.GetBytes(File.GetLastWriteTimeUtc(path).Ticks);
			sha.TransformBlock(header, 0, header.Length, null, 0);
			sha.TransformBlock(time, 0, time.Length, null, 0);
			var buffer = new byte[chunk];
			var read = stream.Read(buffer, 0, chunk);
			sha.TransformBlock(buffer, 0, read, null, 0);
			if (stream.Length > chunk) {
				stream.Seek(System.Math.Max(chunk, stream.Length - chunk), SeekOrigin.Begin);
				read = stream.Read(buffer, 0, chunk);
				sha.TransformBlock(buffer, 0, read, null, 0);
			}
			sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
			return BitConverter.ToString(sha.Hash, 0, 6).Replace("-", "").ToLowerInvariant();
		}
	}
}
