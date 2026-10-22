// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using VisualPinball.Engine.VPT.Sound;

namespace VisualPinball.Unity.Test
{
	#if !UNITY_EDITOR
	[NonParallelizable]
	#endif
	public class ChecksumTests
	{
		private string _folder, _previousRoot;
		private long _previousQuota;
		private bool _previousCompression;
		[SetUp] public void Setup()
		{
			_folder = Path.Combine(Path.GetTempPath(), "vpe-checksum-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_folder);
			_previousRoot = VpeIntegrity.CacheRoot; VpeIntegrity.CacheRoot = Path.Combine(_folder, "cache");
			_previousQuota = VpeSharedTextureCache.QuotaBytes;
			_previousCompression = VpeTextureCache.CompressPayloadOnWrite; VpeTextureCache.CompressPayloadOnWrite = false;
		}
		[TearDown] public void Teardown()
		{
			VpeIntegrity.BytesHashed = null; VpeIntegrity.CacheRoot = _previousRoot;
			VpeSharedTextureCache.QuotaBytes = _previousQuota; VpeTextureCache.CompressPayloadOnWrite = _previousCompression;
			Directory.Delete(_folder, true);
		}

		[Test] public void Sha256MatchesPublishedAbcVector() => Assert.That(VpeIntegrity.Hash(Encoding.ASCII.GetBytes("abc")), Is.EqualTo("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"));

		[Test] public void ProvenanceMatchesIndependentBigEndianFixture()
		{
			var metadata = new TableMetadata { TableName = "Fixture", OriginalReleaseYear = 1993, Manufacturer = "Test", PrimaryAuthors = new() { new TableAuthor { Name = "Alice", VpuHandle = "alice" } } };
			var source = TableProvenance.New(); source.TableId = "11111111-1111-1111-1111-111111111111";
			var snapshot = source.Snapshot(metadata);
			Assert.That(snapshot.MetadataHash, Is.EqualTo("4b3bbd0daef65dcee9a27d06573ccd702ceb08eea2af29a40e919fc6607b3126"));
			Assert.That(snapshot.RootHash, Is.EqualTo("43d9a0171490de44f8cfc4676daa5d4c67194bcaa6506cb5e3e38d994811f4f4"));
			metadata.Abbreviation = ""; metadata.ReleaseDate = "";
			Assert.That(source.Snapshot(metadata).RootHash, Is.EqualTo(snapshot.RootHash));
			metadata.Version = "1.0.1";
			Assert.That(source.Snapshot(metadata).RootHash, Is.Not.EqualTo(snapshot.RootHash));
		}

		[Test] public void ThreeGenerationsKeepCreditsButOnlyDirectEdges()
		{
			var aMeta = new TableMetadata { PrimaryAuthors = new() { new TableAuthor { Name = "Alice" } }, Version = "1.2.0" };
			var a = TableProvenance.New().Snapshot(aMeta);
			var bMeta = new TableMetadata { PrimaryAuthors = new() { new TableAuthor { Name = "Bob" } } };
			var b = TableProvenance.Derive(a, aMeta).Snapshot(bMeta);
			var c = TableProvenance.Derive(b, bMeta).Snapshot(new TableMetadata());
			Assert.That(c.Sources, Is.EquivalentTo(new[] { b.RootHash }));
			Assert.That(c.Credits.Select(x => x.RootHash), Is.EquivalentTo(new[] { a.RootHash, b.RootHash }));
			Assert.That(c.Credits.Single(x => x.RootHash == a.RootHash).Version, Is.EqualTo("1.2.0"));
			aMeta.PrimaryAuthors[0].Name = "Edited";
			Assert.That(c.Credits.Single(x => x.RootHash == a.RootHash).PrimaryAuthors[0].Name, Is.EqualTo("Alice"));
			Assert.That(c.VerifyParent(b, bMeta), Is.True);
			c.Credits.Single(x => x.RootHash == b.RootHash).Version = "wrong";
			Assert.That(c.VerifyParent(b, bMeta), Is.False);
		}

		[Test] public void MalformedAndConflictingProvenanceFails()
		{
			Assert.Throws<InvalidDataException>(() => TableProvenance.New().Snapshot(new TableMetadata { Version = "01.0.0" }));
			var meta = new TableMetadata(); var a = TableProvenance.New().Snapshot(meta);
			var derived = TableProvenance.Derive(a, meta);
			derived.Credits.Add(derived.Credits[0]);
			Assert.Throws<InvalidDataException>(() => derived.Snapshot(meta));
			meta.TableName = "Changed";
			Assert.Throws<InvalidDataException>(() => a.Validate(meta));
		}

		[Test] public void JsonDuplicatesAndOversizedStringsFail()
		{
			Assert.Throws<Newtonsoft.Json.JsonReaderException>(() => VpeIntegrity.Parse<VpeIntegrity.Manifest>(Encoding.UTF8.GetBytes("{\"Version\":1,\"Version\":1,\"Entries\":[]}")));
			Assert.Throws<InvalidDataException>(() => TableProvenance.New().Snapshot(new TableMetadata { TableName = new string('x', 16385) }));
		}

		[Test] public void PackageFirstOpenHashesPayloadButWarmOpenDoesNot()
		{
			var path = CreatePackage();
			long hashed = 0; VpeIntegrity.BytesHashed = n => hashed += n;
			using (PackageApi.StorageManager.OpenStorage(path)) { }
			Assert.That(hashed, Is.GreaterThanOrEqualTo(1024 * 1024));
			hashed = 0;
			using (PackageApi.StorageManager.OpenStorage(path)) { }
			Assert.That(hashed, Is.LessThan(16384), "Warm loads must not rehash large payloads.");
			hashed = 0; VpeIntegrity.VerifyFile(path);
			Assert.That(hashed, Is.GreaterThanOrEqualTo(1024 * 1024));
		}

		[Test] public void ChangedPackageCannotReuseReceipt()
		{
			var path = CreatePackage(); using (PackageApi.StorageManager.OpenStorage(path)) { }
			using (var zip = ZipFile.Open(path, ZipArchiveMode.Update)) {
				zip.GetEntry("data.bin").Delete();
				using var stream = zip.CreateEntry("data.bin").Open();
				var bytes = new byte[1024 * 1024]; bytes[0] = 1; stream.Write(bytes, 0, bytes.Length);
			}
			Assert.Throws<InvalidDataException>(() => { using var storage = PackageApi.StorageManager.OpenStorage(path); });
		}

		[Test] public void HashlessPackageAndUnsafePathsAreRejected()
		{
			var path = Path.Combine(_folder, "old.vpe");
			using (var zip = ZipFile.Open(path, ZipArchiveMode.Create)) zip.CreateEntry("data.bin");
			Assert.Throws<InvalidDataException>(() => { using var storage = PackageApi.StorageManager.OpenStorage(path); });
			foreach (var name in new[] { "../x", "/x", "a\\b", "C:/x", "a//b", "a/../b", "a./b" }) Assert.Throws<InvalidDataException>(() => VpeIntegrity.ValidatePath(name), name);
		}

		[Test] public void RecipeIgnoresPackageIdsAndSamplerButTracksCookInputs()
		{
			var source = new VpeTexturePayload { Id = "a", FileName = "a.png", Width = 512, Height = 512 };
			var hash = VpeIntegrity.Hash(new byte[] { 1 });
			string Key(bool normal = false, int divisor = 1) => VpeSharedTextureCache.Key(source, hash, normal, divisor, true, "test");
			var first = Key(); source.Id = "different"; source.FileName = "b.png"; source.WrapMode = 2; source.ColorSpace = "Linear";
			Assert.That(Key(), Is.EqualTo(first)); Assert.That(Key(true), Is.Not.EqualTo(first)); Assert.That(Key(false, 2), Is.Not.EqualTo(first));
			source.GenerateMipMaps = false; Assert.That(Key(), Is.Not.EqualTo(first));
		}

		[Test] public void CookedObjectReceiptAvoidsPayloadHashAcrossReads()
		{
			var key = WriteObject();
			long hashed = 0; VpeIntegrity.BytesHashed = n => hashed += n;
			Assert.That(VpeSharedTextureCache.TryRead(key), Is.Not.Null);
			Assert.That(hashed, Is.LessThan(16384));
			hashed = 0; Assert.That(VpeSharedTextureCache.TryRead(key, true), Is.Not.Null);
			Assert.That(hashed, Is.GreaterThanOrEqualTo(1024 * 1024));
		}

		[Test] public void ExplicitCheckInvalidatesReceiptForSilentCorruption()
		{
			var key = WriteObject(); var path = VpeSharedTextureCache.ObjectPath(key);
			var stamp = File.GetLastWriteTimeUtc(path);
			using (var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite)) { stream.Position = stream.Length - 1; stream.WriteByte(99); }
			File.SetLastWriteTimeUtc(path, stamp);
			Assert.That(VpeSharedTextureCache.TryRead(key), Is.Not.Null, "Fast path intentionally cannot detect unchanged-state bit rot.");
			Assert.That(VpeSharedTextureCache.TryRead(key, true), Is.Null);
			Assert.That(VpeSharedTextureCache.TryRead(key), Is.Null, "A failed scrub must invalidate the old receipt.");
			WriteObject(); Assert.That(VpeSharedTextureCache.TryRead(key), Is.Not.Null);
		}

