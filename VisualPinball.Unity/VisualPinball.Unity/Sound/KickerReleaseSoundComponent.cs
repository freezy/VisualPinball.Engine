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
using UnityEngine;
namespace VisualPinball.Unity
{
	[PackAs("KickerReleaseSound")]
	[AddComponentMenu("Pinball/Sound/Kicker Release Sound")]
	public class KickerReleaseSoundComponent : EventSoundComponent<KickerApi, KickerReleaseEventArgs>, IPackable
	{
		[Tooltip("Empty matches all successful kicks, including direct API kicks.")]
		public string CoilName;
		public override Type GetRequiredType() => typeof(KickerComponent);
		protected override bool TryFindEventSource(out KickerApi source) {
			source = GetComponentInParent<Player>()?.TableApi.Kicker(GetComponent<KickerComponent>());
			return source != null;
		}
		protected override void Subscribe(KickerApi s) => s.Kicked += OnEvent;
		protected override void Unsubscribe(KickerApi s) => s.Kicked -= OnEvent;
		protected override void OnEvent(object sender, KickerReleaseEventArgs e) {
			if (string.IsNullOrEmpty(CoilName) || CoilName == e.CoilId)
				StartSound();
		}
		public new byte[] Pack() => PackageApi.Packer.Pack(new KickerReleaseSoundPackable { Sound = base.Pack(), CoilName = CoilName });
		public new void Unpack(byte[] bytes) {
			var p = PackageApi.Packer.Unpack<KickerReleaseSoundPackable>(bytes);
			base.Unpack(p.Sound);
			CoilName = p.CoilName;
		}
	}
	public struct KickerReleaseSoundPackable
	{
		public byte[] Sound; public string CoilName;
	}
}
