// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>Editor-local source index. Incoming bytes and selected candidates are always verified.</summary>
	public static class VpeAudioAssetIndex
	{
		private const string IndexPath = "Library/VPE/AudioContentIndex.json";
		private static readonly string[] Platforms = { "Standalone", "Android", "iPhone", "WebGL", "Windows Store Apps", "PS4", "PS5", "XboxOne", "Nintendo Switch" };
		private static Dictionary<string, Record> _records;
		private static bool _dirty = true;
		private static int _generation, _buildGeneration;
		private static Task<Dictionary<string, Record>> _pending;
		private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
		public sealed class Record { public string Path, PhysicalPath, Exact, Pcm; public long Length, Modified, Created; }

		[InitializeOnLoadMethod]
		private static void Initialize() { EditorApplication.projectChanged += () => { _dirty = true; _generation++; }; }
		public static string SourcePath(string assetPath)
		{
			if (!assetPath.StartsWith("Packages/", StringComparison.Ordinal)) return assetPath;
			var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(assetPath);
			if (package == null) throw new IOException("Cannot locate audio package: " + assetPath);
			return Path.Combine(package.resolvedPath, assetPath.Substring(package.assetPath.Length).TrimStart('/'));
		}

		public static void RegisterImported(AudioClip clip)
		{
			var path = AssetDatabase.GetAssetPath(clip);
			_records ??= new();
			_records[AssetDatabase.AssetPathToGUID(path)] = Read(path, SourcePath(path));
			// The imported asset is already indexed; do not rescan the library for the next sound.
			_dirty = false;
		}

		public static void Rebuild()
		{
			if (_pending != null) EnsureIndex();
			_records = new(); _dirty = true; EnsureIndex();
		}

		public static async Task PrepareAsync()
		{
			try {
				var build = StartBuild();
				if (build == null) return;
				var records = await build;
				CompleteBuild(build, records);
			} catch (Exception e) { _pending = null; _dirty = true; Logger.Warn(e, "Cannot prepare the audio index; imports can still create assets."); }
		}

		private static void EnsureIndex()
		{
			var build = StartBuild();
			if (build == null) return;
			try { CompleteBuild(build, build.GetAwaiter().GetResult()); }
			catch { _pending = null; _dirty = true; throw; }
		}

		private static void CompleteBuild(Task<Dictionary<string, Record>> build, Dictionary<string, Record> records)
		{
			if (_pending != build) return;
			_records = records; _pending = null; _dirty = _buildGeneration != _generation;
		}

		private static Task<Dictionary<string, Record>> StartBuild()
		{
			if (_pending != null) return _pending;
			if (!_dirty && _records != null) return null;
			// Capture Unity paths on the main thread; file IO and hashing never call AssetDatabase.
			var sources = AssetDatabase.FindAssets("t:AudioClip").Select(guid => {
				var path = AssetDatabase.GUIDToAssetPath(guid);
				return (Guid: guid, Path: path, Physical: SourcePath(path));
			}).ToArray();
			var previous = _records == null ? null : new Dictionary<string, Record>(_records);
			_buildGeneration = _generation;
			return _pending = Task.Run(() => {
				if (previous == null) {
					try { using var stream = File.OpenRead(IndexPath); previous = VpeIntegrity.Parse<Dictionary<string, Record>>(VpeIntegrity.ReadBounded(stream, VpeIntegrity.MaxManifestBytes)); }
					catch { previous = new(); }
				}
				var result = new Dictionary<string, Record>();
				foreach (var source in sources) {
					try {
						var info = new FileInfo(source.Physical);
						if (!info.Exists) continue;
						if (previous.TryGetValue(source.Guid, out var old) && old != null && old.Path == source.Path && old.PhysicalPath == source.Physical && old.Length == info.Length && old.Modified == info.LastWriteTimeUtc.Ticks && old.Created == info.CreationTimeUtc.Ticks && VpeIntegrity.IsHash(old.Exact)) result[source.Guid] = old;
						else result[source.Guid] = Read(source.Path, source.Physical);
					} catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { Logger.Debug(e, "Cannot index sound {Path}", source.Path); }
				}
				try { VpeIntegrity.AtomicWrite(IndexPath, VpeIntegrity.Json(result)); }
				catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { Logger.Debug(e, "Cannot persist audio index."); }
				return result;
			});
		}

		private static Record Read(string path, string physicalPath)
		{
			var info = new FileInfo(physicalPath);
			info.Refresh();
			if (info.Length > 256 * 1024 * 1024) throw new IOException("Audio source exceeds matching budget.");
			var bytes = File.ReadAllBytes(physicalPath);
			VpeIntegrity.EnsureUnchanged(physicalPath, info);
			return new Record { Path = path, PhysicalPath = physicalPath, Created = info.CreationTimeUtc.Ticks, Length = info.Length, Modified = info.LastWriteTimeUtc.Ticks, Exact = VpeIntegrity.Hash(bytes), Pcm = VpeAudioIdentity.Canonical(bytes) };
		}

		public static AudioClip Resolve(byte[] incoming, SoundMetaPackable intent, string pcm = null, bool allowExact = true, string expectedHash = null)
		{
			var exact = VpeIntegrity.Hash(incoming);
			if (expectedHash != null && exact != expectedHash) throw new InvalidDataException("Sound source checksum mismatch.");
			pcm ??= VpeAudioIdentity.Canonical(incoming);
			try { EnsureIndex(); } catch (Exception e) { Logger.Warn(e, "Audio library index unavailable; importing a new asset."); return null; }
			var candidates = _records.Where(p => allowExact && p.Value.Exact == exact || pcm != null && p.Value.Pcm == pcm)
				.OrderBy(p => p.Key == intent.Guid ? 0 : p.Value.Path.StartsWith("Packages/", StringComparison.Ordinal) ? 1 : 2).ThenBy(p => p.Key, StringComparer.Ordinal).ToArray();
			foreach (var candidate in candidates) {
				try {
					var path = AssetDatabase.GUIDToAssetPath(candidate.Key);
					if (!(AssetImporter.GetAtPath(path) is AudioImporter importer) || !Compatible(Capture(importer, candidate.Key), intent)) continue;
					var current = Read(path, SourcePath(path));
					_records[candidate.Key] = current;
					if (!(allowExact && current.Exact == exact || pcm != null && current.Pcm == pcm)) continue;
					var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
					if (clip) return clip;
				} catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { }
			}
			return null;
		}

		private static string SampleJson(AudioImporterSampleSettings settings) { if (settings.sampleRateSetting != AudioSampleRateSetting.OverrideSampleRate) settings.sampleRateOverride = 0; return JsonUtility.ToJson(settings); }

		public static SoundMetaPackable Capture(AudioImporter importer, string guid)
		{
			var serialized = new SerializedObject(importer);
			var platforms = new Dictionary<string, string>();
			foreach (var platform in Platforms) if (importer.ContainsSampleSettingsOverride(platform)) platforms.Add(platform, SampleJson(importer.GetOverrideSampleSettings(platform)));
			var settings = importer.defaultSampleSettings;
			return new SoundMetaPackable { Guid = guid, ForceToMono = importer.forceToMono, Ambisonic = importer.ambisonic, LoadInBackground = importer.loadInBackground, Normalize = serialized.FindProperty("m_Normalize")?.boolValue ?? true, ImportSettingsVersion = 1, SampleSettings = SampleJson(settings), PlatformSettings = platforms, LoadType = settings.loadType.ToString(), CompressionFormat = settings.compressionFormat.ToString(), Quality = settings.quality };
		}

		public static SoundMetaPackable DefaultIntent()
		{
			var settings = new AudioImporterSampleSettings { loadType = AudioClipLoadType.DecompressOnLoad, compressionFormat = AudioCompressionFormat.Vorbis, quality = 1, sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate, preloadAudioData = true };
			return new SoundMetaPackable { ImportSettingsVersion = 1, Normalize = true, SampleSettings = SampleJson(settings), PlatformSettings = new(), LoadType = settings.loadType.ToString(), CompressionFormat = settings.compressionFormat.ToString(), Quality = 1 };
		}

		public static string IntentKey(SoundMetaPackable intent)
		{
			if (intent.ImportSettingsVersion != 1 || string.IsNullOrEmpty(intent.SampleSettings) || intent.PlatformSettings == null) throw new InvalidDataException("Complete audio importer settings are required.");
			if (intent.PlatformSettings.Any(p => !Platforms.Contains(p.Key) || string.IsNullOrEmpty(p.Value))) throw new InvalidDataException("Unsupported audio platform settings.");
			intent.Guid = null;
			intent.PlatformSettings = intent.PlatformSettings.OrderBy(p => p.Key, StringComparer.Ordinal).ToDictionary(p => p.Key, p => p.Value);
			return VpeIntegrity.Hash(VpeIntegrity.Json(intent));
		}

		private static bool Compatible(SoundMetaPackable a, SoundMetaPackable b) => IntentKey(a) == IntentKey(b);

		public static void Apply(AudioImporter importer, SoundMetaPackable intent)
		{
			if (intent.ImportSettingsVersion != 1 || string.IsNullOrEmpty(intent.SampleSettings) || intent.PlatformSettings == null) throw new InvalidDataException("Complete audio importer settings are required.");
			IntentKey(intent); // Validate all settings before mutating a newly imported asset.
			importer.forceToMono = intent.ForceToMono; importer.ambisonic = intent.Ambisonic; importer.loadInBackground = intent.LoadInBackground;
			var serialized = new SerializedObject(importer);
			var normalize = serialized.FindProperty("m_Normalize");
			if (normalize != null) { normalize.boolValue = intent.Normalize; serialized.ApplyModifiedPropertiesWithoutUndo(); }
			importer.defaultSampleSettings = JsonUtility.FromJson<AudioImporterSampleSettings>(intent.SampleSettings);
			foreach (var platform in Platforms) {
				if (intent.PlatformSettings.TryGetValue(platform, out var json)) importer.SetOverrideSampleSettings(platform, JsonUtility.FromJson<AudioImporterSampleSettings>(json));
				else importer.ClearSampleSettingOverride(platform);
			}
		}
	}
}
#endif