		[Test] public void TruncatedObjectBecomesMissAndQuotaEvictsDerivedDataOnly()
		{
			var package = CreatePackage(); var key = WriteObject();
			using (var stream = new FileStream(VpeSharedTextureCache.ObjectPath(key), FileMode.Open, FileAccess.Write)) stream.SetLength(12);
			Assert.That(VpeSharedTextureCache.TryRead(key), Is.Null);
			WriteObject(); VpeSharedTextureCache.QuotaBytes = 0; VpeSharedTextureCache.Trim();
			Assert.That(VpeSharedTextureCache.TryRead(key), Is.Null); Assert.That(File.Exists(package), Is.True);
		}

		[Test] public void ParallelObjectWritersPublishOneCompleteObject()
		{
			Task.WaitAll(Enumerable.Range(0, 4).Select(_ => Task.Run(WriteObject)).ToArray());
			Assert.That(Directory.GetFiles(VpeSharedTextureCache.Root, "*.vpto", SearchOption.AllDirectories).Length, Is.EqualTo(1));
			Assert.That(VpeSharedTextureCache.TryRead(VpeIntegrity.Hash(new byte[] { 1 }), true), Is.Not.Null);
		}

		[Test] public void PcmMatchesVpxDespiteInformationalChunksButNotSamplesOrLoops()
		{
			var format = new WaveFormat(); var samples = new byte[] { 1, 0, 2, 0 };
			var canonical = VpeAudioIdentity.Canonical(format, samples);
			Assert.That(VpeAudioIdentity.Canonical(Wav(format, samples, "LIST", Encoding.ASCII.GetBytes("INFO"))), Is.EqualTo(canonical));
			Assert.That(VpeAudioIdentity.Canonical(Wav(format, samples, "smpl", new byte[4])), Is.Null);
			samples[0]++; Assert.That(VpeAudioIdentity.Canonical(format, samples), Is.Not.EqualTo(canonical));
			format.FormatTag = 65534; Assert.That(VpeAudioIdentity.Canonical(format, samples), Is.Null);
		}

