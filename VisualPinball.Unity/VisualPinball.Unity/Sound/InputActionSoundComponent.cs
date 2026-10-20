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
	[PackAs("InputActionSound")]
	[AddComponentMenu("Pinball/Sound/Input Action Sound")]
	public class InputActionSoundComponent : SoundComponent, IPackable
	{
		public string ActionName;
		private Player _player;
		private bool _pressed;
		public override bool SupportsLoopingSoundAssets() => false;
		protected override void OnEnableAfterAfterAwake() {
			base.OnEnableAfterAfterAwake();
			_pressed = false;
			_player = GetComponentInParent<Player>();
			if (_player != null)
				_player.CabinetInputActionChanged += OnInput;
		}
		private void OnInput(string action, bool pressed) {
			if (InputManager.GetCanonicalActionName(ActionName) != action || _pressed == pressed)
				return;
			_pressed = pressed;
			if (pressed)
				StartSound();
		}
		protected override void OnDisable() {
			if (_player != null)
				_player.CabinetInputActionChanged -= OnInput;
			_player = null;
			_pressed = false;
			base.OnDisable();
		}
		public new byte[] Pack() => PackageApi.Packer.Pack(new InputActionSoundPackable
		{
			Sound = base.Pack(),
			ActionName = ActionName
		});
		public new void Unpack(byte[] bytes) {
			var p = PackageApi.Packer.Unpack<InputActionSoundPackable>(bytes);
			base.Unpack(p.Sound);
			ActionName = p.ActionName;
		}
	}
	public struct InputActionSoundPackable
	{
		public byte[] Sound;
		public string ActionName;
	}
}
