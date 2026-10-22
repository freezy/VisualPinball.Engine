// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Test
{
	public class RuntimeAudioBatchTests
	{
		private string _folder, _prefix, _previousCache;
		[SetUp] public void Setup()
		{
			_prefix = "audio-batch-" + Guid.NewGuid().ToString("N");
			_folder = Path.Combine(Path.GetTempPath(), _prefix);
			Directory.CreateDirectory(_folder);
			_previousCache = VpeIntegrity.CacheRoot;
			VpeIntegrity.CacheRoot = Path.Combine(_folder, "cache");
		}

		[TearDown] public void Teardown()
		{
			foreach (var clip in Clips()) Object.DestroyImmediate(clip);
			VpeIntegrity.CacheRoot = _previousCache;
			Directory.Delete(_folder, true);
		}

		[UnityTest] public IEnumerator RuntimeAudioBatchesPreserveEveryClipAndItsSamples()
		{
			var package = CreatePackage(19);
			using var storage = PackageApi.StorageManager.OpenStorage(package);
			var files = new PackagedFiles(storage.GetFolder(PackageApi.TableFolder), null);
			var progress = new List<int>();
			var task = files.UnpackSoundsRuntime(progress: (done, total) => { Assert.That(total, Is.EqualTo(19)); progress.Add(done); });
			while (!task.IsCompleted) yield return null;
			if (task.IsFaulted) throw task.Exception.GetBaseException();
			Assert.That(task.IsCanceled, Is.False);
			Assert.That(progress, Is.EqualTo(Enumerable.Range(1, 19)));
			for (var i = 0; i < 19; i++) {
				var clip = files.GetAudioClip("guid-" + i);
				Assert.That(clip, Is.Not.Null);
				Assert.That(clip.name, Is.EqualTo(_prefix + "-" + i));
				Assert.That(clip.channels, Is.EqualTo(1));
				Assert.That(clip.frequency, Is.EqualTo(44100));
				Assert.That(clip.samples, Is.EqualTo(128));
				var samples = new float[128]; Assert.That(clip.GetData(samples, 0), Is.True);
				Assert.That(samples[0], Is.EqualTo((i + 1) * 100 / 32768f).Within(.0001f));
			}
		}

		[UnityTest] public IEnumerator CancellationJoinsPendingAudioAndRemovesTemporaryFiles()
		{
			var package = CreatePackage(19);
			using var storage = PackageApi.StorageManager.OpenStorage(package);
			var files = new PackagedFiles(storage.GetFolder(PackageApi.TableFolder), null);
			Directory.CreateDirectory(Application.temporaryCachePath);
			var before = Directory.GetFiles(Application.temporaryCachePath, "vpe-audio-*");
			using var cancellation = new CancellationTokenSource();
			var task = files.UnpackSoundsRuntime(cancellation.Token);
			cancellation.Cancel();
			while (!task.IsCompleted) yield return null;
			Assert.That(task.IsCanceled, Is.True);
			Assert.That(Clips(), Is.Empty, "No audio clip remains after canceling the first batch.");
			Assert.That(Directory.GetFiles(Application.temporaryCachePath, "vpe-audio-*").Except(before), Is.Empty);
		}

		private AudioClip[] Clips() => Resources.FindObjectsOfTypeAll<AudioClip>().Where(clip => clip.name.StartsWith(_prefix, StringComparison.Ordinal)).ToArray();

		private string CreatePackage(int count)
		{
			var path = Path.Combine(_folder, "audio.vpe");
			using var storage = PackageApi.StorageManager.CreateStorage(path);
			var table = storage.AddFolder(PackageApi.TableFolder);
			var sounds = table.AddFolder(PackageApi.SoundFolder);
			var metas = new Dictionary<string, SoundMetaPackable>();
			for (var i = 0; i < count; i++) {
				var name = _prefix + "-" + i + ".wav";
				using var data = new MemoryStream();
				using var writer = new BinaryWriter(data);
				writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + 256);
				writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
				writer.Write((short)1); writer.Write((short)1); writer.Write(44100); writer.Write(88200);
				writer.Write((short)2); writer.Write((short)16);
				writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(256);
				for (var sample = 0; sample < 128; sample++) writer.Write((short)((i + 1) * 100));
				writer.Flush(); sounds.AddFile(name).SetData(data.ToArray());
				metas.Add(name, new SoundMetaPackable { Guid = "guid-" + i });
			}
			table.AddFolder(PackageApi.MetaFolder).AddFile(PackageApi.SoundFolder, PackageApi.Packer.FileExtension).SetData(PackageApi.Packer.Pack(metas));
			return path;
		}
	}
}
