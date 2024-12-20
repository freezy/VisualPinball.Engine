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
	public class CoilSoundComponent : SoundComponent
	{
		public enum StartWhen { CoilEnergized, CoilDeenergized };
		public enum StopWhen { Never, CoilEnergized, CoilDeenergized };

		[SerializeField] private StartWhen _startWhen = StartWhen.CoilEnergized;
		[SerializeField] private StopWhen _stopWhen = StopWhen.Never;
		[SerializeField, HideInInspector] private string _coilName;
		private IApiCoil _coil;

		protected override void OnEnableAfterAfterAwake()
		{
			base.OnEnableAfterAfterAwake();
			if (TryFindCoil(out var _coil))
				_coil.CoilStatusChanged += OnCoilStatusChanged;
			else
				Logger.Warn("Could not find coil. Make sure an appropriate " +
					"component is attached");
		}

		protected override void OnDisable()
		{
			base.OnDisable();
			if (_coil != null) {
				_coil.CoilStatusChanged -= OnCoilStatusChanged;
				_coil = null;
			}
		}

		private bool TryFindCoil(out IApiCoil coil)
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

		private async void OnCoilStatusChanged(object sender, NoIdCoilEventArgs e)
		{
			if ((e.IsEnergized && _stopWhen == StopWhen.CoilEnergized) ||
				(!e.IsEnergized && _stopWhen == StopWhen.CoilDeenergized))
				Stop(allowFade: true);

			if ((e.IsEnergized && _startWhen == StartWhen.CoilEnergized) ||
				(!e.IsEnergized && _startWhen == StartWhen.CoilDeenergized))
				await Play();
		}

		public override bool SupportsLoopingSoundAssets() => _stopWhen != StopWhen.Never;

		public override Type GetRequiredType() => typeof(ICoilDeviceComponent);
	}
}
