// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// See LICENSE for the terms of use and distribution.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using VisualPinball.Plugins;

namespace VisualPinball.Plugins.Test
{
	[TestFixture]
	public sealed class ConfigurationTests
	{
		private string _directory = null!;
		private readonly PluginDescriptor _plugin = new PluginDescriptor("test.config", "Settings", "Test");
		private readonly GameIdentity _game = new GameIdentity("pinmame", "t2_l8");
		private PluginSettingsSchema Schema() => new PluginSettingsSchema(1, new PluginSettingsGroup("Test", "",
			new PluginField("root", "Root", SettingKind.Folder) { Default = "default" },
			new PluginField("variant", "Variant", SettingKind.Choice) { Scope = ConfigurationScope.GlobalAndGame, Default = "auto", Provider = "variants" },
			new PluginField("layout", "Layout", SettingKind.Collection) { Scope = ConfigurationScope.GlobalAndGame, Default = new JArray("default") }));
		[SetUp] public void SetUp() => _directory = Path.Combine(Path.GetTempPath(), "vpe-plugin-tests", Guid.NewGuid().ToString("N"));
		[TearDown] public void TearDown() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
		[Test]
		public void GlobalDefaultsAndSparseGameOverridesRemainIndependent()
		{
			var store = new PluginConfigurationStore(_directory); var schema = Schema();
			store.Save(_plugin, schema, new PluginSettingsSnapshot(1, JObject.Parse("{'root':'global','variant':'one','layout':['a','b']}")));
			store.Save(_plugin, schema, new PluginSettingsSnapshot(1, JObject.Parse("{'variant':'two','layout':['c']}")), _game);
			Assert.That(store.Effective(_plugin, schema, _game).String("root"), Is.EqualTo("global"));
			Assert.That(store.Effective(_plugin, schema, _game).String("variant"), Is.EqualTo("two"));
			Assert.That(store.Effective(_plugin, schema, _game).Value("layout")!.ToString(), Is.EqualTo(new JArray("c").ToString()));
			store.Save(_plugin, schema, new PluginSettingsSnapshot(1), _game);
			Assert.That(store.Effective(_plugin, schema, _game).String("variant"), Is.EqualTo("one"));
		}
		[Test]
		public void ManuallyStoredValuesCannotOverrideFieldsOutsideTheirDeclaredScope()
		{
			var schema = Schema();
			var resolved = PluginConfigurationStore.Resolve(schema,
				new PluginSettingsSnapshot(1, JObject.Parse("{'root':'global','variant':'one'}")),
				new PluginSettingsSnapshot(1, JObject.Parse("{'root':'wrong','variant':'two','unknown':'kept'}")));
			Assert.That(resolved.String("root"), Is.EqualTo("global"));
			Assert.That(resolved.String("variant"), Is.EqualTo("two"));
			Assert.That(resolved.String("unknown"), Is.EqualTo("kept"));
		}
		[Test]
		public void UnknownFieldsSurviveSaveAndFutureVersionIsNeverOverwritten()
		{
			var store = new PluginConfigurationStore(_directory); var schema = Schema(); string path = store.SettingsPath(_plugin);
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			File.WriteAllText(path, "{'schemaVersion':1,'futureTop':42,'values':{'unknown':'kept','root':'old'}}");
			store.Save(_plugin, schema, new PluginSettingsSnapshot(1, JObject.Parse("{'root':'new'}")));
			var saved = JObject.Parse(File.ReadAllText(path));
			Assert.That(saved["values"]!["unknown"]!.Value<string>(), Is.EqualTo("kept"));
			Assert.That(saved.Value<int>("futureTop"), Is.EqualTo(42));
			saved["schemaVersion"] = 9; File.WriteAllText(path, saved.ToString()); var before = File.ReadAllText(path);
			Assert.Throws<InvalidOperationException>(() => store.Save(_plugin, schema, new PluginSettingsSnapshot(1)));
			Assert.That(File.ReadAllText(path), Is.EqualTo(before));
		}
		[Test]
		public void MigrationsOperateOnDetachedTreeAndPersistOnlyOnApply()
		{
			var store = new PluginConfigurationStore(_directory); string path = store.SettingsPath(_plugin);
			Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, "{'schemaVersion':1,'values':{'old':'value'}}");
			var schema = new PluginSettingsSchema(2, new PluginSettingsGroup("Migrated", "", new PluginField("new", "New", SettingKind.Text))) {
				Migrations = new Dictionary<int, Func<JObject, JObject>> { [1] = tree => { tree["new"] = tree["old"]; tree.Remove("old"); return tree; } }
			};
			Assert.That(store.Read(_plugin, schema).String("new"), Is.EqualTo("value"));
			Assert.That(JObject.Parse(File.ReadAllText(path)).Value<int>("schemaVersion"), Is.EqualTo(1));
			store.Save(_plugin, schema, store.Read(_plugin, schema));
			Assert.That(JObject.Parse(File.ReadAllText(path)).Value<int>("schemaVersion"), Is.EqualTo(2));
		}
		[Test]
		public async Task ApplyRechecksAvailabilityAndDoesNotReplaceMissingChoice()
		{
			var store = new PluginConfigurationStore(_directory); var provider = new CatalogProvider();
			var factory = new ConfigFactory(_plugin, Schema(), provider);
			using var session = new PluginSettingsSession(factory, store, "windows", _game);
			session.Set("variant", "one"); Assert.That((await session.ApplyAsync()).Success, Is.True);
			provider.Available = false;
			Assert.That((await session.ApplyAsync()).Success, Is.False);
			Assert.That(store.Read(_plugin, Schema(), _game).String("variant"), Is.EqualTo("one"));
			session.Set("variant", null); Assert.That(session.Effective.String("variant"), Is.EqualTo("auto"));
		}
		private sealed class CatalogProvider : IPluginSettingsProvider
		{
			internal bool Available = true;
			public Task<PluginChoiceCatalog> DiscoverAsync(string provider, PluginChoiceRequest request, CancellationToken token) => Task.FromResult(new PluginChoiceCatalog(Available.ToString(), new[] { new PluginChoice("auto", "Automatic"), new PluginChoice("one", "One", available: Available, unavailableReason: "Removed") }));
			public Task<PluginActionResult> RunActionAsync(string action, PluginChoiceRequest request, CancellationToken token) => Task.FromResult(new PluginActionResult(true, ""));
			public IReadOnlyList<PluginValidationIssue> Validate(PluginChoiceRequest request) => Array.Empty<PluginValidationIssue>();
		}
		private sealed class ConfigFactory : IPlayerPluginFactory
		{
			public PluginDescriptor Descriptor { get; }
			public PluginSettingsSchema Schema { get; }
			public IPluginSettingsProvider? SettingsProvider { get; }
			internal ConfigFactory(PluginDescriptor descriptor, PluginSettingsSchema schema, IPluginSettingsProvider provider) { Descriptor = descriptor; Schema = schema; SettingsProvider = provider; }
			public IPlayerPlugin Create(PluginApplicationContext context) => throw new NotSupportedException();
		}
	}
}
