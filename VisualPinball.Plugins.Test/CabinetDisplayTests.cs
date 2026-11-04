// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using System;
using NUnit.Framework;

namespace VisualPinball.Plugins.Test
{
	public sealed class CabinetDisplayTests
	{
		[Test]
		public void SnapshotCopiesInputAndKeepsAdoptedMissingOutputsManaged()
		{
			var assignments = new[] { new CabinetOutputAssignment("b2s", CabinetOutputRole.Backglass, CabinetOutputMode.Player) };
			var targets = new[] { new CabinetDisplayTarget("screen", CabinetOutputRole.Backglass, CabinetEndpointState.Unavailable, reason: "Disconnected") };
			var snapshot = new CabinetDisplaySnapshot(4, 7, 9, targets, assignments);
			assignments[0] = new CabinetOutputAssignment("b2s", CabinetOutputRole.Backglass, CabinetOutputMode.Off);
			targets[0] = new CabinetDisplayTarget("screen", CabinetOutputRole.Backglass, CabinetEndpointState.Available, -1920, 0, 1920, 1080);
			Assert.That(snapshot.ModeFor("b2s", CabinetOutputRole.Backglass), Is.EqualTo(CabinetOutputMode.Player));
			Assert.That(snapshot.ModeFor("pup", CabinetOutputRole.Backglass), Is.EqualTo(CabinetOutputMode.Custom));
			Assert.That(snapshot.Targets[0].State, Is.EqualTo(CabinetEndpointState.Unavailable));
		}

		[Test]
		public void DuplicateAssignmentsAndInvalidAvailableDestinationsAreRejected()
		{
			var adoption = new CabinetOutputAssignment("pup", CabinetOutputRole.Topper, CabinetOutputMode.Player);
			Assert.Throws<ArgumentException>(() => new CabinetDisplaySnapshot(1, 1, 1, Array.Empty<CabinetDisplayTarget>(), new[] { adoption, adoption }));
			Assert.Throws<ArgumentOutOfRangeException>(() => new CabinetDisplayTarget("screen", CabinetOutputRole.Backglass, CabinetEndpointState.Available));
			var target = new CabinetDisplayTarget("screen", CabinetOutputRole.Dmd, CabinetEndpointState.Available, width: 128, height: 32);
			Assert.Throws<ArgumentException>(() => new CabinetDisplaySnapshot(1, 1, 1, new[] { target, target }, Array.Empty<CabinetOutputAssignment>()));
			var second = new CabinetDisplayTarget("second", CabinetOutputRole.Dmd, CabinetEndpointState.Disabled);
			Assert.That(new CabinetDisplaySnapshot(1, 1, 1, new[] { target, second }, Array.Empty<CabinetOutputAssignment>()).TargetsFor(CabinetOutputRole.Dmd).Count, Is.EqualTo(2));
		}

		[Test]
		public void RankingFallsBackOnlyWhenIneligibleAndIsIndependentOfPublicationOrder()
		{
			var b2s = Descriptor("b2s/1", "b2s");
			var pup = Descriptor("pup/1", "pup");
			var other = Descriptor("other/1", "other");
			var order = new[] { "pup", "b2s" };
			Assert.That(CabinetSurfaceSelector.Choose(new[] { other, b2s, pup }, order, _ => true), Is.SameAs(pup));
			Assert.That(CabinetSurfaceSelector.Choose(new[] { pup, other, b2s }, order, item => item != pup), Is.SameAs(b2s));
			Assert.That(CabinetSurfaceSelector.Choose(new[] { pup, other, b2s }, order, _ => false), Is.Null);
			Assert.That(CabinetSurfaceSelector.Choose(new[] { Descriptor("z", "unknown"), Descriptor("a", "unknown") }, order, _ => true)!.Id, Is.EqualTo("a"));
		}

		[Test]
		public void DesktopDemandDoesNotEnableAnAuthoredSceneScreen()
		{
			var source = new Surface { Consumers = CabinetSurfaceConsumers.ManagedDesktop, IsReady = true };
			Assert.That(CabinetSurfaceSelector.Eligible(source, CabinetSurfaceConsumers.Scene), Is.False);
			Assert.That(CabinetSurfaceSelector.Eligible(source, CabinetSurfaceConsumers.ManagedDesktop), Is.True);
			source.IsReady = false;
			Assert.That(CabinetSurfaceSelector.Eligible(source, CabinetSurfaceConsumers.ManagedDesktop), Is.False);
		}

		[Test]
		public void RegistryProbeRejectsStaleInstancesAndReadinessChangesRecomputeSelection()
		{
			var registry = new PluginResourceRegistry();
			var descriptor = Descriptor("surface", "pup");
			var source = new Surface { Consumers = CabinetSurfaceConsumers.ManagedDesktop, IsReady = true };
			using var publication = registry.Publish(descriptor, source);
			PluginResourceDescriptor? selected = null;
			using var watch = registry.Observe(_ => selected = CabinetSurfaceSelector.Choose(registry.Snapshot(), new[] { "pup" }, d => registry.TryGetSource<IPluginSurface>(d, out var current) && CabinetSurfaceSelector.Eligible(current!, CabinetSurfaceConsumers.Scene)));
			source.Consumers |= CabinetSurfaceConsumers.Scene;
			registry.NotifyChanged(descriptor.Id);
			Assert.That(selected, Is.SameAs(descriptor));
			source.IsReady = false;
			registry.NotifyChanged(descriptor.Id);
			Assert.That(selected, Is.Null);
			var stale = new PluginResourceDescriptor(descriptor.Id, "pup", "controller", "surface", "backglass", ResourceKind.Surface, 1, 2);
			Assert.That(registry.TryGetSource<IPluginSurface>(stale, out _), Is.False);
		}

		[Test]
		public void SharedRolesAreClosedAndKeepHdDisplaySeparateFromBackglass()
		{
			Assert.That(CabinetOutputRoles.TryParse("pup-screen-8", out _), Is.False);
			Assert.That(CabinetOutputRoles.TryParse("playfield", out _), Is.False, "Playfield placement belongs to Video.");
			Assert.That(CabinetOutputRoles.Parse("game-display"), Is.EqualTo(CabinetOutputRole.GameDisplay));
			Assert.That(CabinetOutputRoles.TryParse("full-dmd", out _), Is.False, "Full DMD is a screen PinUP places itself.");
			Assert.That(CabinetOutputRole.GameDisplay.Id(), Is.Not.EqualTo(CabinetOutputRole.Backglass.Id()));
			Assert.Throws<ArgumentException>(() => new CabinetOutputAssignment("plugin", (CabinetOutputRole)0, CabinetOutputMode.Player));
		}

		private static PluginResourceDescriptor Descriptor(string id, string provider) => new PluginResourceDescriptor(id, provider, "controller", id, "backglass", ResourceKind.Surface, 1, 1);
		private sealed class Surface : IPluginSurface, ICabinetSurface
		{
			public ResourceKind Kind => ResourceKind.Surface;
			public int Width => 1;
			public int Height => 1;
			public string Backend => "test";
			public CabinetSurfaceConsumers Consumers { get; set; }
			public bool IsReady { get; set; }
		}
	}
}
