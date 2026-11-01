// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: MIT

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
	public sealed class LaunchCheckTests
	{
		private string _directory = null!;
		private readonly GameIdentity _game = new GameIdentity("test", "game");
		[SetUp] public void SetUp() => _directory = Path.Combine(Path.GetTempPath(), "vpe-launch-tests", Guid.NewGuid().ToString("N"));
		[TearDown] public void TearDown() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
		private PluginHost Host(params IPlayerPluginFactory[] factories) => new PluginHost(factories, new PluginConfigurationStore(_directory), new OwnerScheduler(), new PluginCapabilities(), new PluginResourceRegistry(), "windows");
		private static void Run(PluginHost host, Task task) => PluginHostTests.Run(host, task);

		[Test]
		public void UnmetRequirementsAreCollectedBeforeAnyPluginIsConstructed()
		{
			var first = new CheckFactory("first") { Issues = new[] { new PluginValidationIssue("rom", "Prepare ROM") } };
			var second = new CheckFactory("second") { Issues = new[] { new PluginValidationIssue("path", "Choose a path") } };
			var optional = new TestFactory("optional"); var host = Host(first, second, optional);
			var check = host.CheckLaunchAsync(_game, "test.vpe", "Test", default); Run(host, check);
			Assert.That(check.Result.Select(b => b.Plugin.Id), Is.EqualTo(new[] { "first", "second" }));
			Assert.That(check.Result[0].Issues[0].Field, Is.EqualTo("rom"));
			Assert.Throws<PluginLaunchBlockedException>(() => Run(host, host.PrepareAndStartAsync(_game, "test.vpe", "Test", "controller", 1, default)));
			Assert.That(first.Inner.Constructions + second.Inner.Constructions + optional.Constructions, Is.Zero);
			Assert.That(host.IsRunning, Is.False); Assert.That(host.IsTransitioning, Is.False);
		}

		[Test]
		public void DisabledAndUnrelatedPluginsDoNotBlockLaunch()
		{
			var disabled = new CheckFactory("disabled"); var unrelated = new CheckFactory("unrelated") { Applies = false };
			var host = Host(disabled, unrelated); host.Configuration.SetEnabled(disabled.Descriptor, false, _game);
			Run(host, host.PrepareAndStartAsync(_game, "test.vpe", "Test", "controller", 1, default));
			Assert.That(disabled.Checks + unrelated.Checks, Is.Zero); Assert.That(host.IsRunning, Is.True);
			Run(host, host.StopGameAsync());
		}

		[Test]
		public void ChecksReceiveEffectiveGameSettingsAndPackageOnOwnerThread()
		{
			var factory = new CheckFactory("settings");
			factory.Inner.Schema = new PluginSettingsSchema(1, new PluginSettingsGroup("Test", "", new PluginField("path", "Path", SettingKind.Text) { Default = "default", Scope = ConfigurationScope.GlobalAndGame }));
			var host = Host(factory); var values = new PluginSettingsSnapshot(1, new JObject { ["path"] = "per-game" });
			host.Configuration.Save(factory.Descriptor, factory.Schema, values, _game);
			var task = host.CheckLaunchAsync(_game, "selected.vpe", "Selected", default); Run(host, task);
			Assert.That(task.Result, Is.Empty); Assert.That(factory.Thread, Is.EqualTo(Thread.CurrentThread.ManagedThreadId));
			Assert.That(factory.Context!.Settings.String("path"), Is.EqualTo("per-game"));
			Assert.That(factory.Context.Game, Is.EqualTo(_game)); Assert.That(factory.Context.Platform, Is.EqualTo("windows"));
			Assert.That(factory.Context.Package.Path, Is.EqualTo("selected.vpe"));
		}

		[Test]
		public void UnsupportedRequiredPluginBlocksOnlyItsGames()
		{
			var factory = new CheckFactory("platform"); factory.Descriptor.Platforms = new[] { "linux" }; var host = Host(factory);
			var check = host.CheckLaunchAsync(_game, "test", "Test", default); Run(host, check);
			Assert.That(check.Result, Has.Count.EqualTo(1)); Assert.That(factory.Checks, Is.Zero);
			factory.Applies = false;
			check = host.CheckLaunchAsync(_game, "test", "Test", default); Run(host, check); Assert.That(check.Result, Is.Empty);
		}

		[Test]
		public void CheckExceptionsFailClosedAndCanBeRetriedAfterCorrection()
		{
			var factory = new CheckFactory("throws") { Hook = (_, __) => throw new IOException("Cannot read ROM") }; var host = Host(factory);
			var task = host.CheckLaunchAsync(_game, "test", "Test", default); Run(host, task);
			Assert.That(task.Result.Single().Issues.Single().Message, Is.EqualTo("Cannot read ROM"));
			factory.Hook = null;
			task = host.CheckLaunchAsync(_game, "test", "Test", default); Run(host, task); Assert.That(task.Result, Is.Empty);
		}

		[Test]
		public void ACheckThatIgnoresCancellationIsNotInvokedAgainUntilItFinishes()
		{
			var completion = new TaskCompletionSource<IReadOnlyList<PluginValidationIssue>>();
			var factory = new CheckFactory("hung") { Hook = (_, __) => completion.Task }; var host = Host(factory); host.StartupTimeout = TimeSpan.FromMilliseconds(25);
			var task = host.CheckLaunchAsync(_game, "test", "Test", default); Run(host, task); Assert.That(task.Result, Has.Count.EqualTo(1));
			task = host.CheckLaunchAsync(_game, "test", "Test", default); Run(host, task);
			Assert.That(task.Result, Has.Count.EqualTo(1)); Assert.That(factory.Checks, Is.EqualTo(1));
			completion.SetResult(Array.Empty<PluginValidationIssue>()); factory.Hook = null;
			task = host.CheckLaunchAsync(_game, "test", "Test", default); Run(host, task); Assert.That(task.Result, Is.Empty);
		}

		[Test]
		public void ACheckThatHonorsTheDeadlineReportsAnActionableTimeout()
		{
			var factory = new CheckFactory("deadline") { Hook = async (_, token) => { await Task.Delay(Timeout.Infinite, token); return Array.Empty<PluginValidationIssue>(); } };
			var host = Host(factory); host.StartupTimeout = TimeSpan.FromMilliseconds(25);
			var check = host.CheckLaunchAsync(_game, "test", "Test", default); Run(host, check);
			Assert.That(check.Result.Single().Issues.Single().Message, Does.Contain("did not finish in time"));
		}

		[TestCase(false)]
		[TestCase(true)]
		public void BusyHostOnlyBlocksGamesWithApplicableChecks(bool applies)
		{
			var completion = new TaskCompletionSource<bool>();
			var slow = new TestFactory("slow") { InitializeCompletion = completion.Task };
			var required = new CheckFactory("required") { Applies = applies }; var host = Host(slow, required);
			host.StartupTimeout = TimeSpan.FromSeconds(5);
			var starting = host.PrepareAndStartAsync(_game, "test", "Test", "controller", 1, default);
			host.StartupTimeout = TimeSpan.FromMilliseconds(25);
			try {
				var check = host.CheckLaunchAsync(_game, "next", "Next", default); Run(host, check);
				Assert.That(check.Result.Count, Is.EqualTo(applies ? 1 : 0));
				if (applies) Assert.That(check.Result[0].Issues[0].Message, Does.Contain("previous game is stopping"));
			} finally { completion.TrySetResult(true); Run(host, starting); Run(host, host.StopGameAsync()); }
		}

		[Test]
		public void CallerCancellationStopsTheCheckAndReleasesTheLifecycleGate()
		{
			using var cancellation = new CancellationTokenSource();
			var factory = new CheckFactory("cancel") { Hook = async (_, token) => { cancellation.Cancel(); await Task.Delay(Timeout.Infinite, token); return Array.Empty<PluginValidationIssue>(); } };
			var host = Host(factory); Assert.Catch<OperationCanceledException>(() => Run(host, host.CheckLaunchAsync(_game, "test", "Test", cancellation.Token)));
			Assert.That(host.IsTransitioning, Is.False); factory.Hook = null;
			var task = host.CheckLaunchAsync(_game, "test", "Test", default); Run(host, task); Assert.That(task.Result, Is.Empty);
		}

		[Test]
		public void RequiredPluginFailureDuringPreparationStopsEveryPlugin()
		{
			var factory = new CheckFactory("required"); factory.Inner.FailAt = "prepare";
			var optional = new TestFactory("optional"); var host = Host(factory, optional);
			Assert.Throws<PluginLaunchBlockedException>(() => Run(host, host.PrepareAndStartAsync(_game, "test", "Test", "controller", 1, default)));
			Assert.That(host.IsRunning, Is.False); Assert.That(optional.Calls, Does.Contain("optional:dispose"));
			Assert.That(factory.Inner.Calls, Does.Contain("required:dispose"));
		}

		[Test]
		public void LateRequirementFailureUnwindsBeforeDisposingApplicationResources()
		{
			var factory = new CheckFactory("required") { BlockDuringInitialize = true }; var host = Host(factory);
			var blocked = Assert.Throws<PluginLaunchBlockedException>(() => Run(host, host.PrepareAndStartAsync(_game, "test", "Test", "controller", 1, default)));
			Assert.That(blocked!.Blockers.Single().Issues.Single().Field, Is.EqualTo("rom"));
			Assert.That(blocked.Blockers.Single().Game, Is.EqualTo(_game));
			Assert.That(factory.Inner.Calls, Is.EqualTo(new[] { "shutdown", "release", "dispose" }));
			Assert.That(host.IsRunning, Is.False); Assert.That(host.IsTransitioning, Is.False);
		}

		[Test]
		public void AsyncCheckReturnsToOwnerBeforeInvokingTheNextFactory()
		{
			var first = new CheckFactory("async") { Hook = (_, __) => Task.Run<IReadOnlyList<PluginValidationIssue>>(() => Array.Empty<PluginValidationIssue>()) };
			var second = new CheckFactory("owner"); var host = Host(first, second);
			var check = host.CheckLaunchAsync(_game, "test", "Test", default); Run(host, check);
			Assert.That(check.Result, Is.Empty); Assert.That(second.Thread, Is.EqualTo(Thread.CurrentThread.ManagedThreadId));
		}

		[Test]
		public void PreflightDoesNotStopAnAlreadyRunningGame()
		{
			var factory = new CheckFactory("required"); var host = Host(factory);
			Run(host, host.PrepareAndStartAsync(_game, "test", "Test", "controller", 1, default));
			factory.Issues = new[] { new PluginValidationIssue("rom", "Missing for next game") };
			var task = host.CheckLaunchAsync(_game, "other", "Other", default); Run(host, task);
			Assert.That(task.Result, Has.Count.EqualTo(1)); Assert.That(host.IsRunning, Is.True);
			Assert.That(host.Statuses[factory.Descriptor.Id].State, Is.EqualTo(PluginState.Running)); Run(host, host.StopGameAsync());
		}

		private sealed class CheckFactory : IPlayerPluginFactory, IPluginLaunchCheck
		{
			internal readonly TestFactory Inner;
			internal bool Applies = true;
			internal bool BlockDuringInitialize;
			internal int Checks, Thread;
			internal PluginLaunchContext? Context;
			internal IReadOnlyList<PluginValidationIssue> Issues = Array.Empty<PluginValidationIssue>();
			internal Func<PluginLaunchContext, CancellationToken, Task<IReadOnlyList<PluginValidationIssue>>>? Hook;
			internal CheckFactory(string id) => Inner = new TestFactory(id);
			public PluginDescriptor Descriptor => Inner.Descriptor;
			public PluginSettingsSchema Schema => Inner.Schema;
			public IPluginSettingsProvider? SettingsProvider => null;
			public IPlayerPlugin Create(PluginApplicationContext context)
			{
				if (!BlockDuringInitialize) return Inner.Create(context);
				context.Lifetime.Own(new Subscription(() => Inner.Calls.Add("release")));
				return new LateBlockingPlugin(this);
			}
			public bool AppliesTo(GameIdentity game) => Applies;
			public Task<IReadOnlyList<PluginValidationIssue>> CheckLaunchAsync(PluginLaunchContext context, CancellationToken cancellation)
			{ Checks++; Thread = System.Threading.Thread.CurrentThread.ManagedThreadId; Context = context; return Hook?.Invoke(context, cancellation) ?? Task.FromResult(Issues); }
			private sealed class LateBlockingPlugin : PlayerPlugin
			{
				private readonly CheckFactory _factory;
				internal LateBlockingPlugin(CheckFactory factory) => _factory = factory;
				public override Task InitializeAsync(CancellationToken cancellation) => throw new PluginLaunchBlockedException(_factory.Descriptor, "ROM disappeared", "rom");
				public override Task ShutdownAsync(CancellationToken cancellation) { _factory.Inner.Calls.Add("shutdown"); return Task.CompletedTask; }
				public override void Dispose() => _factory.Inner.Calls.Add("dispose");
			}
		}
	}
}
