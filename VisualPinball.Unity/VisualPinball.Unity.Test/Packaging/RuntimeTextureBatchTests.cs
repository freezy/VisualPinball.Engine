// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Test
{
	public class RuntimeTextureBatchTests
	{
		private string _prefix, _cache, _previousCache;
		[SetUp] public void Setup()
		{
			_prefix = "texture-batch-" + Guid.NewGuid().ToString("N");
			_cache = Path.Combine(Path.GetTempPath(), _prefix);
			_previousCache = VpeIntegrity.CacheRoot; VpeIntegrity.CacheRoot = _cache;
		}
		[TearDown] public void Teardown()
		{
			foreach (var texture in Textures()) Object.DestroyImmediate(texture);
			VpeIntegrity.BytesHashed = null;
			VpeIntegrity.CacheRoot = _previousCache;
			Directory.Delete(_cache, true);
		}

		[UnityTest] public IEnumerator WarmBatchesUploadAcrossBatchBoundaryWithoutOpeningPackage()
		{
			var (payload, integrity) = Seed(19);
			// Null storage deliberately makes any accidental source read fail.
			var task = VpeSharedTextureCache.LoadAsync(null, payload, integrity, 1, false, default);
			while (!task.IsCompleted) yield return null;
			if (task.IsFaulted) throw task.Exception.GetBaseException();
			Assert.That(task.Result.Count, Is.EqualTo(19));
			foreach (var texture in task.Result.Values) {
				Assert.That(texture, Is.Not.Null);
				Assert.That(texture.width, Is.EqualTo(8));
				Assert.That(texture.height, Is.EqualTo(8));
				Assert.That(texture.format, Is.EqualTo(TextureFormat.RGBA32));
			}
		}

		[UnityTest] public IEnumerator CancellationDestroysUploadedTexturesAndJoinsReadAhead()
		{
			var (payload, integrity) = Seed(19);
			using var cancellation = new CancellationTokenSource();
			var uploaded = 0;
			var task = VpeSharedTextureCache.LoadAsync(null, payload, integrity, 1, false, cancellation.Token, (done, _) => {
				uploaded = done;
				cancellation.Cancel();
			});
			while (!task.IsCompleted) yield return null;
			Assert.That(task.IsCanceled, Is.True);
			Assert.That(uploaded, Is.EqualTo(1));
			Assert.That(Textures(), Is.Empty);
			// The canceled task has joined its worker, so removing the cache cannot race a read.
			Directory.Delete(_cache, true);
			Directory.CreateDirectory(_cache);
		}

		[UnityTest] public IEnumerator ColdDuplicateTextureDoesNotRehashOrRecookItsSource()
		{
			var original = new Texture2D(64, 32, TextureFormat.RGBA32, false);
			var pixels = new Color32[64 * 32]; var random = new System.Random(1234);
			for (var i = 0; i < pixels.Length; i++) pixels[i] = new Color32((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256), 255);
			original.SetPixels32(pixels); original.Apply(); var png = original.EncodeToPNG(); Object.DestroyImmediate(original);
			var path = Path.Combine(_cache, "duplicate.vpe"); Directory.CreateDirectory(_cache);
			using (var write = PackageApi.StorageManager.CreateStorage(path))
				write.AddFolder(PackageApi.TableFolder).AddFolder(PackageApi.TexturesFolder).AddFile("shared.png").SetData(png);
			using var storage = PackageApi.StorageManager.OpenStorage(path);
			var payload = new VpeMaterialsPayload { Textures = Enumerable.Range(0, 2).Select(i => new VpeTexture {
				Id = "duplicate-" + i, SourceName = _prefix + "-duplicate-" + i, FileName = "shared.png", MimeType = "image/png", Width = 64, Height = 32, ForceUncompressed = true, GenerateMipMaps = false
			}).ToArray() };
			UseAllTextures(payload);
			var sourceChecks = 0;
			VpeIntegrity.BytesHashed = bytes => { if (bytes == png.Length) Interlocked.Increment(ref sourceChecks); };
			var task = VpeSharedTextureCache.LoadAsync(storage.GetFolder(PackageApi.TableFolder), payload, VpeIntegrity.ReadManifest(storage), 1, false, default);
			while (!task.IsCompleted) yield return null;
			if (task.IsFaulted) throw task.Exception.GetBaseException();
			Assert.That(task.Result.Values.All(texture => texture), Is.True);
			Assert.That(task.Result.Count, Is.EqualTo(2));
			Assert.That(sourceChecks, Is.EqualTo(1), "A read-ahead miss for an already cooked key must reuse that object.");
			Assert.That(Directory.GetFiles(VpeSharedTextureCache.Root, "*.vpto", SearchOption.AllDirectories).Length, Is.EqualTo(1));
		}

		private static void UseAllTextures(VpeMaterialsPayload payload)
		{
			payload.Profiles = payload.Textures.Select(texture => new VpeMaterialProfile {
				Type = VpeMaterialTypes.Lit, Lit = new VpeLitProfile { MaskMap = new VpeTextureRef { TextureId = texture.Id } }
			}).ToArray();
		}

		private Texture2D[] Textures() => Resources.FindObjectsOfTypeAll<Texture2D>().Where(texture => texture.name.StartsWith(_prefix, StringComparison.Ordinal)).ToArray();

		private (VpeMaterialsPayload, VpeIntegrity.Manifest) Seed(int count)
		{
			var payload = new VpeMaterialsPayload { Textures = new VpeTexture[count] };
			var integrity = new VpeIntegrity.Manifest();
			var backend = SystemInfo.graphicsDeviceType + ":" + SystemInfo.graphicsDeviceVendorID;
			for (var i = 0; i < count; i++) {
				var source = new VpeTexture { Id = "texture-" + i, SourceName = _prefix + "-" + i, FileName = i + ".png", MimeType = "image/png", Width = 8, Height = 8 };
				payload.Textures[i] = source;
				var hash = VpeIntegrity.Hash(new[] { (byte)i });
				integrity.Entries.Add(new VpeIntegrity.Entry { Path = "table/textures/" + source.FileName, Length = 1, Sha256 = hash });
				var descriptor = VpeTextureSources.ToPayload(source);
				var key = VpeSharedTextureCache.Key(descriptor, hash, false, 1, false, backend, VpeTextureCook.IsSupported);
				descriptor.PixelFormat = VpePixelFormats.Rgba32; descriptor.MimeType = "application/octet-stream";
				VpeSharedTextureCache.Write(key, descriptor, new byte[8 * 8 * 4], false);
			}
			UseAllTextures(payload);
			return (payload, integrity);
		}
	}
}