		[Test] public void WavRoundTripKeepsActualSampleFormat()
		{
			var format = new WaveFormat { Channels = 2, BitsPerSample = 24, BlockAlign = 6, AvgBytesPerSec = 44100 * 6 };
			var bytes = Wav(format, new byte[12], "JUNK", new byte[] { 1, 2 });
			Assert.That(VpeAudioIdentity.TryReadPcm(bytes, out var decoded, out var samples), Is.True);
			Assert.That(decoded.BitsPerSample, Is.EqualTo(24)); Assert.That(decoded.Channels, Is.EqualTo(2)); Assert.That(samples.Length, Is.EqualTo(12));
			Assert.That(VpeAudioIdentity.Canonical(bytes.Take(bytes.Length - 1).ToArray()), Is.Null);
		}

		[Test] public void PackageScrubFailureInvalidatesUnchangedFileReceipt()
		{
			var path = CreatePackage(); using (PackageApi.StorageManager.OpenStorage(path)) { }
			var stamp = File.GetLastWriteTimeUtc(path);
			using (var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite)) {
				using var reader = new BinaryReader(stream, Encoding.UTF8, true);
				stream.Position = 26; var nameLength = reader.ReadUInt16(); var extraLength = reader.ReadUInt16();
				stream.Position = 30 + nameLength + extraLength; stream.WriteByte(99);
			}
			File.SetLastWriteTimeUtc(path, stamp);
			using (PackageApi.StorageManager.OpenStorage(path)) { }
			Assert.Throws<InvalidDataException>(() => VpeIntegrity.VerifyFile(path));
			Assert.Throws<InvalidDataException>(() => { using var storage = PackageApi.StorageManager.OpenStorage(path); });
		}

