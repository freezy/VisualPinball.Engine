// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;

namespace VisualPinball.Unity.Test.Packaging
{
	[TestFixture]
	public class PackagedContentTests
	{
		private string _root;

		[SetUp]
		public void SetUp()
		{
			_root = Path.Combine(Path.GetTempPath(), "vpe-content-tests", Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_root);
		}

		[TearDown]
		public void TearDown()
		{
			if (Directory.Exists(_root)) {
				Directory.Delete(_root, true);
			}
		}

		[Test]
		public async Task RoundTripsNestedTreeInEditorAndRuntimeResolver()
		{
			var source = CreateFixture();
			var package = Path.Combine(_root, "table.vpe");
			var contentRef = CreatePackage(package, source, new ContentPackOptions { EntryPoint = "machine/config.yaml" });
			var cache = Path.Combine(_root, "cache");
			var resolver = new PackagedContentResolver(package, new PackagedContentCacheOptions { CacheRoot = cache });

			var progress = 0f;
			var resolved = await resolver.ResolveAsync(contentRef, new InlineProgress(value => progress = value), CancellationToken.None);

			File.ReadAllText(Path.Combine(resolved, "machine", "config.yaml")).Should().Be("name: synthetic\n");
			File.ReadAllBytes(Path.Combine(resolved, "media", "large.mp4")).Should().HaveCount(2 * 1024 * 1024);
			File.Exists(Path.Combine(resolved, "fonts", "Tést Font.ttf")).Should().BeTrue();
			new FileInfo(Path.Combine(resolved, "empty.py")).Length.Should().Be(0);
			File.Exists(Path.Combine(resolved, ".complete")).Should().BeTrue();
			progress.Should().Be(1f);
			PackagedContentValidator.ValidatePackage(package).Should().BeEmpty();
		}

		[Test]
		public async Task IdenticalTreesAreDeterministicAndShareCacheEntry()
		{
			var firstSource = CreateFixture("first");
			var secondSource = CreateFixture("second");
			var firstPackage = Path.Combine(_root, "first.vpe");
			var secondPackage = Path.Combine(_root, "second.vpe");
			var firstRef = CreatePackage(firstPackage, firstSource);
			var secondRef = CreatePackage(secondPackage, secondSource);
			firstRef.Id.Should().Be(secondRef.Id);
			firstRef.ContentHash.Should().Be(secondRef.ContentHash);

			var cache = Path.Combine(_root, "shared-cache");
			var firstPath = await new PackagedContentResolver(firstPackage, CacheOptions(cache)).ResolveAsync(firstRef, null, CancellationToken.None);
			var secondPath = await new PackagedContentResolver(secondPackage, CacheOptions(cache)).ResolveAsync(secondRef, null, CancellationToken.None);
			secondPath.Should().Be(firstPath);
			Directory.GetDirectories(cache).Should().ContainSingle();
		}

		[Test]
		public void IncludesAndExcludesUseForwardSlashGlobs()
		{
			var source = CreateFixture();
			var prepared = PackagedContentWriter.PrepareDirectory("test", source, new ContentPackOptions {
				IncludeGlobs = new[] { "machine/**", "fonts/*.ttf", "**/*.py" },
				ExcludeGlobs = new[] { "**/*.tmp" },
			});
			prepared.Manifest.Files.Select(file => file.Path).Should().BeEquivalentTo(
				"machine/config.yaml", "machine/deep/a/b/c/data.bin", "fonts/Tést Font.ttf", "empty.py");
		}

		[TestCase("../evil")]
		[TestCase("C:\\abs")]
		[TestCase("C:/abs")]
		[TestCase("a\\..\\..\\b")]
		[TestCase("a/../../b")]
		[TestCase("file:stream")]
		[TestCase("/absolute")]
		[TestCase("a//b")]
		public void RejectsUnsafePathsWithActionableMessages(string path)
		{
			Action action = () => PackagedContentPath.ValidateRelative(path);
			action.Should().Throw<InvalidDataException>().Which.Message.Should().Contain(path);
		}

