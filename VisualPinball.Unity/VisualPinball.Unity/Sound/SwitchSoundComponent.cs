// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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
	[AddComponentMenu("Visual Pinball/Sound/Switch Sound")]
	public class SwitchSoundComponent : EventSoundComponent<IApiSwitch, SwitchEventArgs>
	{
		public enum StartWhen { SwitchEnabled, SwitchDisabled };
		public enum StopWhen { Never, SwitchEnabled, SwitchDisabled };

		[SerializeField] private StartWhen _startWhen = StartWhen.SwitchEnabled;
		[SerializeField] private StopWhen _stopWhen = StopWhen.Never;
		[SerializeField, HideInInspector] private string _switchName;

		public override bool SupportsLoopingSoundAssets() => _stopWhen != StopWhen.Never;

		public override Type GetRequiredType() => typeof(ISwitchDeviceComponent);

		protected override bool TryFindEventSource(out IApiSwitch source)
		{
			source = null;
			var player = GetComponentInParent<Player>();
			if (player == null)
				return false;
			foreach (var component in GetComponents<ISwitchDeviceComponent>()) {
				source = player.Switch(component, _switchName);
				if (source != null)
					return true;
			}
			return false;
		}

		protected override async void OnEvent(object sender, SwitchEventArgs e)
		{
			if ((e.IsEnabled && _stopWhen == StopWhen.SwitchEnabled) ||
				(!e.IsEnabled && _stopWhen == StopWhen.SwitchDisabled))
				Stop(allowFade: true);

			if ((e.IsEnabled && _startWhen == StartWhen.SwitchEnabled) ||
				(!e.IsEnabled && _startWhen == StartWhen.SwitchDisabled))
				await Play();
		}

		protected override void Subscribe(IApiSwitch eventSource)
			=> eventSource.Switch += OnEvent;

		protected override void Unsubscribe(IApiSwitch eventSource)
			=> eventSource.Switch -= OnEvent;
	}
}
