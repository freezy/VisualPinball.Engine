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

using UnityEngine;
using System;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Sound/Coil Sound")]
	public class CoilSoundComponent : EventSoundComponent<IApiCoil, NoIdCoilEventArgs>
	{
		public enum StartWhen { CoilEnergized, CoilDeenergized };
		public enum StopWhen { Never, CoilEnergized, CoilDeenergized };

		[SerializeField] private StartWhen _startWhen = StartWhen.CoilEnergized;
		[SerializeField] private StopWhen _stopWhen = StopWhen.Never;
		[SerializeField, HideInInspector] private string _coilName;


		public override bool SupportsLoopingSoundAssets() => _stopWhen != StopWhen.Never;

		public override Type GetRequiredType() => typeof(ICoilDeviceComponent);

		protected override bool TryFindEventSource(out IApiCoil coil)
		{
			coil = null;
			var player = GetComponentInParent<Player>();
			if (player == null)
				return false;
			foreach (var component in GetComponents<ICoilDeviceComponent>()) {
				coil = player.Coil(component, _coilName);
				if (coil != null)
					return true;
			}
			return false;
		}

		protected async override void OnEvent(object sender, NoIdCoilEventArgs e)
		{
			if ((e.IsEnergized && _stopWhen == StopWhen.CoilEnergized) ||
				(!e.IsEnergized && _stopWhen == StopWhen.CoilDeenergized))
				Stop(allowFade: true);

			if ((e.IsEnergized && _startWhen == StartWhen.CoilEnergized) ||
				(!e.IsEnergized && _startWhen == StartWhen.CoilDeenergized))
				await Play();
		}

		protected override void Subscribe(IApiCoil eventSource)
		{
			eventSource.CoilStatusChanged += OnEvent;
		}

		protected override void Unsubscribe(IApiCoil eventSource)
		{
			eventSource.CoilStatusChanged -= OnEvent;
		}
	}
}