		[Test]
		public void RejectsCaseCollisionsInManifest()
		{
			var reference = new PackagedContentRef(new string('a', 16), "test", null, new string('a', 64));
			var manifest = new PackagedContentManifest {
				Format = "vpe-content",
				Version = 1,
				Kind = "test",
				FileCount = 2,
				TotalBytes = 0,
				ContentHash = reference.ContentHash,
				Files = {
					new PackagedContentFile { Path = "A.txt", Size = 0, Sha256 = new string('0', 64) },
					new PackagedContentFile { Path = "a.txt", Size = 0, Sha256 = new string('0', 64) },
				},
			};
			Action action = () => PackagedContentValidator.ValidateManifest(manifest, reference);
			action.Should().Throw<InvalidDataException>().WithMessage("*differ only by case*");
		}

		[Test]
		public async Task CancellationLeavesNoValidLookingPartialAndNextResolveSucceeds()
		{
			var source = CreateFixture();
			File.WriteAllBytes(Path.Combine(source, "cancel.bin"), new byte[16 * 1024 * 1024]);
			var package = Path.Combine(_root, "cancel.vpe");
			var contentRef = CreatePackage(package, source);
			var cache = Path.Combine(_root, "cache");
			var cts = new CancellationTokenSource();
			var resolver = new PackagedContentResolver(package, CacheOptions(cache));
			var progress = new InlineProgress(value => {
				if (value > 0f) cts.Cancel();
			});

			Func<Task> canceled = () => resolver.ResolveAsync(contentRef, progress, cts.Token);
			await canceled.Should().ThrowAsync<OperationCanceledException>();
			Directory.EnumerateFiles(cache, ".complete", SearchOption.AllDirectories).Should().BeEmpty();

			var resolved = await resolver.ResolveAsync(contentRef, null, CancellationToken.None);
			File.Exists(Path.Combine(resolved, ".complete")).Should().BeTrue();
		}

		[Test]
		public async Task StartupRemovesOrphanTempAndUnmarkedFinalIsReplaced()
		{
			var source = CreateFixture();
			var package = Path.Combine(_root, "partial.vpe");
			var contentRef = CreatePackage(package, source);
			var cache = Path.Combine(_root, "cache");
			var orphan = Path.Combine(cache, contentRef.ContentHash + ".tmp-dead");
			var partial = Path.Combine(cache, contentRef.ContentHash);
			Directory.CreateDirectory(orphan);
			Directory.CreateDirectory(partial);
			File.WriteAllText(Path.Combine(orphan, ".complete"), contentRef.ContentHash);
			File.WriteAllText(Path.Combine(partial, "partial"), "bad");

			var resolver = new PackagedContentResolver(package, CacheOptions(cache));
			Directory.Exists(orphan).Should().BeFalse();
			var resolved = await resolver.ResolveAsync(contentRef, null, CancellationToken.None);
			File.Exists(Path.Combine(resolved, "partial")).Should().BeFalse();
			File.Exists(Path.Combine(resolved, ".complete")).Should().BeTrue();
		}

		[Test]
		public async Task CacheCapEvictsLeastRecentlyUsedCompletedBundle()
		{
			var firstSource = CreateFixture("lru-first");
			var secondSource = CreateFixture("lru-second");
			File.WriteAllText(Path.Combine(secondSource, "different.txt"), "different");
			var firstPackage = Path.Combine(_root, "lru-first.vpe");
			var secondPackage = Path.Combine(_root, "lru-second.vpe");
			var firstRef = CreatePackage(firstPackage, firstSource);
			var secondRef = CreatePackage(secondPackage, secondSource);
			var cache = Path.Combine(_root, "lru-cache");
			var options = new PackagedContentCacheOptions { CacheRoot = cache, CapacityBytes = 3 * 1024 * 1024 };
			var firstPath = await new PackagedContentResolver(firstPackage, options).ResolveAsync(firstRef, null, CancellationToken.None);
			Directory.SetLastWriteTimeUtc(firstPath, DateTime.UtcNow.AddHours(-1));

			var secondPath = await new PackagedContentResolver(secondPackage, options).ResolveAsync(secondRef, null, CancellationToken.None);

			Directory.Exists(firstPath).Should().BeFalse();
			Directory.Exists(secondPath).Should().BeTrue();
		}

