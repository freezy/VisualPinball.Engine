// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using VisualPinball.Plugins;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Identifies the game in a table package without loading the table, for per-game plugin settings and for plugins that look for
	/// matching media.
	/// </summary>
	/// <remarks>
	/// The Player uses it when the loaded table's gamelogic engine does not implement <see cref="IGameIdentityProvider"/>, and its
	/// plugin settings page uses it to open the profile of the game selected in the library before anything is loaded. The
	/// package's components are handed to the registered <see cref="IPackagedGameIdentityReader"/>s (see
	/// <see cref="PackagedGameIdentityReaders"/>); PinMAME's reader yields the ROM name. When no reader finds an identity, the game
	/// is identified by the SHA-256 hash of the package file (provider <c>package</c>). That identity survives moving or renaming
	/// the file, but changes when its content changes, for example when the table is exported again, which detaches it from the
	/// settings saved for the old hash.
	/// <para>
	/// Results are cached per full path and read again when the file's size or last write time changes.
	/// </para>
	/// <example>
	/// <code>
	/// var identity = (engine as IGameIdentityProvider)?.GameIdentity ?? await PackagedGameInformation.ReadAsync(path, cancellation);
	/// </code>
	/// </example>
	/// </remarks>
	public static class PackagedGameInformation
	{
		private sealed class Cached
		{
			internal long Size;
			internal DateTime Modified;
			internal GameIdentity Identity;
		}
		// Ordinal on every platform: Linux paths are case-sensitive, and elsewhere a differently cased path only costs a second read.
		private static readonly Dictionary<string, Cached> Cache = new Dictionary<string, Cached>(StringComparer.Ordinal);
		/// <summary>
		/// Reads the game identity of a table package on a worker thread. Safe to call from any thread, also concurrently. The
		/// identity readers run on the worker thread.
		/// </summary>
		/// <param name="path">Path of the table package; a relative path is resolved against the current directory.</param>
		/// <param name="cancellation">Cancels the read, including hashing a large package.</param>
		/// <returns>
		/// The single identity found by the registered readers, or a <c>package</c> identity whose key is the lowercase hexadecimal
		/// SHA-256 hash of the file. Errors opening or reading the file fault the task.
		/// </returns>
		/// <exception cref="InvalidDataException">The readers found more than one distinct identity in the package.</exception>
		/// <exception cref="OperationCanceledException"><paramref name="cancellation"/> was cancelled.</exception>
		public static Task<GameIdentity> ReadAsync(string path, CancellationToken cancellation = default) => Task.Run(() => Read(path, cancellation), cancellation);
		private static GameIdentity Read(string path, CancellationToken cancellation)
		{
			path = Path.GetFullPath(path);
			var file = new FileInfo(path);
			lock (Cache) if (Cache.TryGetValue(path, out var cached) && cached.Size == file.Length && cached.Modified == file.LastWriteTimeUtc) return cached.Identity;
			var found = new HashSet<GameIdentity>();
			using (var storage = PackageApi.StorageManager.OpenStorage(path)) {
				var table = storage.GetFolder(PackageApi.TableFolder);
				if (table.TryGetFolder("items", out var items)) items.VisitFolders(node => node.VisitFolders(component => {
					cancellation.ThrowIfCancellationRequested();
					var reader = PackagedGameIdentityReaders.Find(component.Name);
					if (reader == null) return;
					component.VisitFiles(payload => { var identity = reader.Read(payload.GetData()); if (identity.HasValue) found.Add(identity.Value); });
				}));
			}
			if (found.Count > 1) throw new InvalidDataException("The table has multiple controller identities. Select a controller profile explicitly.");
			GameIdentity result;
			if (found.Count == 1) { using var iterator = found.GetEnumerator(); iterator.MoveNext(); result = iterator.Current; }
			else {
				using var stream = File.OpenRead(path); using var sha = SHA256.Create();
				var buffer = new byte[64 * 1024]; int read;
				while ((read = stream.Read(buffer, 0, buffer.Length)) > 0) { cancellation.ThrowIfCancellationRequested(); sha.TransformBlock(buffer, 0, read, buffer, 0); }
				sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
				result = new GameIdentity("package", BitConverter.ToString(sha.Hash).Replace("-", "").ToLowerInvariant());
			}
			lock (Cache) Cache[path] = new Cached { Size = file.Length, Modified = file.LastWriteTimeUtc, Identity = result };
			return result;
		}
	}
}
