// Visual Pinball Engine. Copyright (C) 2026 freezy and VPE Team.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using NUnit.Framework;
using UnityEngine;
using VisualPinball.Plugins;

namespace VisualPinball.Unity.Test
{
	public class PcmAudioTests
	{
		private static NativePcmSession Session()
		{
			try { return new NativePcmSession(); }
			catch (DllNotFoundException) { Assert.Ignore("Build the desktop VpePcm native library to run interop tests."); return null; }
		}
		[Test]
		public void AutomaticRoutesRespectComponentRolesAndPreserveUnknownIds()
		{
			var effect = ScriptableObject.CreateInstance<SoundEffectAsset>();
			var music = ScriptableObject.CreateInstance<MusicAsset>();
			var callout = ScriptableObject.CreateInstance<CalloutAsset>();
			try {
				Assert.That(SoundBuses.Resolve(music), Is.EqualTo(SoundBuses.Music));
				Assert.That(SoundBuses.Resolve(callout), Is.EqualTo(SoundBuses.Callouts));
				Assert.That(SoundBuses.Resolve(effect, SoundBuses.Rolling), Is.EqualTo(SoundBuses.Rolling));
				effect.BusId = "newer.route";
				Assert.That(SoundBuses.Resolve(effect, SoundBuses.Rolling), Is.EqualTo(SoundBuses.Rolling));
				Assert.That(effect.BusId, Is.EqualTo("newer.route"));
				effect.BusId = SoundBuses.Synthetic;
				Assert.That(SoundBuses.Resolve(effect, SoundBuses.Rolling), Is.EqualTo(SoundBuses.Synthetic));
			} finally { UnityEngine.Object.DestroyImmediate(effect); UnityEngine.Object.DestroyImmediate(music); UnityEngine.Object.DestroyImmediate(callout); }
		}
		[Test]
		public void RomTakeoverCopiesRawDataAppliesGainOnceAndReleasesPlayback()
		{
			using var session = Session();
			using var source = new GainControlledAudioResourceSource { PlaybackGain = .5f };
			var bytes = new byte[64];
			for (int i = 1; i < bytes.Length; i += 2) bytes[i] = 64;
			using (var input = new PcmRomInput(session, source)) {
				Assert.That(source.PublishForPlayback(new PluginPcmFrame(bytes, PcmEncoding.Int16LittleEndian, 48000, 1, 32, 1)), Is.True);
				Array.Clear(bytes, 0, bytes.Length);
				var output = new float[16]; session.Render(output, 2, 48000, false);
				foreach (var value in output) Assert.That(value, Is.EqualTo(.25f).Within(.00001f));
				source.PlaybackGain = 0;
				source.Publish(new PluginPcmFrame(bytes, PcmEncoding.Int16LittleEndian, 48000, 1, 32, 2));
				session.Render(output, 2, 48000, false);
				foreach (var value in output) Assert.That(value, Is.Zero, "mute also silences samples queued before the gain change");
			}
			Assert.That(source.PlaybackReplaced, Is.False);
		}
		[Test]
		public void RomOwnershipConflictAndFormatChangeFailWithoutDuplicatePlayback()
		{
			using var session = Session();
			using var source = new GainControlledAudioResourceSource();
			using (var legacy = new AudioResourceSource()) Assert.Throws<InvalidOperationException>(() => new PcmRomInput(session, legacy), "unknown gain must preserve legacy playback, including saved mute");
			using (source.AcquirePlayback("external")) {
				Assert.Throws<InvalidOperationException>(() => new PcmRomInput(session, source));
				Assert.That(source.HasSubscribers, Is.False, "failed takeover must release its subscription");
			}
			using (var input = new PcmRomInput(session, source)) {
				source.Publish(new PluginPcmFrame(new byte[32], PcmEncoding.Int16LittleEndian, 48000, 1, 16, 1));
				source.Publish(new PluginPcmFrame(new byte[64], PcmEncoding.Int16LittleEndian, 48000, 2, 16, 2));
				Assert.That(input.Fault, Does.Contain("format"));
			}
			Assert.That(source.PlaybackReplaced, Is.False);
		}
		[Test]
		public void LegacyMetadataKeepsAutomaticRouting()
		{
			var data = PackageApi.Packer.Unpack<SoundAssetMetaPackable>(PackageApi.Packer.Pack(new { ClipRefs = Array.Empty<string>(), InstanceId = 7 }));
			Assert.That(data.RoutingVersion, Is.Zero);
			Assert.That(string.IsNullOrEmpty(data.BusId), Is.True);
		}
	}
}
