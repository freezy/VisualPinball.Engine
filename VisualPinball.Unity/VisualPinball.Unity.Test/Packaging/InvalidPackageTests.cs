// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// https://github.com/freezy/VisualPinball.Engine
//
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
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

		[UnityTest]
		public IEnumerator PackageInAnOlderLayoutIsRefusedWithWhatChanged()
		{
			// what a build from before the items moved into one file wrote
			var path = Path.Combine(Path.GetTempPath(), $"vpe-old-layout-{Guid.NewGuid():N}.vpe");
			using (var storage = PackageApi.StorageManager.CreateStorage(path)) {
				var manifest = Manifest(3, "VPE 0.0.1-preview.248");
				manifest.FormatVersion = 2;
				VpePackageManifestIo.Write(storage, manifest);
				storage.AddFolder(PackageApi.TableFolder).AddFolder("items").AddFolder("node").AddFile("item", PackageApi.Packer.FileExtension).SetData(PackageApi.Packer.Empty);
			}
			try {
				yield return AssertInvalid(path, "VPE 0.0.1-preview.248", VpePackageSchemas.ItemsChanges[2], "Export the table again");
			} finally {
				File.Delete(path);
			}
		}

		[Test]
		public void FormatVersionDecidesBeforeTheItemsSchema()
		{
			var current = Manifest(VpePackageSchemas.ItemsVersion);
			Assert.That(VpePackageSchemas.Incompatibility(current), Is.Null);

			// a newer layout can't be read, whatever it says about its items
			var newer = Manifest(VpePackageSchemas.ItemsVersion);
			newer.FormatVersion = PackageApi.FormatVersion + 1;
			Assert.That(VpePackageSchemas.Incompatibility(newer), Does.Contain("Update VPE"));

			// an older layout is told what changed, or at least that it has to be exported again
			var older = Manifest(VpePackageSchemas.MinItemsVersion - 1, "VPE 0.1");
			older.FormatVersion = PackageApi.FormatVersion - 1;
			Assert.That(VpePackageSchemas.Incompatibility(older), Does.Contain("VPE 0.1").And.Contain("Export the table again"));
			var olderLayoutOnly = Manifest(VpePackageSchemas.ItemsVersion);
			olderLayoutOnly.FormatVersion = PackageApi.FormatVersion - 1;
			Assert.That(VpePackageSchemas.Incompatibility(olderLayoutOnly), Does.Contain($"format version {PackageApi.FormatVersion - 1}").And.Contain("Export the table again"));
		}

		[Test]
		public void IncompatibleLightingSchemasAreRejectedBeforeLoadingPayloads()
		{
			var manifest = Manifest(VpePackageSchemas.ItemsVersion);
			manifest.Schemas[VpePackageSchemas.Materials] = VpeMaterialSchema.Version;
			manifest.Schemas[VpePackageSchemas.Lights] = VpeLightsPayload.CurrentVersion;
			Assert.That(VpePackageSchemas.Incompatibility(manifest), Is.Null);

			manifest.Schemas[VpePackageSchemas.Materials]--;
			Assert.That(VpePackageSchemas.Incompatibility(manifest), Does.Contain("materials schema").And.Contain("Export the table again"));
			manifest.Schemas[VpePackageSchemas.Materials]++;
			manifest.Schemas[VpePackageSchemas.Lights]++;
			Assert.That(VpePackageSchemas.Incompatibility(manifest), Does.Contain("lights schema").And.Contain("Update VPE"));
		}

		[Test]
		public void OldItemsSchemaMustBeReExportedForAuthoredShadows()
		{
			Assert.That(VpePackageSchemas.Incompatibility(Manifest(6, reader: 4)),
				Does.Contain("authored shadow settings").And.Contain("Export the table again"));
		}

		[Test]
		public void UnsupportedPayloadVersionWinsOverUnknownFields()
		{
			var bytes = Encoding.UTF8.GetBytes("{\"formatVersion\":2,\"removedField\":true}");
			var error = Assert.Throws<InvalidPackageException>(() => VpeIntegrity.ParseVersioned<VpeMaterialsPayload>(
				bytes, "formatVersion", VpePackageSchemas.Materials, VpeMaterialSchema.Version));
			Assert.That(error.Message, Does.Contain("materials schema 2").And.Contain("Export the table again"));
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
