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
using VisualPinball.Plugins;

namespace VisualPinball.Plugins.Test
{
	[TestFixture]
	public sealed class PluginHostTests
	{
		private string _directory = null!;
		private readonly GameIdentity _game = new GameIdentity("test", "game");
		[SetUp] public void SetUp() => _directory = Path.Combine(Path.GetTempPath(), "vpe-plugin-tests", Guid.NewGuid().ToString("N"));
		[TearDown] public void TearDown() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }

		[Test]
		public void DisabledGamePolicyPreventsConstruction()
		{
			var factory = new TestFactory("test.disabled");
			var host = Host(factory);
			host.Configuration.SetEnabled(factory.Descriptor, false, _game);
			Run(host, host.PrepareAndStartAsync(_game, "test.vpe", "Test", "controller", 1, default));
			Assert.That(factory.Constructions, Is.Zero);
			Assert.That(host.Statuses[factory.Descriptor.Id].State, Is.EqualTo(PluginState.Disabled));
			Run(host, host.StopGameAsync());
			Run(host, host.PrepareAndStartAsync(new GameIdentity("test", "other"), "test.vpe", "Other", "controller", 2, default));
			Assert.That(factory.Constructions, Is.EqualTo(1));
			Run(host, host.StopGameAsync());
		}

		[Test]
		public void RequiredProviderPrecedesConsumerAndCleanupReversesStart()
		{
			var calls = new List<string>();
			var provider = new TestFactory("provider", calls); provider.Descriptor.Provides = new[] { "service" }; provider.Descriptor.Order = 20;
			var consumer = new TestFactory("consumer", calls); consumer.Descriptor.Requires = new[] { "service" }; consumer.Descriptor.Order = -20;
			var host = Host(consumer, provider);
			Run(host, host.PrepareAndStartAsync(_game, "test", "Test", "controller", 1, default));
			Assert.That(calls.Where(call => call.EndsWith(":start")), Is.EqualTo(new[] { "provider:start", "consumer:start" }));
			Run(host, host.StopGameAsync());
			Assert.That(calls.Where(call => call.EndsWith(":stop")), Is.EqualTo(new[] { "consumer:stop", "provider:stop" }));
			Assert.That(calls.Count(call => call.EndsWith(":dispose")), Is.EqualTo(2));
		}

		[TestCase("initialize")]
		[TestCase("prepare")]
		[TestCase("start")]
		public void ProviderFailureBlocksDependentsWhileIndependentPluginRuns(string failAt)
		{
			var calls = new List<string>();
			var provider = new TestFactory("provider", calls) { FailAt = failAt };
			var consumer = new TestFactory("consumer", calls); consumer.Descriptor.DependsOn = new[] { "provider" };
			var independent = new TestFactory("independent", calls);
			var host = Host(consumer, provider, independent);
			Run(host, host.PrepareAndStartAsync(_game, "test", "Test", "controller", 1, default));
			Assert.That(host.Statuses["provider"].State, Is.EqualTo(PluginState.Faulted));
			Assert.That(host.Statuses["consumer"].State, Is.EqualTo(PluginState.Blocked));
			Assert.That(host.Statuses["independent"].State, Is.EqualTo(PluginState.Running));
			Assert.That(calls.Count(call => call == "provider:dispose"), Is.EqualTo(1));
			Run(host, host.StopGameAsync());
			Assert.That(calls.Count(call => call == "provider:dispose"), Is.EqualTo(1));
		}

		[Test]
		public void ReadyFailureIsContainedAndResourcesAreReleased()
		{
			var calls = new List<string>();
			var host = Host(new TestFactory("failed", calls) { FailAt = "ready" }, new TestFactory("healthy", calls));
			Run(host, host.PrepareAndStartAsync(_game, "test", "Test", "controller", 1, default));
			Assert.DoesNotThrow(host.GameReady);
			Assert.That(calls, Does.Contain("healthy:ready"));
			Assert.That(calls, Does.Contain("failed:release"));
			Run(host, host.StopGameAsync());
		}

		[Test]
		public void WorkerContinuationReturnsToOwnerBeforeNextHook()
		{
			int owner = Thread.CurrentThread.ManagedThreadId;
			var factory = new TestFactory("async") { AsyncInitialize = true };
			var host = Host(factory);
			Run(host, host.PrepareAndStartAsync(_game, "test", "Test", "controller", 1, default));
			Assert.That(factory.PrepareThread, Is.EqualTo(owner));
			Run(host, host.StopGameAsync());
		}

