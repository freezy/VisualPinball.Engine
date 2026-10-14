// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

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
		[Test]
		public void ScreenLayoutsAllowSignedDesktopCoordinatesAndRejectAmbiguousOrInvalidHomes()
		{
			var schema = new PluginSettingsSchema(1, new PluginSettingsGroup("Displays", "", new PluginField("screens", "Screens", SettingKind.ScreenLayout)));
			var valid = JObject.Parse("{'screens':[{'screen':'backglass','x':-1920,'y':0,'width':1920,'height':1080}]}");
			Assert.That(schema.Validate(new PluginSettingsSnapshot(1, valid)), Is.Empty);
			((JArray)valid["screens"]!).Add(valid["screens"]![0]!.DeepClone());
			Assert.That(schema.Validate(new PluginSettingsSnapshot(1, valid)), Has.Count.EqualTo(1));
			valid["screens"] = JArray.Parse("[{'screen':'backglass','x':0,'y':0,'width':0,'height':1080}]");
			Assert.That(schema.Validate(new PluginSettingsSnapshot(1, valid)), Has.Count.EqualTo(1));
		}
		[Test]
		public void MultipleChoicesAreListsOfIds()
		{
			var schema = new PluginSettingsSchema(1, new PluginSettingsGroup("Test", "",
				new PluginField("output", "Output", SettingKind.Choice) { Provider = "outputs", Multiple = true, Default = new JArray("native") },
				new PluginField("single", "Single", SettingKind.Choice) { Provider = "outputs", Default = "native" }));
			Assert.That(schema.Validate(new PluginSettingsSnapshot(1, JObject.Parse("{'output':['native','scene'],'single':'scene'}"))), Is.Empty);
			Assert.That(schema.Validate(new PluginSettingsSnapshot(1, JObject.Parse("{'output':[]}"))), Is.Empty, "picking none is valid");
			Assert.That(schema.Validate(new PluginSettingsSnapshot(1, JObject.Parse("{'output':'native'}"))), Has.Count.EqualTo(1));
			Assert.That(schema.Validate(new PluginSettingsSnapshot(1, JObject.Parse("{'output':['native',3]}"))), Has.Count.EqualTo(1));
			Assert.That(schema.Validate(new PluginSettingsSnapshot(1, JObject.Parse("{'single':['native']}"))), Has.Count.EqualTo(1));
			var values = new PluginSettingsSnapshot(1, JObject.Parse("{'output':['native',1,'scene'],'single':'scene'}"));
			Assert.That(values.Strings("output"), Is.EqualTo(new[] { "native", "scene" }));
			Assert.That(values.Strings("single"), Is.Empty);
			Assert.That(values.Strings("missing"), Is.Empty);
			Assert.That(schema.Defaults().Strings("output"), Is.EqualTo(new[] { "native" }));
		}
		[Test]
		public void NestedRecordValidationChecksDeclaredTypesAndRanges()
		{
			var item = new PluginField("devices", "Devices", SettingKind.Collection) { Children = new[] { new PluginField("gain", "Gain", SettingKind.Number) { Minimum = 0, Maximum = 1 } } };
			var schema = new PluginSettingsSchema(1, new PluginSettingsGroup("Output", "", item));
			Assert.That(schema.Validate(new PluginSettingsSnapshot(1, JObject.Parse("{'devices':[{'gain':0.5}]}"))), Is.Empty);
			Assert.That(schema.Validate(new PluginSettingsSnapshot(1, JObject.Parse("{'devices':[{'gain':2.0}]}"))), Has.Count.EqualTo(1));
			Assert.That(schema.Validate(new PluginSettingsSnapshot(1, JObject.Parse("{'devices':['invalid']}"))), Has.Count.EqualTo(1));
		}
		[Test]
		public async Task ScreenApplyRechecksAvailabilityWithoutReplacingAnExistingHome()
		{
			var field = new PluginField("screens", "Screens", SettingKind.ScreenLayout) { Provider = "variants", Scope = ConfigurationScope.GlobalAndGame };
			var schema = new PluginSettingsSchema(1, new PluginSettingsGroup("Displays", "", field));
			var store = new PluginConfigurationStore(_directory); var provider = new CatalogProvider();
			var factory = new ConfigFactory(_plugin, schema, provider);
			using var session = new PluginSettingsSession(factory, store, "windows", _game);
			session.Set("screens", JArray.Parse("[{'screen':'one','x':-640,'y':0,'width':640,'height':480}]"));
			Assert.That((await session.ApplyAsync()).Success, Is.True);
			provider.Available = false;
			Assert.That((await session.ApplyAsync()).Success, Is.False);
			Assert.That(store.Read(_plugin, schema, _game).Value("screens")![0]!["x"]!.Value<int>(), Is.EqualTo(-640));
		}
		[Test]
		public async Task NestedChoiceAvailabilityIsRecheckedBeforeSavingRecords()
		{
			var field = new PluginField("devices", "Devices", SettingKind.Collection) {
				Children = new[] { new PluginField("variant", "Variant", SettingKind.Choice) { Provider = "variants", Default = "auto" } }
			};
			var schema = new PluginSettingsSchema(1, new PluginSettingsGroup("Output", "", field));
			var store = new PluginConfigurationStore(_directory); var provider = new CatalogProvider();
			using var session = new PluginSettingsSession(new ConfigFactory(_plugin, schema, provider), store, "windows");
			session.Set("devices", JArray.Parse("[{'variant':'one'}]"));
			Assert.That((await session.ApplyAsync()).Success, Is.True);
			provider.Available = false;
			Assert.That((await session.ApplyAsync()).Success, Is.False);
			Assert.That(store.Read(_plugin, schema).Value("devices")![0]!["variant"]!.Value<string>(), Is.EqualTo("one"));
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
