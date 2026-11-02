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
			var assignments = new[] { new CabinetOutputAssignment("b2s", SurfaceRoles.Backglass, CabinetOutputMode.Player) };
			var targets = new[] { new CabinetDisplayTarget("screen", SurfaceRoles.Backglass, CabinetEndpointKind.DesktopRegion, CabinetEndpointState.Unavailable, reason: "Disconnected") };
			var snapshot = new CabinetDisplaySnapshot(4, 7, 9, targets, assignments);
			assignments[0] = new CabinetOutputAssignment("b2s", SurfaceRoles.Backglass, CabinetOutputMode.Off);
			targets[0] = new CabinetDisplayTarget("screen", SurfaceRoles.Backglass, CabinetEndpointKind.DesktopRegion, CabinetEndpointState.Available, -1920, 0, 1920, 1080);
			Assert.That(snapshot.ModeFor("b2s", SurfaceRoles.Backglass), Is.EqualTo(CabinetOutputMode.Player));
			Assert.That(snapshot.ModeFor("pup", SurfaceRoles.Backglass), Is.EqualTo(CabinetOutputMode.Custom));
			Assert.That(snapshot.Targets[0].State, Is.EqualTo(CabinetEndpointState.Unavailable));
		}

		[Test]
		public void DuplicateAssignmentsAndInvalidAvailableDestinationsAreRejected()
		{
			var adoption = new CabinetOutputAssignment("pup", SurfaceRoles.Topper, CabinetOutputMode.Player);
			Assert.Throws<ArgumentException>(() => new CabinetDisplaySnapshot(1, 1, 1, Array.Empty<CabinetDisplayTarget>(), new[] { adoption, adoption }));
			Assert.Throws<ArgumentOutOfRangeException>(() => new CabinetDisplayTarget("screen", "backglass", CabinetEndpointKind.DesktopRegion, CabinetEndpointState.Available));
			Assert.Throws<ArgumentException>(() => new CabinetDisplayTarget("device", "dmd", CabinetEndpointKind.Device, CabinetEndpointState.Available));
			var target = new CabinetDisplayTarget("screen", "dmd", CabinetEndpointKind.DesktopRegion, CabinetEndpointState.Available, width: 128, height: 32);
			Assert.Throws<ArgumentException>(() => new CabinetDisplaySnapshot(1, 1, 1, new[] { target, target }, Array.Empty<CabinetOutputAssignment>()));
			var device = new CabinetDisplayTarget("device", "dmd", CabinetEndpointKind.Device, CabinetEndpointState.Available, deviceLeaseKey: "serial:COM3");
			Assert.That(new CabinetDisplaySnapshot(1, 1, 1, new[] { target, device }, Array.Empty<CabinetOutputAssignment>()).TargetsFor("dmd").Count, Is.EqualTo(2));
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
		public void MonitorAndDeviceAdoptionAreIndependentAndLegacyOverloadMeansMonitor()
		{
			var snapshot = new CabinetDisplaySnapshot(1, 1, 1, Array.Empty<CabinetDisplayTarget>(), new[] {
				new CabinetOutputAssignment("dmd", SurfaceRoles.Dmd, CabinetOutputMode.Player),
				new CabinetOutputAssignment("dmd", SurfaceRoles.Dmd, CabinetOutputMode.Off, CabinetEndpointKind.Device) });
			Assert.That(snapshot.ModeFor("dmd", SurfaceRoles.Dmd), Is.EqualTo(CabinetOutputMode.Player));
			Assert.That(snapshot.ModeFor("dmd", SurfaceRoles.Dmd, CabinetEndpointKind.Device), Is.EqualTo(CabinetOutputMode.Off));
			Assert.That(snapshot.ModeFor("other", SurfaceRoles.Dmd, CabinetEndpointKind.Device), Is.EqualTo(CabinetOutputMode.Custom));
		}

		[Test]
		public void DeviceProfileRequestIsDetachedAndTargetCannotContradictItsProfile()
		{
			var settings = new PluginSettingsSnapshot(1, Newtonsoft.Json.Linq.JObject.Parse("{\"port\":\"COM3\"}"));
			var keys = new[] { "port" }; var request = new CabinetDeviceProfileRequest(settings, keys, "windows");
			settings.Values["port"] = "COM4"; keys[0] = "other";
			Assert.That(request.Settings.String("port"), Is.EqualTo("COM3")); Assert.That(request.SavedKeys, Is.EqualTo(new[] { "port" }));
			var profile = new CabinetDeviceProfile("profile", "dmd", SurfaceRoles.Dmd, "DMD", "serial:COM3", "usb:family", "{}");
			var target = CabinetDisplayTarget.Device("device", SurfaceRoles.Dmd, CabinetEndpointState.Available, profile);
			Assert.That(target.DeviceProfile, Is.SameAs(profile)); Assert.That(target.DeviceLeaseKey, Is.EqualTo(profile.LeaseKey));
			Assert.Throws<ArgumentException>(() => CabinetDisplayTarget.Device("device", SurfaceRoles.Topper, CabinetEndpointState.Available, profile));
			Assert.Throws<ArgumentException>(() => CabinetDisplayTarget.Device("device", SurfaceRoles.Dmd, CabinetEndpointState.Available, null));
			Assert.Throws<ArgumentException>(() => new CabinetDeviceProfile("p", "dmd", SurfaceRoles.Dmd, "DMD", "", "", "{}"));
		}

		[Test]
		public void ImportPreviewCopiesSettingsAndKeepsUnusableGeometryUnsupported()
		{
			var settings = new PluginSettingsSnapshot(1); settings.Values["x"] = 10; var keys = new[] { "x" };
			var request = new CabinetDisplayImportRequest(settings, keys, "windows"); settings.Values["x"] = 20; keys[0] = "y";
			Assert.That(request.Settings.Integer("x"), Is.EqualTo(10)); Assert.That(request.SavedKeys, Is.EqualTo(new[] { "x" }));
			Assert.That(new CabinetDisplayImportCandidate(SurfaceRoles.Dmd, 0, 0, 0, 32).Supported, Is.False);
			Assert.That(new CabinetDisplayImportCandidate(SurfaceRoles.Dmd, int.MaxValue, 0, 128, 32).Supported, Is.False);
			var hardware = new CabinetDisplayImportCandidate(SurfaceRoles.Dmd, "profile"); Assert.That(hardware.Kind, Is.EqualTo(CabinetEndpointKind.Device)); Assert.That(hardware.DeviceProfileId, Is.EqualTo("profile"));
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
