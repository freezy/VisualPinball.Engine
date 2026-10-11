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
	/// <summary>
	/// The runtime of a rubber slingshot: one switch and one coil, as seen by the
	/// gamelogic engine, the player wiring and the scripting api.
	/// </summary>
	/// <remarks>
	/// The switch is the slingshot wall's: every slingshot event of the wall closes it
	/// as a pulse. The coil only animates; the wall has already kicked the ball.
	/// </remarks>
	[Api]
	public class RubberSlingshotApi : IApi, IApiSwitch, IApiSwitchDevice, IApiCoilDevice,
		IApiWireDeviceDest
	{
		/// <summary>Event emitted when the table is started.</summary>
		public event EventHandler Init;

		/// <summary>Event emitted when the slingshot wall fires the switch.</summary>
		public event EventHandler<SwitchEventArgs> Switch;

		/// <summary>Event emitted on every coil transition.</summary>
		public event EventHandler<NoIdCoilEventArgs> CoilStatusChanged;

		/// <summary>The actuator coil, driven by the gamelogic engine or a wire.</summary>
		public DeviceCoil ActuatorCoil { get; }

		/// <summary>Whether the coil is currently energized.</summary>
		public bool IsCoilEnergized { get; private set; }

		/// <summary>How many times the coil energized since the table started.</summary>
		public int CoilEnergizeCount { get; private set; }

		/// <summary>How many times the slingshot wall fired the switch since the table started.</summary>
		public int SwitchCount { get; private set; }

		public bool IsSwitchEnabled => _switchHandler.IsEnabled;

		private readonly RubberSlingshotComponent _component;
		private readonly Player _player;
		private readonly SwitchHandler _switchHandler;
		private SurfaceApi _wallApi;

		internal RubberSlingshotApi(GameObject go, Player player, PhysicsEngine physicsEngine,
			RubberSlingshotComponent component)
		{
			_component = component;
			_player = player;
			_switchHandler = new SwitchHandler(go.name, player, physicsEngine);
			ActuatorCoil = new DeviceCoil(player, () => SetCoil(true), () => SetCoil(false));
		}

		#region Wiring

		IApiSwitchStatus IApiSwitch.AddSwitchDest(SwitchConfig switchConfig, IApiSwitchStatus switchStatus)
			=> _switchHandler.AddSwitchDest(switchConfig.WithPulse(true), switchStatus);

		void IApiSwitch.AddWireDest(WireDestConfig wireConfig)
			=> _switchHandler.AddWireDest(wireConfig.WithPulse(true));

		void IApiSwitch.RemoveWireDest(string destId) => _switchHandler.RemoveWireDest(destId);

		IApiSwitch IApiSwitchDevice.Switch(string deviceItem) => this;
		IApiCoil IApiCoilDevice.Coil(string deviceItem) => ActuatorCoil;
		IApiWireDest IApiWireDeviceDest.Wire(string deviceItem) => ActuatorCoil;

		#endregion

		#region Events

		void IApi.OnInit(BallManager ballManager)
		{
			var wall = _component ? _component.SlingshotWall : null;
			_wallApi = wall && _player.TableApi != null ? _player.TableApi.Surface(wall) : null;
			if (_wallApi != null) {
				_wallApi.Slingshot += OnSlingshot;
			}
			Init?.Invoke(this, EventArgs.Empty);
		}

		void IApi.OnDestroy()
		{
			if (_wallApi != null) {
				_wallApi.Slingshot -= OnSlingshot;
			}
		}

		/// <summary>
		/// The wall kicked a ball: the switch closes as a pulse and, when the slingshot is
		/// self-wired, the coil fires.
		/// </summary>
		private void OnSlingshot(object sender, EventArgs args)
		{
			SwitchCount++;
			Switch?.Invoke(this, new SwitchEventArgs(true));
			_switchHandler.OnSwitch(true);
			if (_component && _component.SelfWired) {
				_component.FireSelfWiredPulse();
			}
		}

		/// <summary>Sets the coil and reports the transition. Repeated levels are no transition.</summary>
		internal void SetCoil(bool energized)
		{
			if (IsCoilEnergized == energized) {
				return;
			}
			IsCoilEnergized = energized;
			if (energized) {
				CoilEnergizeCount++;
			}
			CoilStatusChanged?.Invoke(this, new NoIdCoilEventArgs(energized));
		}

		#endregion
	}
}
