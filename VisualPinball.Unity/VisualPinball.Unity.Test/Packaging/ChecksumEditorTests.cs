// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using VisualPinball.Engine.VPT.Sound;
using VisualPinball.Unity.Editor;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Test
{
	#if !UNITY_EDITOR
	[NonParallelizable]
	#endif
	public class ChecksumEditorTests
	{
		private string _folder, _cache, _previousCache;
		[SetUp] public void Setup()
		{
			_folder = "Assets/ChecksumTest-" + Guid.NewGuid().ToString("N"); Directory.CreateDirectory(_folder);
			_cache = Path.Combine(Path.GetTempPath(), "vpe-editor-checksum-" + Guid.NewGuid().ToString("N"));
			_previousCache = VpeIntegrity.CacheRoot; VpeIntegrity.CacheRoot = _cache;
			AssetDatabase.Refresh();
		}
		[TearDown] public void Teardown()
		{
			VpeIntegrity.CacheRoot = _previousCache;
			AssetDatabase.DeleteAsset(_folder);
			if (Directory.Exists(_cache)) Directory.Delete(_cache, true);
			Undo.ClearAll();
		}

		[Test] public void SceneSerializationAndUndoRetainIdentityAndDefensiveCredits()
		{
			var go = new GameObject("Checksum table");
			try {
				var table = go.AddComponent<TableComponent>();
				var metadata = new TableMetadata { Version = "2.1.0", PrimaryAuthors = new() { new TableAuthor { Name = "Alice" } } };
				var source = TableProvenance.New().Snapshot(metadata);
				table.RestoreProvenance(TableProvenance.Derive(source, metadata));
				var id = table.Provenance.TableId;
				var view = table.Provenance; view.Credits.Clear();
				Assert.That(table.Provenance.Credits.Count, Is.EqualTo(1));
				var json = EditorJsonUtility.ToJson(table);
				Undo.RecordObject(table, "Change identity for test"); table.RestoreProvenance(TableProvenance.New()); Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
				Assert.That(table.Provenance.TableId, Is.EqualTo(id));
				table.RestoreProvenance(TableProvenance.New()); EditorJsonUtility.FromJsonOverwrite(json, table);
				Assert.That(table.Provenance.TableId, Is.EqualTo(id)); Assert.That(table.Provenance.Credits.Single().Version, Is.EqualTo("2.1.0"));
			} finally { Object.DestroyImmediate(go); }
		}

		[Test] public void MetadataCopyKeepsArtworkWithoutSharingMutableCredits()
		{
			var image = new Texture2D(2, 2);
			try {
				var original = new TableMetadata { BackglassImage = image, PrimaryAuthors = new() { new TableAuthor { Name = "Alice" } } };
				var copy = TableProvenance.CopyMetadata(original);
				Assert.That(copy.BackglassImage, Is.SameAs(image));
				copy.PrimaryAuthors[0].Name = "Bob";
				Assert.That(original.PrimaryAuthors[0].Name, Is.EqualTo("Alice"));
			} finally { Object.DestroyImmediate(image); }
		}

		[Test] public void ImportedAudioMatchesContentAndSettingsAndKeepsVpxName()
		{
			var bytes = Wav(); var path = _folder + "/library-name.wav"; File.WriteAllBytes(path, bytes); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
			var importer = (AudioImporter)AssetImporter.GetAtPath(path);
			var intent = VpeAudioAssetIndex.DefaultIntent(); VpeAudioAssetIndex.Apply(importer, intent); importer.SaveAndReimport();
			var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
			VpeAudioAssetIndex.RegisterImported(clip);
			intent = VpeAudioAssetIndex.Capture((AudioImporter)AssetImporter.GetAtPath(path), Guid.NewGuid().ToString("N"));
			Assert.That(VpeAudioAssetIndex.Resolve(bytes, intent), Is.EqualTo(clip));
			Assert.That(VpeAudioIdentity.TryReadPcm(bytes, out var format, out var samples), Is.True);
			Assert.That(VpeAudioAssetIndex.Resolve(samples, intent, VpeAudioIdentity.Canonical(format, samples), false), Is.EqualTo(clip));
			var legacy = new LegacySound(new SoundData("original-vpx-name") { Wfx = format, Data = samples, Path = "original.wav" }, clip);
			Assert.That(legacy.Name, Is.EqualTo("original-vpx-name"));
			Assert.That(legacy.ToSound().Data.Name, Is.EqualTo("original-vpx-name"));
			Assert.That(legacy.ToSound().Data.Data, Is.EqualTo(samples));
			intent.ForceToMono = !intent.ForceToMono;
			Assert.That(VpeAudioAssetIndex.Resolve(bytes, intent), Is.Null);
		}

		[TestCase(8)] [TestCase(24)]
		public void OddLengthVpxWavReusesAndExportsExactSamples(int bits)
		{
			var format = new WaveFormat { Channels = 1, BitsPerSample = (ushort)bits, BlockAlign = (ushort)(bits / 8), AvgBytesPerSec = (uint)(44100 * bits / 8) };
			var samples = Enumerable.Range(0, 3 * bits / 8).Select(i => (byte)(128 + i)).ToArray();
			var data = new SoundData("odd-vpx-name") { Wfx = format, Data = samples, Path = "odd.wav" };
			var bytes = data.GetFileData();
			Assert.That(VpeAudioIdentity.TryReadPcm(bytes, out var decoded, out var decodedSamples), Is.True);
			Assert.That(decodedSamples, Is.EqualTo(samples));
			var path = _folder + "/odd.wav"; File.WriteAllBytes(path, bytes); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
			var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path); Assert.That(clip, Is.Not.Null); VpeAudioAssetIndex.RegisterImported(clip);
			var intent = VpeAudioAssetIndex.Capture((AudioImporter)AssetImporter.GetAtPath(path), AssetDatabase.AssetPathToGUID(path));
			Assert.That(VpeAudioAssetIndex.Resolve(samples, intent, VpeAudioIdentity.Canonical(format, samples), false), Is.EqualTo(clip));
			var exported = new LegacySound(data, clip).ToSound().Data;
			Assert.That(exported.Data, Is.EqualTo(samples)); Assert.That(exported.Wfx.BitsPerSample, Is.EqualTo(bits));
		}

		[Test] public void VpeImportReusesMatchingAudioWithoutCopying()
		{
			var bytes = Wav(); var path = _folder + "/source.wav"; File.WriteAllBytes(path, bytes); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
			var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path); VpeAudioAssetIndex.RegisterImported(clip);
			var meta = VpeAudioAssetIndex.Capture((AudioImporter)AssetImporter.GetAtPath(path), Guid.NewGuid().ToString("N"));
			var package = Path.Combine(_cache, "audio.vpe"); Directory.CreateDirectory(_cache);
			using (var storage = PackageApi.StorageManager.CreateStorage(package)) {
				var table = storage.AddFolder(PackageApi.TableFolder);
				table.AddFolder(PackageApi.SoundFolder).AddFile("different-name.wav").SetData(bytes);
				table.AddFolder(PackageApi.MetaFolder).AddFile(PackageApi.SoundFolder, PackageApi.Packer.FileExtension).SetData(PackageApi.Packer.Pack(new Dictionary<string, SoundMetaPackable> { ["different-name.wav"] = meta }));
			}
			var go = new GameObject("Audio import");
			try {
				using var storage = PackageApi.StorageManager.OpenStorage(package);
				var files = new PackagedFiles(storage.GetFolder(PackageApi.TableFolder), new PackagedRefs(go.transform));
				files.UnpackSounds(Path.GetFullPath(_folder + "/Imported"), VpeIntegrity.ReadManifest(storage));
				Assert.That(files.GetAudioClip(meta.Guid), Is.EqualTo(clip));
				Assert.That(Directory.Exists(_folder + "/Imported/Sounds"), Is.False);
			} finally { Object.DestroyImmediate(go); }
		}

		[Test] public void PackageAudioSourcesResolveAndMatchWithoutChangingLibrary()
		{
			var path = AssetDatabase.FindAssets("t:AudioClip").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => p.StartsWith("Packages/", StringComparison.Ordinal));
			if (path == null) Assert.Ignore("This project has no package audio fixture.");
			var physical = VpeAudioAssetIndex.SourcePath(path);
			Assert.That(File.Exists(physical), Is.True);
			var original = File.ReadAllBytes(physical);
			var meta = File.ReadAllBytes(physical + ".meta");
			var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
			var intent = VpeAudioAssetIndex.Capture((AudioImporter)AssetImporter.GetAtPath(path), AssetDatabase.AssetPathToGUID(path));
			VpeAudioAssetIndex.Rebuild();
			Assert.That(VpeAudioAssetIndex.Resolve(original, intent), Is.EqualTo(clip));
			Assert.That(File.ReadAllBytes(physical), Is.EqualTo(original));
			Assert.That(File.ReadAllBytes(physical + ".meta"), Is.EqualTo(meta));
		}

		[UnityTest] public IEnumerator FullExportRuntimeAndEditorImportKeepDifferentIdentitySemantics()
		{
			var source = new GameObject("Checksum export"); GameObject runtime = null; GameObject imported = null;
			var name = "ChecksumRoundtrip-" + Guid.NewGuid().ToString("N");
			source.name = name;
			Directory.CreateDirectory(_cache); var path = Path.Combine(_cache, "source.vpe");
			try {
				var table = source.AddComponent<TableComponent>(); table.Metadata = new TableMetadata { TableName = "Original", Version = "1.2.0", PrimaryAuthors = new() { new TableAuthor { Name = "Alice" } } }; table.RestoreProvenance(TableProvenance.New());
				var export = new PackageWriter(source).WritePackageAsync(path); while (!export.IsCompleted) yield return null; export.GetAwaiter().GetResult();
				var original = table.Provenance.Snapshot(table.Metadata);
				var load = new RuntimePackageReader(path).ImportIntoScene(); while (!load.IsCompleted) yield return null; runtime = load.GetAwaiter().GetResult();
				Assert.That(runtime.GetComponent<TableComponent>().Provenance.TableId, Is.EqualTo(original.TableId));
				var import = new PackageReader(path).ImportIntoScene(name); while (!import.IsCompleted) yield return null; import.GetAwaiter().GetResult();
				imported = Object.FindObjectsByType<TableComponent>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(t => t != table && t.gameObject != runtime).gameObject;
				var derivative = imported.GetComponent<TableComponent>();
				Assert.That(derivative.Provenance.TableId, Is.Not.EqualTo(original.TableId)); Assert.That(derivative.Metadata.Version, Is.EqualTo("1.0.0"));
				Assert.That(derivative.Provenance.Credits.Single().Version, Is.EqualTo("1.2.0"));
				var reexport = new PackageWriter(imported).WritePackageAsync(Path.Combine(_cache, "derived.vpe")); while (!reexport.IsCompleted) yield return null; reexport.GetAwaiter().GetResult();
				using var storage = PackageApi.StorageManager.OpenStorage(Path.Combine(_cache, "derived.vpe"));
				var restored = TableProvenance.Read(storage.GetFolder(PackageApi.TableFolder), out var metadata);
				Assert.That(restored.TableId, Is.EqualTo(derivative.Provenance.TableId)); Assert.That(restored.VerifyParent(original, table.Metadata), Is.True);
			} finally {
				if (runtime) Object.DestroyImmediate(runtime); if (imported) Object.DestroyImmediate(imported); Object.DestroyImmediate(source);
				AssetDatabase.DeleteAsset("Assets/Resources/" + name);
			}
		}

		[UnityTest] public IEnumerator MaterialsOnlyImportCannotBeExportedAsCompleteTable()
		{
			var name = "ChecksumDiagnostic-" + Guid.NewGuid().ToString("N");
			var source = new GameObject(name); GameObject diagnostic = null;
			Directory.CreateDirectory(_cache); var path = Path.Combine(_cache, "source.vpe");
			try {
				var table = source.AddComponent<TableComponent>(); table.RestoreProvenance(TableProvenance.New());
				var export = new PackageWriter(source).WritePackageAsync(path); while (!export.IsCompleted) yield return null; export.GetAwaiter().GetResult();
				var import = new PackageReader(path).ImportMaterialsOnly(name); while (!import.IsCompleted) yield return null; import.GetAwaiter().GetResult();
				var component = Object.FindObjectsByType<TableComponent>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(t => t != table);
				diagnostic = component.gameObject;
				Assert.That(component.Provenance.Kind, Is.EqualTo("diagnostic"));
				var refused = new PackageWriter(diagnostic).WritePackageAsync(Path.Combine(_cache, "refused.vpe")); while (!refused.IsCompleted) yield return null;
				Assert.Throws<InvalidOperationException>(() => refused.GetAwaiter().GetResult());
				Assert.That(File.Exists(Path.Combine(_cache, "refused.vpe")), Is.False);
			} finally {
				if (diagnostic) Object.DestroyImmediate(diagnostic); Object.DestroyImmediate(source);
				AssetDatabase.DeleteAsset("Assets/Resources/" + name);
			}
		}

		[UnityTest] public IEnumerator SharedTextureWarmLoadAvoidsBulkHashAndKeepsSamplerState()
		{
			var input = new Texture2D(256, 256, TextureFormat.RGBA32, false);
			var colors = new Color32[256 * 256]; var random = new System.Random(512);
			for (var i = 0; i < colors.Length; i++) colors[i] = new Color32((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256), 255);
			input.SetPixels32(colors); input.Apply(); var png = input.EncodeToPNG(); Object.DestroyImmediate(input);
			Directory.CreateDirectory(_cache); var path = Path.Combine(_cache, "texture.vpe");
			using (var output = PackageApi.StorageManager.CreateStorage(path)) output.AddFolder("table").AddFolder("textures").AddFile("noise.png").SetData(png);
			VpeMaterialsPayload Payload(string id, string wrap) => new() {
				Textures = new[] { new VpeTexture { Id = id, FileName = "noise.png", Width = 256, Height = 256, WrapMode = wrap, RuntimeCompress = false } },
				Profiles = new[] { new VpeMaterialProfile { Id = "material", Type = VpeMaterialTypes.Lit, Lit = new VpeLitProfile { BaseColor = new VpeColorAndTexture { Texture = new VpeTextureRef { TextureId = id } } } } }
			};
			using var storage = PackageApi.StorageManager.OpenStorage(path);
			var manifest = VpeIntegrity.ReadManifest(storage);
			Dictionary<string, Texture2D> cold = null, warm = null;
			try {
				var first = VpeSharedTextureCache.LoadAsync(storage.GetFolder("table"), Payload("first", VpeWrapModes.Repeat), manifest, 1, false, default);
				while (!first.IsCompleted) yield return null; cold = first.GetAwaiter().GetResult();
				Assert.That(cold["first"], Is.Not.Null);
				long hashed = 0; VpeIntegrity.BytesHashed = count => System.Threading.Interlocked.Add(ref hashed, count);
				var second = VpeSharedTextureCache.LoadAsync(storage.GetFolder("table"), Payload("renamed", VpeWrapModes.Clamp), manifest, 1, false, default);
				while (!second.IsCompleted) yield return null; warm = second.GetAwaiter().GetResult();
				Assert.That(warm["renamed"].wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
				Assert.That(hashed, Is.LessThan(16384)); Assert.That(Directory.GetFiles(VpeSharedTextureCache.Root, "*.vpto", SearchOption.AllDirectories).Length, Is.EqualTo(1));
			} finally {
				VpeIntegrity.BytesHashed = null;
				foreach (var group in new[] { cold, warm }) if (group != null) foreach (var texture in group.Values) if (texture) Object.DestroyImmediate(texture);
			}
		}

		private static byte[] Wav()
		{
			var samples = new byte[8820]; new System.Random(37189).NextBytes(samples);
			using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
			writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples.Length); writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
			writer.Write((ushort)1); writer.Write((ushort)1); writer.Write(44100); writer.Write(88200); writer.Write((ushort)2); writer.Write((ushort)16);
			writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(samples.Length); writer.Write(samples); writer.Flush(); return stream.ToArray();
		}
	}
}
