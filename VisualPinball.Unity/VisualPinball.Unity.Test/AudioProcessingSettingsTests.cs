// Visual Pinball Engine. Copyright (C) 2026 freezy and VPE Team.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using NUnit.Framework;
using UnityEngine;

namespace VisualPinball.Unity.Test
{
	public class AudioProcessingSettingsTests
	{
		[TestCase("reference")]
		[TestCase("unrecognized")]
		public void ReferenceAndUnknownPresetsRemainDry(string preset)
		{
			var settings = AudioProcessingSettings.Preset(preset);
			Assert.That(settings.enabled || settings.reverb || settings.limiter, Is.False);
			Assert.That(settings.Route(2).send, Is.Zero);
		}
		[TestCase("desktop-room")]
		[TestCase("headphones")]
		public void RoomPresetsDoNotReverberateMixedRomOrProgramAudio(string preset)
		{
			var settings = AudioProcessingSettings.Preset(preset);
			Assert.That(settings.enabled && settings.reverb && settings.limiter, Is.True);
			Assert.That(settings.Route(0).send, Is.GreaterThan(0));
			for (int i=2; i<7; ++i) Assert.That(settings.Route(i).send, Is.Zero);
		}
		[Test]
		public void CopyAndJsonRoundtripDoNotAliasThePreviewOrPersistBypass()
		{
			var original = AudioProcessingSettings.Preset("night"); original.bypass = true;
			var copy = original.Copy(); copy.Route(0).bands[4].db = 6; copy.master = .5f;
			Assert.That(original.Route(0).bands[4].db, Is.Zero); Assert.That(original.master, Is.EqualTo(1));
			var restored = JsonUtility.FromJson<AudioProcessingSettings>(JsonUtility.ToJson(copy)); restored.Normalize();
			Assert.That(restored.bypass, Is.False); Assert.That(restored.Route(0).bands[4].db, Is.EqualTo(6));
		}
		[Test]
		public void NormalizationBoundsMalformedConfigurationAndBuildsEightRoutes()
		{
			var settings = new AudioProcessingSettings { master = float.NaN, decaySeconds = float.PositiveInfinity, dampingHz = -100 };
			settings.Route(0).bands = null; settings.Route(0).ratio = 100000; settings.Normalize();
			Assert.That(settings.routes.Count, Is.EqualTo(8)); Assert.That(settings.Route(0).bands.Count, Is.EqualTo(8));
			Assert.That(settings.master, Is.EqualTo(1)); Assert.That(settings.Route(0).ratio, Is.EqualTo(20));
			foreach (float value in settings.ToNative()) Assert.That(float.IsNaN(value) || float.IsInfinity(value), Is.False);
		}
		[Test]
		public void CopyRepairsMissingCollectionsWithoutMutatingTheSource()
		{
			var original = new AudioProcessingSettings { routes = null };
			var copy = original.Copy();
			Assert.That(copy.ToNative().Length, Is.EqualTo(342)); Assert.That(original.routes, Is.Null);
		}
		[Test]
		public void NativeWireLayoutKeepsBusEqCompressorAndRoomSeparate()
		{
			var settings = AudioProcessingSettings.Preset("night"); var route = settings.Route(1);
			route.eq = route.bands[4].enabled = true; route.bands[4].hz = 3456; route.bands[4].db = -5; route.ratio = 3; route.send = .23f;
			var values = settings.ToNative(); int offset = AudioProcessingSettings.HeaderSize + AudioProcessingSettings.RouteSize;
			Assert.That(values.Length, Is.EqualTo(342)); Assert.That(values[offset+18], Is.EqualTo(3456));
			Assert.That(values[offset+19], Is.EqualTo(-5)); Assert.That(values[offset+35], Is.EqualTo(3)); Assert.That(values[offset+40], Is.EqualTo(.23f));
			Assert.That(values[14+7*41+33], Is.EqualTo(1)); Assert.That(values[11], Is.EqualTo(1)); Assert.That(values[4], Is.Zero);
		}
	}
}
