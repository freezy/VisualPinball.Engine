// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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
using System.Collections.Generic;
using UnityEngine;
using VisualPinball.Engine.VPT.Trough;
using VisualPinball.Unity.Switch;

namespace VisualPinball.Unity
{
	public class TroughApi : ItemApi<Trough, TroughData>, IApi, IApiInitializable, IApiSwitchDevice, IApiCoilDevice
	{
		/// <summary>
		/// The entry kicker is where the ball rolls into the trough.
		/// </summary>
		private KickerApi _entryKicker;

		/// <summary>
		/// The exit kicker is where new balls are created when we get the eject
		/// coil event from the gamelogic engine.
		/// </summary>
		private KickerApi _exitKicker;

		/// <summary>
		/// The ball switches. These are virtual switches that don't exist on the
		/// playfield, but running <see cref="DeviceSwitch.OnSwitch"/> on them will
		/// send the event to the gamelogic engine.
		/// </summary>
		private DeviceSwitch[] _ballSwitches;

		/// <summary>
		/// Eject coil triggers the kicker that throws out the ball.
		/// </summary>
		private IApiCoil _ejectCoil;

		/// <summary>
		/// Since TriggerApi implements IApiSwitch, we return directly this when the
		/// engine asks for the jam switch.
		/// </summary>
		///
		/// <remarks>
		/// Basically we short-wire the jam trigger to the switch, so the jam trigger
		/// events go directly to the gamelogic engine.
		///
		/// In case we need to hook into the jam trigger logic here, uncomment the
		/// blocks below (ctrl+f jam events)
		/// </remarks>
		private TriggerApi _jamTrigger;

		/// <summary>
		/// The player will ask for switches to hook up to the gamelogic engine,
		/// this allows fast lookup.
		/// </summary>
		private readonly Dictionary<string, IApiSwitch> _switchLookup = new Dictionary<string, IApiSwitch>();

		private DeviceSwitch EntrySwitch => _ballSwitches[Data.SwitchCount - 1];
		private DeviceSwitch EjectSwitch => _ballSwitches[0];

		/// <summary>
		/// Event emitted when the table is started.
		/// </summary>
		public event EventHandler Init;

		internal TroughApi(Trough item, Player player) : base(item, player)
		{
			Debug.Log("Trough API instantiated.");
		}

		/// <summary>
		/// This is called when the player starts. It tells the trough
		/// "please give me switch XXX so I can hook it up to the gamelogic engine".
		/// </summary>
		/// <param name="switchId"></param>
		/// <returns></returns>
		IApiSwitch IApiSwitchDevice.Switch(string switchId)
		{
			// if the engine is asking for the jam switch, return the trigger directly.
			if (switchId == Trough.JamSwitchId) {
				return _jamTrigger;
			}
			return _switchLookup.ContainsKey(switchId) ? _switchLookup[switchId] : null;
		}

		/// <summary>
		/// Returns a coil by ID. Same principle as <see cref="Switch"/>
		/// </summary>
		/// <param name="coilId"></param>
		/// <returns></returns>
		IApiCoil IApiCoilDevice.Coil(string coilId)
		{
			if (coilId == Trough.EjectCoilId) {
				return _ejectCoil;
			}
			return null;
		}

		internal void OnEjectCoil(bool closed)
		{
			if (closed) {
				Debug.Log("Spawning new ball.");
				_exitKicker.CreateBall();
				_exitKicker.Kick();

				EntrySwitch.OnSwitch(false);
			}
		}

		private void OnEntryKickerHit(object sender, EventArgs args)
		{
			Debug.Log("Draining ball.");
			(sender as KickerApi)?.DestroyBall();

			// todo properly close ball switch, etc
			EntrySwitch.OnSwitch(true);

		}

		#region Wiring

		void IApiInitializable.OnInit(BallManager ballManager)
		{
			// playfield elements
			_entryKicker = TableApi.Kicker(Data.EntryKicker);
			_exitKicker = TableApi.Kicker(Data.ExitKicker);
			_jamTrigger = TableApi.Trigger(Data.JamSwitch);

			// setup entry kicker handler
			if (_entryKicker != null) {
				_entryKicker.Hit += OnEntryKickerHit;
			}

			// in case we need also need to handle jam events here, uncomment
			// if (_jamTrigger != null) {
			// 	_jamTrigger.Hit += OnJamTriggerHit;
			// 	_jamTrigger.UnHit += OnJamTriggerUnHit;
			// }

			// create switches to hook up
			_ballSwitches = new DeviceSwitch[Data.SwitchCount];
			foreach (var sw in Item.AvailableSwitches) {
				if (int.TryParse(sw.Id, out var id)) {
					_ballSwitches[id - 1] = CreateSwitch(false);
					_switchLookup[sw.Id] = _ballSwitches[id - 1];

				} else if (sw.Id == Trough.JamSwitchId) {
					// we short-wire the jam trigger to the switch, so we don't care about it here,
					// all the jam trigger does push its switch events to the gamelogic engine. in
					// case we need to hook into the jam trigger logic here, uncomment those two
					// lines and and relay the events manually to the engine.
					//_jamSwitch = CreateSwitch(false);
					//_switchLookup[sw.Id] = _jamSwitch;

				} else {
					Debug.LogWarning($"Unknown switch ID {sw.Id}");
				}
			}

			// setup eject coil
			_ejectCoil = new TroughEjectCoil(this);

			// finally, emit the event for anyone else to chew on
			Init?.Invoke(this, EventArgs.Empty);
		}

		void IApi.OnDestroy()
		{
			Debug.Log("Destroying trough!");

			if (_entryKicker != null) {
				_entryKicker.Hit -= OnEntryKickerHit;
			}

			// in case we need also need to handle jam events here, uncomment
			// if (_jamTrigger != null) {
			// 	_jamTrigger.Hit -= OnJamTriggerHit;
			// 	_jamTrigger.UnHit -= OnJamTriggerUnHit;
			// }
		}

		// in case we need also need to handle jam events here, uncomment
		// private IApiSwitch _jamSwitch;
		// private void OnJamTriggerHit(object sender, EventArgs e) => _jamSwitch?.OnSwitch(true);
		// private void OnJamTriggerUnHit(object sender, EventArgs e) => _jamSwitch?.OnSwitch(false);

		#endregion
	}

	internal class TroughEjectCoil : IApiCoil
	{
		private readonly TroughApi _troughApi;

		public TroughEjectCoil(TroughApi troughApi)
		{
			_troughApi = troughApi;
		}

		public void OnCoil(bool closed, bool isHoldCoil)
		{
			_troughApi.OnEjectCoil(closed);
		}
	}
}
