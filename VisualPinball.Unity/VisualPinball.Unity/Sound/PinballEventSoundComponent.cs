// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Linq;
using UnityEngine;

namespace VisualPinball.Unity
{
	public enum SoundEventComparison
	{
		GreaterThan, LessThan, Equal
	}
	[PackAs("PinballEventSound")]
	[AddComponentMenu("Pinball/Sound/Pinball Event Sound")]
	public class PinballEventSoundComponent : EventSoundComponent<IPinballEventEmitter, PinballEventArgs>, IPackable
	{
		public string EventName;
		public SoundEventComparison Comparison;
		public float Threshold;
		private bool _active;
		public override Type GetRequiredType() => typeof(IPinballEventEmitter);
		public override bool SupportsLoopingSoundAssets() => true;
		protected override bool TryFindEventSource(out IPinballEventEmitter source) {
			_active = false;
			var sources = GetComponents<IPinballEventEmitter>().Where(s => s.Events.Any(e => e.Name == EventName)).ToArray();
			source = sources.Length == 1 ? sources[0] : null;
			if (sources.Length == 0)
				Logger.Warn("Sound {Name}: no emitter advertises event {Event}.", name, EventName);
			else if (sources.Length > 1)
				Logger.Warn("Sound {Name}: event {Event} is advertised by {Count} emitters ({Types}); cannot bind uniquely.",
					name, EventName, sources.Length, string.Join(", ", sources.Select(s => s.GetType().Name)));
			return source != null;
		}
		protected override void Subscribe(IPinballEventEmitter s) => s.OnPinballEvent += OnEvent;
		protected override void Unsubscribe(IPinballEventEmitter s) => s.OnPinballEvent -= OnEvent;
		protected override void OnEvent(object sender, PinballEventArgs e) {
			if (e.Name != EventName)
				return;
			var active = Comparison == SoundEventComparison.GreaterThan ? e.Value > Threshold
				: Comparison == SoundEventComparison.LessThan ? e.Value < Threshold : e.Value == Threshold;
			if (_active == active)
				return;
			_active = active;
			if (active)
				StartSound();
			else
				StopAllSounds(false);
		}
		protected override void OnDisable() {
			_active = false;
			StopAllSounds(false);
			base.OnDisable();
		}
		public new byte[] Pack() => PackageApi.Packer.Pack(new PinballEventSoundPackable
		{
			Sound = base.Pack(),
			EventName = EventName,
			Comparison = Comparison,
			Threshold = Threshold
		});
		public new void Unpack(byte[] bytes) {
			var p = PackageApi.Packer.Unpack<PinballEventSoundPackable>(bytes);
			base.Unpack(p.Sound);
			EventName = p.EventName;
			Comparison = p.Comparison;
			Threshold = p.Threshold;
		}
	}
	public struct PinballEventSoundPackable
	{
		public byte[] Sound;
		public string EventName;
		public SoundEventComparison Comparison;
		public float Threshold;
	}
}
