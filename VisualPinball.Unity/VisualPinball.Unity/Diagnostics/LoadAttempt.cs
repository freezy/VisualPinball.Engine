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
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using NLog;
using VisualPinball.Plugins;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	/// <summary>
	/// The table being loaded or played, and how far loading got.
	/// </summary>
	///
	/// <remarks>
	/// Created before anything is read, so a report about a table that failed to load still
	/// says which table it was and in which phase it failed, also after the plugins that knew
	/// more have been torn down. Main thread only.
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

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// The current attempt, or null when no table was loaded in this session.
		/// </summary>
		public static LoadAttempt Current { get; private set; }

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

		private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
		private long _loadMs = -1;

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
		}

		/// <summary>
		/// Starts tracking a table load.
		/// </summary>
		public static LoadAttempt Begin(string path, string title)
		{
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
		}

		public void SetPhase(string phase)
		{
			if (FailedPhase != null || Phase == phase) {
				return;
			}
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

		public void SetGameId(string gameId)
		{
			GameId = gameId;
			Publish(log: false);
		}

		/// <summary>
		/// The table finished loading and runs.
		/// </summary>
		public void Succeed()
		{
			// a failure is final: the player may still start a table whose loading failed later
			if (FailedPhase != null) {
				return;
			}
			_loadMs = _stopwatch.ElapsedMilliseconds;
			Phase = PhasePlaying;
			Logger.Info("Table {Title} loaded in {LoadMs} ms", Title, _loadMs);
			Publish(log: true);
		}

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
			Phase = PhaseFailed;
			Error = reason;
			Publish(log: true);
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