		[Test] public void CorruptReceiptReverifiesOnlyItsObject()
		{
			var key = WriteObject();
			var receipt = Directory.GetFiles(Path.Combine(VpeIntegrity.CacheRoot, "verification", "textures"), "*.json").Single();
			File.WriteAllText(receipt, "[null,null]");
			long hashed = 0; VpeIntegrity.BytesHashed = n => hashed += n;
			Assert.That(VpeSharedTextureCache.TryRead(key), Is.Not.Null); Assert.That(hashed, Is.GreaterThanOrEqualTo(1024 * 1024));
			hashed = 0; Assert.That(VpeSharedTextureCache.TryRead(key), Is.Not.Null); Assert.That(hashed, Is.LessThan(16384));
		}

		[Test] public void CompressedObjectsAlsoSkipWarmPayloadHash()
		{
			VpeTextureCache.CompressPayloadOnWrite = true;
			var key = WriteObject(); long hashed = 0; VpeIntegrity.BytesHashed = n => hashed += n;
			var cached = VpeSharedTextureCache.TryRead(key);
			Assert.That(cached.Header.Compressed, Is.True); Assert.That(cached.Bytes.Length, Is.EqualTo(1024 * 1024)); Assert.That(hashed, Is.LessThan(16384));
		}

		[Test] public void MissingRequiredVersionAndDirectoryAliasesFail()
		{
			Assert.Throws<Newtonsoft.Json.JsonSerializationException>(() => VpeIntegrity.Parse<VpeIntegrity.Manifest>(Encoding.UTF8.GetBytes("{\"Entries\":[]}")));
			Assert.Throws<Newtonsoft.Json.JsonSerializationException>(() => VpeIntegrity.Parse<TableMetadata>(Encoding.UTF8.GetBytes("{}")));
			var path = Path.Combine(_folder, "alias.vpe");
			using (var zip = ZipFile.Open(path, ZipArchiveMode.Create)) { zip.CreateEntry("Folder/a"); zip.CreateEntry("folder/b"); }
			var error = Assert.Throws<InvalidDataException>(() => { using var storage = PackageApi.StorageManager.OpenStorage(path); });
			Assert.That(error.Message, Does.Contain("Conflicting"));
		}

		[Test] public void CanceledAdmissionNeverPublishesVerificationReceipt()
		{
			var path = CreatePackage(); using var cancellation = new System.Threading.CancellationTokenSource();
			Assert.Throws<OperationCanceledException>(() => { using var storage = PackageApi.StorageManager.OpenStorage(path, cancellation.Token, (_, _) => cancellation.Cancel()); });
			Assert.That(Directory.Exists(Path.Combine(VpeIntegrity.CacheRoot, "verification", "packages")), Is.False);
		}

		[Test] public void CanceledWarmAdmissionKeepsVerifiedReceipt()
		{
			var path = CreatePackage(); using (PackageApi.StorageManager.OpenStorage(path)) { }
			using var cancellation = new System.Threading.CancellationTokenSource(); cancellation.Cancel();
			Assert.Throws<OperationCanceledException>(() => { using var storage = PackageApi.StorageManager.OpenStorage(path, cancellation.Token); });
			long hashed = 0; VpeIntegrity.BytesHashed = n => hashed += n;
			using (PackageApi.StorageManager.OpenStorage(path)) { }
			Assert.That(hashed, Is.LessThan(16384));
		}

		[Test] public void AtomicPublicationFailurePreservesDestination()
		{
			if (Path.DirectorySeparatorChar != '\\') Assert.Ignore("Uses Windows file-share exclusion.");
			var path = Path.Combine(_folder, "existing.bin"); File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
			using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
				Assert.Throws<IOException>(() => VpeIntegrity.AtomicWrite(path, new byte[] { 4, 5, 6 }));
			Assert.That(File.ReadAllBytes(path), Is.EqualTo(new byte[] { 1, 2, 3 })); Assert.That(Directory.GetFiles(_folder, "*.tmp"), Is.Empty);
		}

