// Visual Pinball Engine. Copyright (C) 2026 freezy and VPE Team.
// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;

namespace VisualPinball.Unity
{
	/// <summary>Portable, player-owned stereo processing data. Never part of a table component or packer.</summary>
	[Serializable]
	public sealed class AudioProcessingSettings
	{
		public const int WireVersion = 1, HeaderSize = 14, RouteSize = 41, RouteCount = 8, ValueCount = HeaderSize + RouteSize * RouteCount;
		public int version = WireVersion;
		public string preset = "reference";
		public bool enabled, mute;
		[NonSerialized] public bool bypass;
		public float master = 1;
		public bool reverb;
		public float reverbAmount = .1f, roomMs = 30, decaySeconds = .45f, predelayMs = 8, dampingHz = 8000, width = 1;
		public bool limiter;
		public float ceilingDb = -1, limiterReleaseMs = 80;
		public List<AudioProcessingRoute> routes = new();
		public AudioProcessingRoute Route(int index)
		{
			routes ??= new();
			string id = index == 7 ? "output" : SoundBuses.Ids[index];
			var route = routes.Find(value => value != null && value.id == id);
			if (route == null) { route = new AudioProcessingRoute { id = id }; routes.Add(route); }
			return route;
		}
		public void Normalize()
		{
			version = WireVersion;
			if (preset != "reference" && preset != "desktop-room" && preset != "headphones" && preset != "night" && preset != "custom") preset = "reference";
			master = Clamp(master, 0, 1, 1); reverbAmount = Clamp(reverbAmount, 0, 1, .1f); roomMs = Clamp(roomMs, 10, 100, 30);
			decaySeconds = Clamp(decaySeconds, .1f, 3, .45f); predelayMs = Clamp(predelayMs, 0, 100, 8); dampingHz = Clamp(dampingHz, 1000, 20000, 8000);
			width = Clamp(width, 0, 1.5f, 1); ceilingDb = Clamp(ceilingDb, -12, 0, -1); limiterReleaseMs = Clamp(limiterReleaseMs, 10, 250, 80);
			routes ??= new();
			var normalized = new List<AudioProcessingRoute>(RouteCount);
			for (int i = 0; i < RouteCount; ++i) { var route = Route(i); route.Normalize(); normalized.Add(route); }
			routes = normalized;
		}
		public AudioProcessingSettings Copy()
		{
			var copy = (AudioProcessingSettings)MemberwiseClone(); copy.routes = new();
			if (routes != null) foreach (var route in routes) if (route != null) copy.routes.Add(route.Copy());
			return copy;
		}
		public static AudioProcessingSettings Preset(string name)
		{
			var settings = new AudioProcessingSettings { preset = name }; settings.Normalize();
			if (settings.preset == "reference") return settings;
			settings.enabled = settings.limiter = true;
			if (name == "desktop-room" || name == "headphones") {
				settings.reverb = true; settings.reverbAmount = name == "headphones" ? .1f : .08f;
				settings.Route(0).send = .12f; settings.Route(1).send = .06f;
			}
			if (name == "night") { var output = settings.Route(7); output.compressor = true; output.thresholdDb = -24; output.ratio = 2; }
			return settings;
		}
		internal float[] ToNative()
		{
			Normalize(); var data = new float[ValueCount];
			data[0] = enabled ? 1 : 0; data[1] = bypass ? 1 : 0; data[2] = master; data[3] = mute ? 1 : 0;
			data[4] = reverb ? 1 : 0; data[5] = reverbAmount; data[6] = roomMs; data[7] = decaySeconds; data[8] = predelayMs;
			data[9] = dampingHz; data[10] = width; data[11] = limiter ? 1 : 0; data[12] = ceilingDb; data[13] = limiterReleaseMs;
			for (int i = 0; i < RouteCount; ++i) {
				var route = Route(i); int start = HeaderSize + i * RouteSize; data[start] = route.eq ? 1 : 0;
				for (int b = 0; b < 8; ++b) {
					var band = route.bands[b]; int offset = start + 1 + b * 4;
					data[offset] = band.enabled ? 1 : 0; data[offset+1] = band.hz; data[offset+2] = band.db; data[offset+3] = band.q;
				}
				data[start+33] = route.compressor ? 1 : 0; data[start+34] = route.thresholdDb; data[start+35] = route.ratio; data[start+36] = route.kneeDb;
				data[start+37] = route.attackMs; data[start+38] = route.releaseMs; data[start+39] = route.makeupDb; data[start+40] = route.send;
			}
			return data;
		}
		internal static float Clamp(float value, float min, float max, float fallback) => float.IsNaN(value) || float.IsInfinity(value) ? fallback : System.Math.Max(min, System.Math.Min(max, value));
	}
	[Serializable]
	public sealed class AudioProcessingRoute
	{
		public string id;
		public bool eq, compressor;
		public List<AudioEqBand> bands = new();
		public float thresholdDb = -18, ratio = 2, kneeDb = 6, attackMs = 10, releaseMs = 120, makeupDb, send;
		public void Normalize()
		{
			float[] defaults = { 30, 18000, 120, 8000, 1000, 250, 2500, 5000 };
			bands ??= new();
			while (bands.Count < 8) bands.Add(new AudioEqBand { hz = defaults[bands.Count] });
			if (bands.Count > 8) bands.RemoveRange(8, bands.Count-8);
			for (int i = 0; i < 8; ++i) { bands[i] ??= new AudioEqBand { hz = defaults[i] }; bands[i].Normalize(defaults[i]); }
			thresholdDb = AudioProcessingSettings.Clamp(thresholdDb, -60, 0, -18); ratio = AudioProcessingSettings.Clamp(ratio, 1, 20, 2);
			kneeDb = AudioProcessingSettings.Clamp(kneeDb, 0, 24, 6); attackMs = AudioProcessingSettings.Clamp(attackMs, .1f, 100, 10);
			releaseMs = AudioProcessingSettings.Clamp(releaseMs, 10, 1000, 120); makeupDb = AudioProcessingSettings.Clamp(makeupDb, 0, 12, 0); send = AudioProcessingSettings.Clamp(send, 0, 1, 0);
		}
		internal AudioProcessingRoute Copy() { var copy = (AudioProcessingRoute)MemberwiseClone(); copy.bands = new(); if (bands != null) foreach (var band in bands) copy.bands.Add(band == null ? null : new AudioEqBand { enabled = band.enabled, hz = band.hz, db = band.db, q = band.q }); return copy; }
	}
	[Serializable]
	public sealed class AudioEqBand
	{
		public bool enabled;
		public float hz = 1000, db, q = .7071068f;
		internal void Normalize(float fallback) { hz = AudioProcessingSettings.Clamp(hz, 20, 20000, fallback); db = AudioProcessingSettings.Clamp(db, -12, 12, 0); q = AudioProcessingSettings.Clamp(q, .1f, 10, .7071068f); }
	}
}
