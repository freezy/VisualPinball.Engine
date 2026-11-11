// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// https://github.com/freezy/VisualPinball.Engine
//
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections;
using System.IO;
using System.Linq;
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
				VpePackageManifestIo.Write(storage, Manifest(VpePackageSchemas.ItemsVersion));
				storage.AddFolder(PackageApi.TableFolder);
			}
			try {
				yield return AssertInvalid(path, "scene");
			} finally {
				File.Delete(path);
			}
		}

		[UnityTest]
		public IEnumerator PackageFromBeforeABreakingChangeIsRefusedWithTheReason()
		{
			var path = Path.Combine(Path.GetTempPath(), $"vpe-old-revision-{Guid.NewGuid():N}.vpe");
			using (var storage = PackageApi.StorageManager.CreateStorage(path)) {
				VpePackageManifestIo.Write(storage, Manifest(1, "VPE 0.0.1-preview.235"));
				storage.AddFolder(PackageApi.TableFolder);
			}
			try {
				yield return AssertInvalid(path, "VPE 0.0.1-preview.235", VpePackageSchemas.ItemsChanges[0], "Export the table again");
			} finally {
				File.Delete(path);
			}
		}

		[UnityTest]
		public IEnumerator PackageFromANewerBuildIsRefused()
		{
			var path = Path.Combine(Path.GetTempPath(), $"vpe-new-revision-{Guid.NewGuid():N}.vpe");
			using (var storage = PackageApi.StorageManager.CreateStorage(path)) {
				VpePackageManifestIo.Write(storage, Manifest(VpePackageSchemas.ItemsVersion + 1));
				storage.AddFolder(PackageApi.TableFolder);
			}
			try {
				yield return AssertInvalid(path, "Update VPE");
			} finally {
				File.Delete(path);
			}
		}

		[Test]
		public void EveryItemsSchemaVersionHasItsReason()
		{
			Assert.That(VpePackageSchemas.ItemsChanges, Has.Length.EqualTo(VpePackageSchemas.ItemsVersion - 1));
			Assert.That(VpePackageSchemas.MinItemsVersion, Is.InRange(1, VpePackageSchemas.ItemsVersion));
			for (var version = VpePackageSchemas.MinItemsVersion; version <= VpePackageSchemas.ItemsVersion; version++) {
				Assert.That(VpePackageSchemas.ItemsIncompatibility(Manifest(version)), Is.Null, $"Items schema {version}");
			}
			Assert.That(VpePackageSchemas.ItemsIncompatibility(Manifest(VpePackageSchemas.MinItemsVersion - 1)), Is.Not.Null);
			Assert.That(VpePackageSchemas.ItemsIncompatibility(Manifest(VpePackageSchemas.ItemsVersion + 1)), Is.Not.Null);
			// what this build writes, it loads
			Assert.That(VpePackageSchemas.ItemsReaderVersion, Is.InRange(VpePackageSchemas.MinItemsVersion, VpePackageSchemas.ItemsVersion));
			Assert.That(VpePackageSchemas.ItemsIncompatibility(Manifest(VpePackageSchemas.ItemsVersion, reader: VpePackageSchemas.ItemsReaderVersion)), Is.Null);
			// a package older than the oldest version still loaded is told everything that changed up to it
			foreach (var change in VpePackageSchemas.ItemsChanges.Take(VpePackageSchemas.MinItemsVersion - 1)) {
				Assert.That(VpePackageSchemas.ItemsIncompatibility(Manifest(1)), Does.Contain(change));
				// a manifest without the schema table is older than any version
				Assert.That(VpePackageSchemas.ItemsIncompatibility(new VpePackageManifest { Schemas = null }), Does.Contain(change));
			}
		}

		[Test]
		public void NewerPackageLoadsWhenItNeedsNoNewerReader()
		{
			// a later build added what this one can ignore, and says so
			Assert.That(VpePackageSchemas.ItemsIncompatibility(Manifest(VpePackageSchemas.ItemsVersion + 3, reader: VpePackageSchemas.ItemsVersion)), Is.Null);
			// a later build changed tables in a way this one would load wrongly
			var reason = VpePackageSchemas.ItemsIncompatibility(Manifest(VpePackageSchemas.ItemsVersion + 3, "VPE 9.9", VpePackageSchemas.ItemsVersion + 2));
			Assert.That(reason, Does.Contain("VPE 9.9").And.Contain("Update VPE").And.Contain($"items schema {VpePackageSchemas.ItemsVersion + 2}"));
			// the reader version doesn't make up for a package older than this build loads
			Assert.That(VpePackageSchemas.ItemsIncompatibility(Manifest(VpePackageSchemas.MinItemsVersion - 1, reader: 1)), Does.Contain("Export the table again"));
		}

		private static VpePackageManifest Manifest(int itemsVersion, string writtenBy = "test", int? reader = null)
		{
			var manifest = new VpePackageManifest {
				WrittenBy = writtenBy,
				Schemas = new System.Collections.Generic.Dictionary<string, int> { [VpePackageSchemas.Items] = itemsVersion },
			};
			if (reader.HasValue) {
				manifest.Schemas[VpePackageSchemas.ItemsReader] = reader.Value;
			}
			return manifest;
		}

		private static IEnumerator AssertInvalid(string path, params string[] reasons)
		{
			var task = new RuntimePackageReader(path).ImportIntoScene();
			while (!task.IsCompleted) {
				yield return null;
			}
			Assert.That(task.IsFaulted, Is.True);
			Assert.That(task.Exception!.InnerException, Is.InstanceOf<InvalidPackageException>(), task.Exception.ToString());
			foreach (var reason in reasons) {
				Assert.That(task.Exception.InnerException.Message, Does.Contain(reason));
			}
		}
	}
}
