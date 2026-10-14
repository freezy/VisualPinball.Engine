// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace VisualPinball.Plugins.Test
{
	[TestFixture]
	public sealed class GlobalSettingsTests
	{
		private string _directory = null!;
		private PluginConfigurationStore _store = null!;
		[SetUp] public void SetUp() { _directory = Path.Combine(Path.GetTempPath(), "vpe-mixer-tests", Guid.NewGuid().ToString("N")); _store = new PluginConfigurationStore(_directory); }
		[TearDown] public void TearDown() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
		private static PluginField Volume(string id = "value") => new PluginField(id, "Volume", SettingKind.Integer) {
			Default = 100, Minimum = 0, Maximum = 100, Change = ConfigurationChange.Live, GlobalSetting = new PluginGlobalSetting(PluginGlobalSections.VolumeMixer)
		};
		private static TestFactory Factory(string id = "sound") => new TestFactory(id) {
			Schema = new PluginSettingsSchema(1, new PluginSettingsGroup("Sound", "", Volume(), new PluginField("path", "Library", SettingKind.Text) { Default = "old" }))
		};
		private PluginHost Host(params IPlayerPluginFactory[] factories) => new PluginHost(factories, _store, new OwnerScheduler(), new PluginCapabilities(), new PluginResourceRegistry(), "windows");
		[Test]
		public void OnlyExplicitSupportedVisibleAndCompatibleFieldsAppear()
		{
			var first = Factory("first"); first.Schema.Fields.First().GlobalSetting!.Order = 20;
			var second = Factory("second"); second.Schema.Fields.First().GlobalSetting!.Order = 10;
			var unsupported = Factory("unsupported"); unsupported.Descriptor.Platforms = new[] { "linux" };
			var game = Factory("game"); game.Schema.Fields.First().Scope = ConfigurationScope.GameOnly;
			var unbounded = Factory("unbounded"); unbounded.Schema.Fields.First().Maximum = null;
			var absent = new TestFactory("absent"); var broken = Factory("broken");
			Directory.CreateDirectory(_store.DirectoryFor(broken.Descriptor)); File.WriteAllText(_store.SettingsPath(broken.Descriptor), "bad json");
			var diagnostics = new List<string>();
			var found = PluginGlobalSettingBinding.Discover(new[] { first, second, unsupported, game, unbounded, absent, broken }, _store, "windows", PluginGlobalSections.VolumeMixer, diagnostics.Add);
			Assert.That(found.Select(item => item.Id), Is.EqualTo(new[] { "second/value", "first/value" }));
			Assert.That(diagnostics.Count, Is.EqualTo(3));
			Assert.That(PluginGlobalSettingBinding.Discover(new[] { first }, _store, "windows", "unknown"), Is.Empty);
		}
		[Test]
		public void OrdinaryDraftCannotOverwriteANewerMixerValueAndResetIsExplicit()
		{
			var factory = Factory(); var host = Host(factory);
			using var session = new PluginSettingsSession(factory, _store, "windows");
			session.Set("path", "new");
			PluginHostTests.Run(host, host.CommitGlobalSettingAsync(factory, "value", new JValue(37)));
			var apply = session.ApplyAsync(host: host); PluginHostTests.Run(host, apply);
			Assert.That(apply.Result.Success, Is.True);
			Assert.That(_store.Effective(factory.Descriptor, factory.Schema).Integer("value"), Is.EqualTo(37));
			Assert.That(_store.Effective(factory.Descriptor, factory.Schema).String("path"), Is.EqualTo("new"));
			session.Reset(); PluginHostTests.Run(host, session.ApplyAsync(host: host));
			Assert.That(_store.Read(factory.Descriptor, factory.Schema).Values, Is.Empty);
			Assert.That(_store.Effective(factory.Descriptor, factory.Schema).Integer("value"), Is.EqualTo(100));
		}
		[Test]
		public void AliasRefreshKeepsDirtyFieldsAndInvalidValuesAreNeverPersisted()
		{
			var factory = Factory(); var host = Host(factory);
			using var session = new PluginSettingsSession(factory, _store, "windows");
			session.Set("path", "draft");
			var latest = new PluginSettingsSnapshot(1); latest.Set("value", 30); latest.Set("path", "external");
			session.RefreshUnedited(latest, new[] { "value", "path" });
			Assert.That(session.Effective.Integer("value"), Is.EqualTo(30));
			Assert.That(session.HasEdits("value"), Is.False); Assert.That(session.Draft.String("path"), Is.EqualTo("draft"));
			var invalid = host.CommitGlobalSettingAsync(factory, "value", new JValue(101)); PluginHostTests.Run(host, invalid);
			Assert.That(invalid.Result.Success, Is.False); Assert.That(File.Exists(_store.SettingsPath(factory.Descriptor)), Is.False);
		}
		[Test]
		public void MandatorySettingsServiceIgnoresLegacyDisablePolicies()
		{
			var factory = Factory(); factory.Descriptor.CanDisable = false;
			Directory.CreateDirectory(_store.DirectoryFor(factory.Descriptor)); File.WriteAllText(Path.Combine(_store.DirectoryFor(factory.Descriptor), "policy.json"), "{'enabled':false}");
			Assert.That(_store.Enabled(factory.Descriptor), Is.True);
			Assert.Throws<InvalidOperationException>(() => _store.SetEnabled(factory.Descriptor, false));
			var host = Host(factory); PluginHostTests.Run(host, host.PrepareAndStartAsync(new GameIdentity("test", "game"), "test", "Test", "controller", 1, default));
			Assert.That(factory.Constructions, Is.EqualTo(1)); PluginHostTests.Run(host, host.StopGameAsync());
		}
		[Test]
		public void FailedDiskReplaceRollsBackGainWithoutSuppressingTicks()
		{
			if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows)) Assert.Ignore("Windows file-sharing semantics are required for this persistence fault injection.");
			var factory = Factory(); factory.SynchronousApply = true;
			var initial = new PluginSettingsSnapshot(1); initial.Set("value", 5); _store.Save(factory.Descriptor, factory.Schema, initial);
			var host = Host(factory); PluginHostTests.Run(host, host.PrepareAndStartAsync(new GameIdentity("test", "game"), "test", "Test", "controller", 1, default));
			FileStream? held = null;
			factory.ApplyAction = level => { if (level == 42) held = new FileStream(_store.SettingsPath(factory.Descriptor), FileMode.Open, FileAccess.Read, FileShare.Read); };
			var apply = host.CommitGlobalSettingAsync(factory, "value", new JValue(42));
			try {
				var deadline = DateTime.UtcNow.AddSeconds(5);
				while (!apply.IsCompleted && DateTime.UtcNow < deadline) { host.Scheduler.Pump(); if (held != null) host.Tick(.016); Thread.Sleep(1); }
				PluginHostTests.Run(host, apply);
				Assert.That(apply.Result.Success, Is.False); Assert.That(factory.Calls, Does.Contain("sound:apply:5"));
				Assert.That(factory.Calls, Does.Contain("sound:tick"));
				Assert.That(_store.Effective(factory.Descriptor, factory.Schema).Integer("value"), Is.EqualTo(5));
			} finally { held?.Dispose(); PluginHostTests.Run(host, host.StopGameAsync()); }
		}
	}
}
