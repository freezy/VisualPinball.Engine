// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// https://github.com/freezy/VisualPinball.Engine
//
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace VisualPinball.Unity.Test
{
	/// <summary>
	/// A file that isn't a .vpe package fails with <see cref="InvalidPackageException"/>, so the
	/// player can tell the user instead of reporting it as a bug.
	/// </summary>
	public class InvalidPackageTests
	{
		[UnityTest]
		public IEnumerator FileThatIsNoPackageAtAllIsInvalid()
		{
			var path = Path.Combine(Path.GetTempPath(), $"vpe-not-a-package-{Guid.NewGuid():N}.vpe");
			File.WriteAllText(path, "this is a text file, not a table");
			try {
				yield return AssertInvalid(path);
			} finally {
				File.Delete(path);
			}
		}

		[UnityTest]
		public IEnumerator PackageWithoutManifestIsInvalid()
		{
			var path = Path.Combine(Path.GetTempPath(), $"vpe-no-manifest-{Guid.NewGuid():N}.vpe");
			using (var storage = PackageApi.StorageManager.CreateStorage(path)) {
				storage.AddFolder(PackageApi.TableFolder);
			}
			try {
				yield return AssertInvalid(path);
			} finally {
				File.Delete(path);
			}
		}

		[UnityTest]
		public IEnumerator PackageWithoutSceneIsInvalid()
		{
			var path = Path.Combine(Path.GetTempPath(), $"vpe-no-scene-{Guid.NewGuid():N}.vpe");
			using (var storage = PackageApi.StorageManager.CreateStorage(path)) {
				VpePackageManifestIo.Write(storage, new VpePackageManifest { WrittenBy = "test" });
				storage.AddFolder(PackageApi.TableFolder);
			}
			try {
				yield return AssertInvalid(path);
			} finally {
				File.Delete(path);
			}
		}

		private static IEnumerator AssertInvalid(string path)
		{
			var task = new RuntimePackageReader(path).ImportIntoScene();
			while (!task.IsCompleted) {
				yield return null;
			}
			Assert.That(task.IsFaulted, Is.True);
			Assert.That(task.Exception!.InnerException, Is.InstanceOf<InvalidPackageException>(), task.Exception.ToString());
		}
	}
}
