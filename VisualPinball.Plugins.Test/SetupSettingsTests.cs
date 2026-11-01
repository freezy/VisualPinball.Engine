// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace VisualPinball.Plugins.Test
{
	[TestFixture]
	public sealed class SetupSettingsTests
	{
		private string _directory = null!;
		[SetUp] public void SetUp() => _directory = Path.Combine(Path.GetTempPath(), "vpe-setup-tests", Guid.NewGuid().ToString("N"));
		[TearDown] public void TearDown() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
		private PluginHost Host(TestFactory factory) => new PluginHost(new[] { factory }, new PluginConfigurationStore(_directory), new OwnerScheduler(), new PluginCapabilities(), new PluginResourceRegistry(), "windows");
		[Test]
		public void RequiredSettingsCanBeSavedOneAtATimeWithoutLosingOtherValues()
		{
			var factory = new TestFactory("setup") { Schema = new PluginSettingsSchema(1, new PluginSettingsGroup("Setup", "",
				new PluginField("first", "First", SettingKind.Text) { Default = "", Validate = value => string.IsNullOrEmpty(value?.ToString()) ? "Choose first" : null },
				new PluginField("second", "Second", SettingKind.Text) { Default = "", Validate = value => string.IsNullOrEmpty(value?.ToString()) ? "Choose second" : null })) };
			var host = Host(factory);
			var save = host.CommitSettingAsync(factory, "first", new JValue("one")); PluginHostTests.Run(host, save);
			Assert.That(save.Result.Success, Is.True, save.Result.Message);
			Assert.That(factory.Schema.Validate(host.Configuration.Effective(factory.Descriptor, factory.Schema)).Count, Is.EqualTo(1));
			save = host.CommitSettingAsync(factory, "second", new JValue("two")); PluginHostTests.Run(host, save);
			Assert.That(save.Result.Success, Is.True); var saved = host.Configuration.Effective(factory.Descriptor, factory.Schema);
			Assert.That(saved.String("first"), Is.EqualTo("one")); Assert.That(factory.Schema.Validate(saved), Is.Empty);
		}
		[Test]
		public void GameSetupWritesOnlyThatGameAndChecksDynamicChoices()
		{
			var provider = new Provider();
			var factory = new TestFactory("setup") { SettingsProvider = provider, Schema = new PluginSettingsSchema(1, new PluginSettingsGroup("Setup", "",
				new PluginField("image", "Image", SettingKind.Choice) { Scope = ConfigurationScope.GameOnly, Provider = "images", Default = "" })) };
			var host = Host(factory); var game = new GameIdentity("test", "game"); var package = new PluginPackageReference("selected.vpe", "Selected");
			var save = host.CommitSettingAsync(factory, "image", new JValue("gone"), game, package); PluginHostTests.Run(host, save);
			Assert.That(save.Result.Success, Is.False); Assert.That(provider.Package, Is.SameAs(package));
			save = host.CommitSettingAsync(factory, "image", new JValue("available"), game, package); PluginHostTests.Run(host, save);
			Assert.That(save.Result.Success, Is.True); Assert.That(host.Configuration.Effective(factory.Descriptor, factory.Schema, game).String("image"), Is.EqualTo("available"));
			Assert.That(host.Configuration.Read(factory.Descriptor, factory.Schema).Values, Is.Empty);
			Assert.Throws<ArgumentException>(() => host.CommitSettingAsync(factory, "image", new JValue("available")));
		}
		[Test]
		public void ActionProgressReachesTheCallerWithoutSavingDrafts()
		{
			var provider = new Provider(); var factory = new TestFactory("setup") { SettingsProvider = provider };
			var host = Host(factory); using var session = new PluginSettingsSession(factory, host.Configuration, "windows");
			var progress = new ProgressSink(); var run = session.RunActionAsync("prepare", default, progress); PluginHostTests.Run(host, run);
			Assert.That(run.Result.Success, Is.True); Assert.That(progress.Last.Fraction, Is.EqualTo(.5)); Assert.That(progress.Last.Message, Is.EqualTo("Preparing files"));
			Assert.That(Directory.Exists(_directory), Is.False);
		}
		private sealed class ProgressSink : IProgress<PluginActionProgress>
		{ internal PluginActionProgress Last; public void Report(PluginActionProgress value) => Last = value; }
		private sealed class Provider : IPluginSettingsProvider
		{
			internal PluginPackageReference? Package;
			public Task<PluginChoiceCatalog> DiscoverAsync(string provider, PluginChoiceRequest request, CancellationToken cancellation)
			{ Package = request.Package; return Task.FromResult(new PluginChoiceCatalog("one", new[] { new PluginChoice("available", "Available") })); }
			public IReadOnlyList<PluginValidationIssue> Validate(PluginChoiceRequest request) => Array.Empty<PluginValidationIssue>();
			public Task<PluginActionResult> RunActionAsync(string action, PluginChoiceRequest request, CancellationToken cancellation)
			{ request.Progress?.Report(new PluginActionProgress(.5, "Preparing files")); return Task.FromResult(new PluginActionResult(true, "Ready")); }
		}
	}
}