		[Test]
		public void CancellationUnwindsWithFreshCleanupToken()
		{
			var calls = new List<string>();
			using var cancel = new CancellationTokenSource();
			var factory = new TestFactory("cancel", calls) { CancelDuringStart = cancel };
			var host = Host(factory);
			Assert.Throws<OperationCanceledException>(() => Run(host, host.PrepareAndStartAsync(_game, "test", "Test", "controller", 1, cancel.Token)));
			Assert.That(calls, Does.Contain("cancel:stop"));
			Assert.That(factory.CleanupWasCanceled, Is.False);
		}

		private PluginHost Host(params IPlayerPluginFactory[] factories) => new PluginHost(factories, new PluginConfigurationStore(_directory), new OwnerScheduler(), new PluginCapabilities(), new PluginResourceRegistry(), "windows");
		[Test]
		public void HealthyPluginKeepsTickingWhileAnotherPluginsCleanupWaits()
		{
			var calls = new List<string>(); var completion = new TaskCompletionSource<bool>();
			var host = Host(new TestFactory("failed", calls) { FailAt = "ready", StopCompletion = completion.Task }, new TestFactory("healthy", calls));
			Run(host, host.PrepareAndStartAsync(_game, "test", "Test", "controller", 1, default));
			host.GameReady(); Assert.That(host.IsTransitioning, Is.True);
			host.Tick(.016); Assert.That(calls, Does.Contain("healthy:tick"));
			completion.SetResult(true); Run(host, host.StopGameAsync());
		}
		[Test]
		public void HungLiveApplyDoesNotPersistOrBlockRelaunch()
		{
			var factory = new TestFactory("live") { ApplyCompletion = new TaskCompletionSource<PluginActionResult>().Task,
				Schema = new PluginSettingsSchema(1, new PluginSettingsGroup("Test", "", new PluginField("value", "Value", SettingKind.Integer) { Default = 5, Change = ConfigurationChange.Live })) };
			var host = Host(factory); host.StartupTimeout = TimeSpan.FromMilliseconds(30);
			Run(host, host.PrepareAndStartAsync(_game, "test", "Test", "controller", 1, default));
			using var session = new PluginSettingsSession(factory, host.Configuration, "windows"); session.Set("value", 42);
			var apply = session.ApplyAsync(host: host); Run(host, apply);
			Assert.That(apply.Result.Success, Is.False); Assert.That(File.Exists(host.Configuration.SettingsPath(factory.Descriptor)), Is.False);
			Run(host, host.StopGameAsync());
			Run(host, host.PrepareAndStartAsync(_game, "test", "Test", "controller", 2, default));
			Assert.That(host.Statuses["live"].State, Is.EqualTo(PluginState.Unresponsive));
			Assert.That(factory.Constructions, Is.EqualTo(1));
			Run(host, host.StopGameAsync());
		}
		[Test]
		public void AbandonedStartupIsQuarantinedUntilItsHookAndShutdownFinish()
		{
			var completion = new TaskCompletionSource<bool>();
			var factory = new TestFactory("late") { InitializeCompletion = completion.Task };
			var host = Host(factory); host.StartupTimeout = TimeSpan.FromMilliseconds(30);
			Run(host, host.PrepareAndStartAsync(_game, "test", "Test", "controller", 1, default));
			Assert.That(host.Statuses["late"].State, Is.EqualTo(PluginState.Unresponsive));
			Run(host, host.StopGameAsync());
			Run(host, host.PrepareAndStartAsync(_game, "test", "Test", "controller", 2, default));
			Assert.That(factory.Constructions, Is.EqualTo(1));
			completion.SetResult(true);
			var until = DateTime.UtcNow.AddSeconds(2);
			while (host.Statuses["late"].State == PluginState.Unresponsive && DateTime.UtcNow < until) { host.Scheduler.Pump(); Thread.Sleep(1); }
			Assert.That(factory.Calls, Does.Contain("late:dispose"));
			Run(host, host.PrepareAndStartAsync(_game, "test", "Test", "controller", 3, default));
			Assert.That(factory.Constructions, Is.EqualTo(2)); Run(host, host.StopGameAsync());
		}
		[Test]
		public void AResourceAcquiredAfterScopeClosureIsReleasedImmediately()
		{
			var scope = new ResourceScope(); scope.Dispose(); bool released = false;
			Assert.Throws<ObjectDisposedException>(() => scope.Own(new Subscription(() => released = true)));
			Assert.That(released, Is.True);
		}
		[Test]
		public void PauseAndResumeDuringFaultCleanupKeepTheLatestState()
		{
			var calls = new List<string>();
			var blockedStop = new TaskCompletionSource<bool>();
			var factory = new TestFactory("failed", calls) { FailAt = "ready", StopCompletion = blockedStop.Task };
			var host = Host(factory, new TestFactory("healthy", calls));
			Run(host, host.PrepareAndStartAsync(_game, "test", "Test", "controller", 1, default));
			host.GameReady();
			host.PauseChanged(true); host.PauseChanged(false);
			blockedStop.SetResult(true);
			while (host.IsTransitioning) { host.Scheduler.Pump(); Thread.Sleep(1); }
			Assert.That(calls, Does.Contain("healthy:resume"));
			Assert.That(calls, Does.Not.Contain("healthy:pause"));
			Run(host, host.StopGameAsync());
		}
		[Test]
		public void FutureDocumentsAreRejectedBeforeTouchingLiveSettings()
		{
			var calls = new List<string>();
			var factory = new TestFactory("live", calls) { Schema = new PluginSettingsSchema(1, new PluginSettingsGroup("Test", "",
				new PluginField("value", "Value", SettingKind.Integer) { Default = 5, Change = ConfigurationChange.Live })) };
			var host = Host(factory);
			Run(host, host.PrepareAndStartAsync(_game, "test", "Test", "controller", 1, default));
			string path = host.Configuration.SettingsPath(factory.Descriptor);
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			const string future = "{'schemaVersion':9,'values':{'value':5,'future':'kept'}}";
			File.WriteAllText(path, future);
			using var session = new PluginSettingsSession(factory, host.Configuration, "windows");
			session.Set("value", 42);
			Assert.Throws<InvalidOperationException>(() => Run(host, session.ApplyAsync(host: host)));
			Assert.That(calls, Does.Not.Contain("live:apply:42"));
			Assert.That(File.ReadAllText(path), Is.EqualTo(future));
			Run(host, host.StopGameAsync());
		}
		[Test]
		public void UncooperativeCleanupDoesNotBlockOtherPluginsOrExit()
		{
			var calls = new List<string>();
			var factory = new TestFactory("stuck", calls) { NeverFinishStop = true };
			var host = Host(factory, new TestFactory("healthy", calls));
			host.CleanupTimeout = TimeSpan.FromMilliseconds(30);
			Run(host, host.PrepareAndStartAsync(_game, "test", "Test", "controller", 1, default));
			Run(host, host.StopGameAsync());
			Assert.That(host.Statuses["stuck"].State, Is.EqualTo(PluginState.Unresponsive));
			Assert.That(calls, Does.Contain("healthy:stop"));
			Assert.That(calls, Does.Contain("stuck:release"));
			Assert.That(host.IsRunning, Is.False);
		}
		[Test]
		public void BadPolicyAndOrderingCycleOnlyBlockAffectedPlugins()
		{
			var bad = new TestFactory("bad"); var a = new TestFactory("cycle.a"); var b = new TestFactory("cycle.b"); var healthy = new TestFactory("healthy");
			a.Descriptor.After = new[] { b.Descriptor.Id }; b.Descriptor.After = new[] { a.Descriptor.Id };
			var host = Host(bad, a, b, healthy);
			Directory.CreateDirectory(host.Configuration.DirectoryFor(bad.Descriptor));
			File.WriteAllText(Path.Combine(host.Configuration.DirectoryFor(bad.Descriptor), "policy.json"), "invalid JSON");
			Run(host, host.PrepareAndStartAsync(_game, "test", "Test", "controller", 1, default));
			Assert.That(host.Statuses["bad"].State, Is.EqualTo(PluginState.Faulted));
			Assert.That(host.Statuses["cycle.a"].State, Is.EqualTo(PluginState.Blocked));
			Assert.That(host.Statuses["cycle.b"].State, Is.EqualTo(PluginState.Blocked));
			Assert.That(host.Statuses["healthy"].State, Is.EqualTo(PluginState.Running));
			Run(host, host.StopGameAsync());
		}
		internal static void Run(PluginHost host, Task operation)
		{
			var deadline = DateTime.UtcNow.AddSeconds(5);
			while (!operation.IsCompleted && DateTime.UtcNow < deadline) { host.Scheduler.Pump(); Thread.Sleep(1); }
			Assert.That(operation.IsCompleted, Is.True, "The owner scheduler did not complete the operation.");
			operation.GetAwaiter().GetResult();
		}
	}

	internal sealed class TestFactory : IPlayerPluginFactory
	{
		public PluginDescriptor Descriptor { get; }
		public PluginSettingsSchema Schema { get; set; } = new PluginSettingsSchema(1);
		public IPluginSettingsProvider? SettingsProvider { get; set; }
		internal readonly List<string> Calls;
		internal string FailAt = "";
		internal int Constructions;
		internal int PrepareThread;
		internal bool AsyncInitialize;
		internal bool CleanupWasCanceled;
		internal bool NeverFinishStop;
		internal Task? StopCompletion;
		internal Task? InitializeCompletion;
		internal Task<PluginActionResult>? ApplyCompletion;
		internal Action<int>? ApplyAction;
		internal bool SynchronousApply;
		internal CancellationTokenSource? CancelDuringStart;
		internal TestFactory(string id, List<string>? calls = null) { Descriptor = new PluginDescriptor(id, id, "Test") { EnabledByDefault = true }; Calls = calls ?? new List<string>(); }
		public IPlayerPlugin Create(PluginApplicationContext context) { Constructions++; context.Lifetime.Own(new Subscription(() => Calls.Add(Descriptor.Id + ":release"))); return SynchronousApply ? new SynchronousTestPlugin(this) : new TestPlugin(this); }
		private sealed class SynchronousTestPlugin : TestPlugin, IPluginSynchronousReconfiguration
		{
			internal SynchronousTestPlugin(TestFactory factory) : base(factory) { }
			public PluginActionResult Apply(PluginSettingsSnapshot settings) => ApplyAsync(settings, default).GetAwaiter().GetResult();
		}
		private class TestPlugin : PlayerPlugin, IPluginReconfiguration, IPluginTick
		{
			private readonly TestFactory _factory;
			internal TestPlugin(TestFactory factory) { _factory = factory; }
			private void Call(string phase) { _factory.Calls.Add(_factory.Descriptor.Id + ":" + phase); if (_factory.FailAt == phase) throw new InvalidOperationException("Test failure"); }
			public override async Task InitializeAsync(CancellationToken token) { Call("initialize"); if (_factory.AsyncInitialize) await Task.Run(() => Thread.Sleep(5), token).ConfigureAwait(false); if (_factory.InitializeCompletion != null) await _factory.InitializeCompletion; }
			public override Task PrepareGameAsync(PluginGameContext game, CancellationToken token) { _factory.PrepareThread = Thread.CurrentThread.ManagedThreadId; Call("prepare"); return Task.CompletedTask; }
			public override Task StartGameAsync(CancellationToken token) { Call("start"); _factory.CancelDuringStart?.Cancel(); token.ThrowIfCancellationRequested(); return Task.CompletedTask; }
			public override void GameReady() => Call("ready");
			public override void PauseChanged(bool paused) => Call(paused ? "pause" : "resume");
			public void Tick(double delta) => Call("tick");
			public Task<PluginActionResult> ApplyAsync(PluginSettingsSnapshot settings, CancellationToken cancellation) { Call("apply:" + settings.Integer("value")); _factory.ApplyAction?.Invoke(settings.Integer("value")); return _factory.ApplyCompletion ?? Task.FromResult(new PluginActionResult(true, "")); }
			public override Task StopGameAsync(CancellationToken token) { _factory.CleanupWasCanceled = token.IsCancellationRequested; Call("stop"); return _factory.StopCompletion ?? (_factory.NeverFinishStop ? new TaskCompletionSource<bool>().Task : Task.CompletedTask); }
			public override Task ShutdownAsync(CancellationToken token) { Call("shutdown"); return Task.CompletedTask; }
			public override void Dispose() => Call("dispose");
		}
	}
}