		[Test]
		public void ValidatorReportsMissingCorruptAndOversizedContent()
		{
			var source = CreateFixture();
			var prepared = PackagedContentWriter.PrepareDirectory("test", source);
			var missingPackage = Path.Combine(_root, "missing.vpe");
			using (var storage = PackageApi.StorageManager.CreateStorage(missingPackage)) {
				var table = storage.AddFolder(PackageApi.TableFolder);
				var bundle = table.AddFolder(PackageApi.ContentFolder).AddFolder(prepared.Reference.Id);
				bundle.AddFile("manifest", PackageApi.Packer.FileExtension).SetData(PackageApi.Packer.Pack(prepared.Manifest));
				bundle.AddFolder("files");
			}
			PackagedContentValidator.ValidatePackage(missingPackage).Single().Should().Contain("missing");
			PackagedContentValidator.ValidatePackage(missingPackage, maxBundleBytes: 1).Single().Should().Contain("limit");

			prepared.Manifest.Files[0].Sha256 = new string('0', 64);
			var corruptPackage = Path.Combine(_root, "corrupt.vpe");
			using (var storage = PackageApi.StorageManager.CreateStorage(corruptPackage)) {
				var table = storage.AddFolder(PackageApi.TableFolder);
				new PackagedContentWriter(table).Write(prepared);
			}
			PackagedContentValidator.ValidatePackage(corruptPackage).Single().Should().Contain("contentHash");
		}

		[Test]
		public void ValidatorRejectsPayloadThatUnderdeclaresItsActualSize()
		{
			var source = Path.Combine(_root, "underdeclared-source");
			Directory.CreateDirectory(source);
			File.WriteAllBytes(Path.Combine(source, "payload.bin"), new byte[] { 1, 2, 3, 4 });
			var prepared = PackagedContentWriter.PrepareDirectory("test", source);
			prepared.Manifest.Files[0].Size = 1;
			prepared.Manifest.TotalBytes = 1;
			var package = Path.Combine(_root, "underdeclared.vpe");
			using (var storage = PackageApi.StorageManager.CreateStorage(package)) {
				var table = storage.AddFolder(PackageApi.TableFolder);
				new PackagedContentWriter(table).Write(prepared);
			}

			PackagedContentValidator.ValidatePackage(package).Single().Should().Contain("manifest declares 1");
		}

		[Test]
		public void LinterReportsEntryExecutableDuplicateAndPerFileSizeProblems()
		{
			var source = Path.Combine(_root, "lint-source");
			Directory.CreateDirectory(source);
			File.WriteAllBytes(Path.Combine(source, "first.bin"), new byte[] { 1, 2, 3 });
			File.WriteAllBytes(Path.Combine(source, "second.bin"), new byte[] { 1, 2, 3 });
			File.WriteAllBytes(Path.Combine(source, "helper.exe"), new byte[] { 4, 5, 6 });
			var prepared = PackagedContentWriter.PrepareDirectory("web-show", source);

			var issues = PackagedContentValidator.LintManifest(prepared.Manifest, prepared.Reference,
				requireEntryPoint: true, forbidExecutables: true, maxFileBytes: 2);

			issues.Select(issue => issue.Code).Should().Contain(new[] {
				"CONTENT_ENTRY_POINT_REQUIRED",
				"CONTENT_EXECUTABLE_FORBIDDEN",
				"CONTENT_DUPLICATE_PAYLOAD",
				"CONTENT_FILE_OVERSIZED",
			});
		}