		[Test] public void CacheReadBatchesPreserveMissesAndStopAtSixteenObjects()
		{
			var key = WriteObject();
			var missing = new string('a', 64);
			var keys = Enumerable.Repeat(key, 18).ToArray(); keys[1] = missing;
			long hashed = 0; VpeIntegrity.BytesHashed = bytes => hashed += bytes;
			var batch = VpeSharedTextureCache.ReadBatch(keys, 0, default);
			Assert.That(batch.Count, Is.EqualTo(16));
			Assert.That(batch[1], Is.Null);
			Assert.That(batch.Where((_, index) => index != 1).All(data => data?.Header.Key == key), Is.True);
			Assert.That(VpeSharedTextureCache.ReadBatch(keys, 16, default).Count, Is.EqualTo(2));
			Assert.That(hashed, Is.LessThan(32768), "Warm batches must not hash texture payloads.");
		}

		[Test] public void CacheReadBatchBudgetUsesExpandedBytes()
		{
			VpeTextureCache.CompressPayloadOnWrite = true;
			var key = VpeIntegrity.Hash(new byte[] { 2 });
			VpeSharedTextureCache.Write(key, new VpeTexturePayload { Width = 2048, Height = 2048, MipCount = 1, PixelFormat = VpePixelFormats.Rgba32 }, new byte[16 * 1024 * 1024], false);
			Assert.That(new FileInfo(VpeSharedTextureCache.ObjectPath(key)).Length, Is.LessThan(1024 * 1024));
			var batch = VpeSharedTextureCache.ReadBatch(new[] { key, key, key }, 0, default);
			Assert.That(batch.Count, Is.EqualTo(2), "Stop at 32 MiB even when compressed files are tiny.");
			Assert.That(batch.Sum(data => data.Bytes.LongLength), Is.EqualTo(32L * 1024 * 1024));
		}

		[Test] public void CanceledCacheReadBatchDoesNotOpenObjects()
		{
			using var cancellation = new System.Threading.CancellationTokenSource(); cancellation.Cancel();
			long hashed = 0; VpeIntegrity.BytesHashed = bytes => hashed += bytes;
			Assert.Throws<OperationCanceledException>(() => VpeSharedTextureCache.ReadBatch(new[] { new string('a', 64) }, 0, cancellation.Token));
			Assert.That(hashed, Is.Zero);
		}

		private string CreatePackage()
		{
			var path = Path.Combine(_folder, "table.vpe");
			using var storage = PackageApi.StorageManager.CreateStorage(path);
			storage.AddFile("data.bin").SetData(new byte[1024 * 1024], PackageCompression.Stored);
			using (var stream = storage.AddFile("stream.txt").AsStream()) { var bytes = Encoding.UTF8.GetBytes("streamed entry"); stream.Write(bytes, 0, bytes.Length); }
			return path;
		}
		private string WriteObject()
		{
			var key = VpeIntegrity.Hash(new byte[] { 1 });
			VpeSharedTextureCache.Write(key, new VpeTexturePayload { Width = 512, Height = 512, MipCount = 1, PixelFormat = VpePixelFormats.Rgba32, MimeType = "application/octet-stream" }, new byte[1024 * 1024], false);
			return key;
		}
		private static byte[] Wav(WaveFormat format, byte[] samples, string chunk, byte[] extra)
		{
			using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
			writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(0); writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
			writer.Write(format.FormatTag); writer.Write(format.Channels); writer.Write(format.SamplesPerSec); writer.Write(format.AvgBytesPerSec); writer.Write(format.BlockAlign); writer.Write(format.BitsPerSample);
			writer.Write(Encoding.ASCII.GetBytes(chunk)); writer.Write(extra.Length); writer.Write(extra); if ((extra.Length & 1) != 0) writer.Write((byte)0);
			writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(samples.Length); writer.Write(samples); if ((samples.Length & 1) != 0) writer.Write((byte)0);
			stream.Position = 4; writer.Write((int)stream.Length - 8); writer.Flush(); return stream.ToArray();
		}
	}
}
