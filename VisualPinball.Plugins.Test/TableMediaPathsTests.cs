// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace VisualPinball.Plugins.Test
{
	public sealed class TableMediaPathsTests
	{
		private string _root = null!;

		[SetUp]
		public void SetUp() => _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))).FullName;

		[TearDown]
		public void TearDown() => Directory.Delete(_root, recursive: true);

		[Test]
		public void LooksUpEachComponentAndRetainsItsSpelling()
		{
			string mediaRoot = Directory.CreateDirectory(Path.Combine(_root, "Sérum")).FullName;
			string local = Directory.CreateDirectory(Path.Combine(mediaRoot, "PINMAME", "ROM")).FullName;
			string fallback = Directory.CreateDirectory(Path.Combine(mediaRoot, "Rom")).FullName;
			string file = Path.Combine(local, "ROM.cRZ");
			File.WriteAllText(file, "");

			var media = TableMediaPaths.FindDirectory(_root, "sérum");
			Assert.That(TableMediaPaths.GameDirectories(media, new GameIdentity("pinmame", "rom")).ToArray(),
				Is.EqualTo(new[] { local, fallback }));
			Assert.That(TableMediaPaths.FindFile(local, "rom.crz"), Is.EqualTo(file));
		}

		[TestCase("..")]
		[TestCase("../outside")]
		[TestCase("pinmame\\rom")]
		[TestCase("C:outside")]
		public void RejectsPathsAsGameKeys(string key)
		{
			Assert.That(TableMediaPaths.GameDirectories(_root, new GameIdentity("pinmame", key)), Is.Empty);
			Assert.That(TableMediaPaths.FindDirectory(_root, key), Is.Null);
		}
	}
}
