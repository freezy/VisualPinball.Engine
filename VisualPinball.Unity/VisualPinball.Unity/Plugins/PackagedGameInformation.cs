// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using VisualPinball.Plugins;

namespace VisualPinball.Unity
{
	/// <summary>Reads provider identities without restoring a scene; content hashes keep fallback identities stable across moves.</summary>
	public static class PackagedGameInformation
	{
		private sealed class Cached
		{
			internal long Size;
			internal DateTime Modified;
			internal GameIdentity Identity;
		}
		private static readonly Dictionary<string, Cached> Cache = new Dictionary<string, Cached>(StringComparer.OrdinalIgnoreCase);
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
