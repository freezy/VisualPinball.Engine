// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace VisualPinball.Unity
{
	/// <summary>A compact, offline credit index. Source roots commit to deeper paths without embedding their graphs.</summary>
	[Serializable]
	public sealed class TableProvenance
	{
		public const string FileName = "provenance.json";
		public const int MaxBytes = 8 * 1024 * 1024;
		[JsonProperty(Required = Required.Always)] public int SchemaVersion = 1;
		[JsonProperty(Required = Required.Always)] public string TableId;
		[JsonProperty(Required = Required.Always)] public string Kind;
		public string SourceFingerprint = "";
		public string MetadataHash;
		public string CreditIndexHash;
		public string RootHash;
		[JsonProperty(Required = Required.Always)] public List<string> Sources = new();
		[JsonProperty(Required = Required.Always)] public List<Credit> Credits = new();

		[Serializable]
		public sealed class Credit
		{
			public string RootHash;
			[JsonProperty(Required = Required.Always)] public string TableId;
			public string Version;
			public List<TableAuthor> PrimaryAuthors = new();
			public List<TableAuthor> SecondaryAuthors = new();
			[JsonProperty(Required = Required.Always)] public string Kind;
			public string SourceFingerprint;
		}

		public static TableProvenance New(string kind = "native") => new() { TableId = Guid.NewGuid().ToString("D"), Kind = kind };
		public TableProvenance Copy() => VpeIntegrity.Parse<TableProvenance>(VpeIntegrity.Json(this), MaxBytes);

		public static TableMetadata CopyMetadata(TableMetadata metadata)
		{
			var copy = PackageApi.Packer.Unpack<TableMetadata>(PackageApi.Packer.Pack(metadata));
			// Unity references are excluded from portable metadata, but derivatives retain them.
			copy.BackglassImage = metadata.BackglassImage;
			return copy;
		}

		public TableProvenance Snapshot(TableMetadata metadata)
		{
			if (metadata == null || !ValidVersion(metadata.Version)) throw new InvalidDataException("Table version must be major.minor.patch, with an optional v prefix, -prerelease and +build suffixes (for example v1.0.0-alpha1+build.1).");
			var value = Copy();
			value.ValidateRecords();
			value.Sources.Sort(StringComparer.Ordinal);
			value.Credits.Sort((a, b) => StringComparer.Ordinal.Compare(a.RootHash, b.RootHash));
			value.MetadataHash = Digest("VPE-METADATA", w => {
				Text(w, metadata.Version); Text(w, metadata.TableName); Text(w, metadata.Abbreviation);
				Authors(w, metadata.PrimaryAuthors); Authors(w, metadata.SecondaryAuthors);
				Text(w, metadata.ReleaseDate); Number(w, metadata.OriginalReleaseYear); Text(w, metadata.Manufacturer);
			});
			value.CreditIndexHash = Digest("VPE-CREDITS", w => {
				Number(w, value.Credits.Count);
				foreach (var c in value.Credits) { Hash(w, c.RootHash); Text(w, c.TableId); Text(w, c.Version); Authors(w, c.PrimaryAuthors); Authors(w, c.SecondaryAuthors); Text(w, c.Kind); Text(w, c.SourceFingerprint); }
			});
			value.RootHash = Digest("VPE-PROVENANCE-NODE", w => {
				Text(w, value.TableId); Text(w, value.Kind); Text(w, value.SourceFingerprint);
				Hash(w, value.MetadataHash); Hash(w, value.CreditIndexHash); Number(w, value.Sources.Count);
				foreach (var hash in value.Sources) Hash(w, hash);
			});
			if (value.Credits.Any(c => c.RootHash == value.RootHash)) throw new InvalidDataException("Self-referencing provenance.");
			if (VpeIntegrity.Json(value).Length > MaxBytes) throw new InvalidDataException("Provenance exceeds its size limit.");
			return value;
		}

		public void Validate(TableMetadata metadata)
		{
			var expected = Snapshot(metadata);
			if (RootHash != expected.RootHash || MetadataHash != expected.MetadataHash || CreditIndexHash != expected.CreditIndexHash)
				throw new InvalidDataException("Table provenance checksum does not match its metadata/credits.");
		}

		private void ValidateRecords()
		{
			if (SchemaVersion != 1 || !ValidId(TableId) || Kind != "native" && Kind != "derived" && Kind != "legacyScene" && Kind != "diagnostic") throw new InvalidDataException("Invalid provenance identity/schema.");
			if (!string.IsNullOrEmpty(SourceFingerprint)) throw new InvalidDataException("Current tables cannot have synthetic identities.");
			Sources ??= new(); Credits ??= new();
			if (Sources.Count > 256 || Credits.Count > 4096 || Sources.Distinct(StringComparer.Ordinal).Count() != Sources.Count) throw new InvalidDataException("Too many or duplicate source records.");
			var seen = new HashSet<string>(StringComparer.Ordinal);
			var authorCount = 0;
			foreach (var c in Credits) {
				if (c == null || !VpeIntegrity.IsHash(c.RootHash) || !seen.Add(c.RootHash)) throw new InvalidDataException("Invalid/duplicate ancestor hash.");
				if (c.Kind == "vpx" || c.Kind == "memoryImport") {
					if (!string.IsNullOrEmpty(c.TableId) || string.IsNullOrEmpty(c.SourceFingerprint)) throw new InvalidDataException("Invalid legacy identity.");
					if (c.Kind == "vpx" && !VpeIntegrity.IsHash(c.SourceFingerprint)) throw new InvalidDataException("Invalid VPX fingerprint.");
					if (LegacyHash(c) != c.RootHash) throw new InvalidDataException("Invalid legacy source hash.");
				} else if (!ValidVersion(c.Version) || !ValidId(c.TableId) || c.Kind != "native" && c.Kind != "derived" && c.Kind != "legacyScene" || !string.IsNullOrEmpty(c.SourceFingerprint)) throw new InvalidDataException("Invalid ancestor identity.");
				authorCount += (c.PrimaryAuthors?.Count ?? 0) + (c.SecondaryAuthors?.Count ?? 0);
			}
			if (authorCount > 32768 || Sources.Any(s => !seen.Contains(s)) || Kind == "derived" && Sources.Count == 0 || Kind == "native" && (Sources.Count != 0 || Credits.Count != 0)) throw new InvalidDataException("Invalid provenance sources/credits.");
		}

		public static TableProvenance Derive(TableProvenance source, TableMetadata metadata)
		{
			source.Validate(metadata);
			if (source.Kind == "diagnostic") throw new InvalidDataException("A diagnostic import is not a complete table.");
			var result = New("derived");
			result.Credits = source.Copy().Credits;
			result.Credits.Add(new Credit { RootHash = source.RootHash, TableId = source.TableId, Version = metadata.Version, PrimaryAuthors = CloneAuthors(metadata.PrimaryAuthors), SecondaryAuthors = CloneAuthors(metadata.SecondaryAuthors), Kind = source.Kind, SourceFingerprint = source.SourceFingerprint });
			result.Sources.Add(source.RootHash);
			result.ValidateRecords();
			return result;
		}

		public static TableProvenance FromVpx(string fingerprint, string version, string author)
		{
			var credit = new Credit { TableId = "", Version = version ?? "", Kind = fingerprint == null ? "memoryImport" : "vpx", SourceFingerprint = fingerprint ?? Guid.NewGuid().ToString("D") };
			if (!string.IsNullOrEmpty(author)) credit.PrimaryAuthors.Add(new TableAuthor { Name = author, VpuHandle = "" });
			credit.RootHash = LegacyHash(credit);
			var value = New("derived"); value.Sources.Add(credit.RootHash); value.Credits.Add(credit);
			return value;
		}

		public static TableProvenance Read(IPackageFolder table, out TableMetadata metadata)
		{
			using (var stream = table.GetFile(PackageApi.TableMetadataFile, PackageApi.Packer.FileExtension).AsStream())
				metadata = VpeIntegrity.Parse<TableMetadata>(VpeIntegrity.ReadBounded(stream, MaxBytes), MaxBytes);
			using var source = table.GetFile(FileName).AsStream();
			var value = VpeIntegrity.Parse<TableProvenance>(VpeIntegrity.ReadBounded(source, MaxBytes), MaxBytes);
			value.Validate(metadata);
			return value;
		}

		/// <summary>Verifies a supplied parent's captured credits. No network request or stored-record mutation.</summary>
		public bool VerifyParent(TableProvenance parent, TableMetadata metadata)
		{
			parent.Validate(metadata);
			var record = Credits.SingleOrDefault(c => c.RootHash == parent.RootHash);
			if (record == null) return false;
			var expected = Derive(parent, metadata).Credits;
			return expected.All(c => Credits.Any(existing => existing.RootHash == c.RootHash && CreditDigest(existing) == CreditDigest(c)));
		}

		private static string CreditDigest(Credit c) => Digest("VPE-CREDIT-RECORD", w => { Hash(w, c.RootHash); Text(w, c.TableId); Text(w, c.Version); Authors(w, c.PrimaryAuthors); Authors(w, c.SecondaryAuthors); Text(w, c.Kind); Text(w, c.SourceFingerprint); });

		private static bool ValidVersion(string version) => version != null && version.Length <= 128 && Regex.IsMatch(version,
			@"\Av?(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)" +
			@"(?:-(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)(?:\.(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*))*)?" +
			@"(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?\z");
		private static bool ValidId(string id) => Guid.TryParseExact(id, "D", out var guid) && guid != Guid.Empty && id == guid.ToString("D");
		private static List<TableAuthor> CloneAuthors(List<TableAuthor> authors) =>
			(authors ?? new()).Select(author => author == null ? throw new InvalidDataException("Null author.") : new TableAuthor {
				Name = author.Name ?? "",
				VpuHandle = author.VpuHandle ?? "",
				Roles = (author.Roles ?? new()).Select(role => role == null ? throw new InvalidDataException("Null author role.") : new TableAuthorRole {
					Title = role.Title,
					Description = role.Description
				}).ToList()
			}).ToList();
		private static string LegacyHash(Credit c) => Digest("VPE-LEGACY-SOURCE", w => { Text(w, c.Kind); Text(w, c.SourceFingerprint); Text(w, c.Version); Authors(w, c.PrimaryAuthors); Authors(w, c.SecondaryAuthors); });
		private static string Digest(string domain, Action<BinaryWriter> write)
		{
			using var stream = new MemoryStream();
			using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
			writer.Write(Encoding.ASCII.GetBytes(domain)); writer.Write((byte)0); Number(writer, 1); write(writer); writer.Flush();
			return VpeIntegrity.Hash(stream.ToArray());
		}
		private static void Number(BinaryWriter writer, int value) { writer.Write((byte)(value >> 24)); writer.Write((byte)(value >> 16)); writer.Write((byte)(value >> 8)); writer.Write((byte)value); }
		private static void Text(BinaryWriter writer, string value) { var bytes = new UTF8Encoding(false, true).GetBytes(value ?? ""); if (bytes.Length > 16384) throw new InvalidDataException("Provenance string too long."); Number(writer, bytes.Length); writer.Write(bytes); }
		private static void Hash(BinaryWriter writer, string value) { if (!VpeIntegrity.IsHash(value)) throw new InvalidDataException("Invalid SHA-256."); for (var i = 0; i < 64; i += 2) writer.Write(Convert.ToByte(value.Substring(i, 2), 16)); }
		private static void Authors(BinaryWriter writer, List<TableAuthor> authors)
		{
			var count = authors?.Count ?? 0;
			if (count > 32768) throw new InvalidDataException("Too many authors.");

			// A negative count marks author lists that include roles, preserving the hashes of existing credits without roles.
			var hasRoles = authors?.Any(author => author?.Roles?.Count > 0) ?? false;
			Number(writer, hasRoles ? -count : count);
			if (authors == null) return;

			foreach (var author in authors) {
				if (author == null) throw new InvalidDataException("Null author.");
				Text(writer, author.Name);
				Text(writer, author.VpuHandle);
				if (!hasRoles) continue;

				Number(writer, author.Roles?.Count ?? 0);
				if (author.Roles == null) continue;
				foreach (var role in author.Roles) {
					if (string.IsNullOrWhiteSpace(role?.Title)) throw new InvalidDataException("Each author role must have a title.");
					Text(writer, role.Title);
					Text(writer, role.Description);
				}
			}
		}
	}
}