		[Test]
		public void SkipsSymbolicLinksInsteadOfFollowingThem()
		{
			var source = CreateFixture();
			var outside = Path.Combine(_root, "outside.txt");
			File.WriteAllText(outside, "outside");
			var link = Path.Combine(source, "outside-link.txt");
			var createSymbolicLink = typeof(File).GetMethod("CreateSymbolicLink", new[] { typeof(string), typeof(string) });
			if (createSymbolicLink == null) {
				Assert.Ignore("Symbolic link creation is unavailable on this runtime.");
			}
			try {
				createSymbolicLink.Invoke(null, new object[] { link, outside });
			} catch (Exception) {
				Assert.Ignore("Symbolic link creation is unavailable on this host.");
			}
			string warning = null;
			var prepared = PackagedContentWriter.PrepareDirectory("test", source, new ContentPackOptions { Warning = value => warning = value });
			prepared.Manifest.Files.Select(file => file.Path).Should().NotContain("outside-link.txt");
			warning.Should().Contain("Skipping symbolic link");
		}

		[Test, Category("Performance")]
		public async Task Synthetic400MbBundlePerformance()
		{
			var source = Path.Combine(_root, "perf-source");
			Directory.CreateDirectory(source);
			var bigFile = Path.Combine(source, "payload.bin");
			using (var stream = new FileStream(bigFile, FileMode.CreateNew, FileAccess.Write)) {
				stream.SetLength(400L * 1024 * 1024);
			}
			var package = Path.Combine(_root, "perf.vpe");
			var stopwatch = Stopwatch.StartNew();
			var contentRef = CreatePackage(package, source);
			var packMs = stopwatch.ElapsedMilliseconds;
			var resolver = new PackagedContentResolver(package, CacheOptions(Path.Combine(_root, "perf-cache")));
			stopwatch.Restart();
			await resolver.ResolveAsync(contentRef, null, CancellationToken.None);
			var firstLoadMs = stopwatch.ElapsedMilliseconds;
			stopwatch.Restart();
			await resolver.ResolveAsync(contentRef, null, CancellationToken.None);
			var cachedLoadMs = stopwatch.ElapsedMilliseconds;
			var result = $"400 MB: pack={packMs}ms, first-load={firstLoadMs}ms, cached-load={cachedLoadMs}ms";
			Console.WriteLine(result);
		}

		private string CreateFixture(string name = "source")
		{
			var source = Path.Combine(_root, name);
			Directory.CreateDirectory(Path.Combine(source, "machine", "deep", "a", "b", "c"));
			Directory.CreateDirectory(Path.Combine(source, "media"));
			Directory.CreateDirectory(Path.Combine(source, "fonts"));
			File.WriteAllText(Path.Combine(source, "machine", "config.yaml"), "name: synthetic\n");
			File.WriteAllBytes(Path.Combine(source, "machine", "deep", "a", "b", "c", "data.bin"), new byte[] { 0, 1, 2, 3 });
			File.WriteAllBytes(Path.Combine(source, "media", "large.mp4"), new byte[2 * 1024 * 1024]);
			File.WriteAllText(Path.Combine(source, "fonts", "Tést Font.ttf"), "synthetic font");
			File.WriteAllBytes(Path.Combine(source, "empty.py"), Array.Empty<byte>());
			return source;
		}

		private static PackagedContentRef CreatePackage(string package, string source, ContentPackOptions options = null)
		{
			using var storage = PackageApi.StorageManager.CreateStorage(package);
			var table = storage.AddFolder(PackageApi.TableFolder);
			return new PackagedContentWriter(table).AddDirectory("test", source, options);
		}

		private static PackagedContentCacheOptions CacheOptions(string root) => new() { CacheRoot = root };

		private sealed class InlineProgress : IProgress<float>
		{
			private readonly Action<float> _report;
			public InlineProgress(Action<float> report) => _report = report;
			public void Report(float value) => _report(value);
		}
	}
}
