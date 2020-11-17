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
using NLog;
using UnityEngine;
using VisualPinball.Engine.VPT.Trough;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	public class TroughApi : ItemApi<Trough, TroughData>, IApi, IApiInitializable, IApiSwitchDevice, IApiCoilDevice, IApiWireDeviceDest
	{
		/// <summary>
		/// The ball manager.
		/// </summary>
		private BallManager BallManager;

		/// <summary>
		/// The kicker or trigger where the ball rolls into the trough.
		/// </summary>
		private IApiHittable _entryHittable;

		/// <summary>
		/// The exit kicker is where new balls are created when we get the eject
		/// coil event from the gamelogic engine.
		/// </summary>
		private KickerApi _exitKicker;

		/// <summary>
		/// The ball switches. These are virtual switches that don't exist on the
		/// playfield, but running <see cref="DeviceSwitch.SetSwitch"/> on them will
		/// send the event to the gamelogic engine.
		/// </summary>
		private DeviceSwitch[] _ballSwitches;

		/// <summary>
		/// Number of virtual balls currently in the trough
		/// </summary>
		private int _ballCount = 0;

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

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

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

		IApiWireDest IApiWireDeviceDest.Wire(string coilId) => (this as IApiCoilDevice).Coil(coilId);

		/// <summary>
		/// Create a ball in the trough without triggering extra events
		/// </summary>
		internal void AddBall()
		{
			if (_ballCount < Data.BallCount) {
				_ballSwitches[_ballCount].SetSwitch(true);

				_ballCount++;
			}
		}

		/// <summary>
		/// If there are any balls in the trough add one to play and
		/// trigger any switches which the remaining balls would activate
		/// </summary>
		internal void OnEjectCoil(bool closed)
		{
			if (closed && (_ballCount > 0)) {
				Logger.Info("Spawning new ball.");

				_exitKicker.CreateBall();
				_exitKicker.Kick();

				for (int i = 0; i < _ballCount; i++) {
					_ballSwitches[i].ScheduleSwitch(false, Data.SettleTime / 2);
				}

				_ballCount--;

				for (int i = 0; i < _ballCount; i++) {
					_ballSwitches[i].ScheduleSwitch(true, Data.SettleTime);
				}
			}
		}

		/// <summary>
		/// If there's room in the trough remove the ball from play
		/// and trigger any switches which it would roll over
		/// </summary>
		private void OnEntryHit(object sender, HitEventArgs args)
		{
			if (_ballCount < Data.BallCount) {
				Logger.Info("Draining ball into trough.");

				BallManager.DestroyEntity(args.BallEntity);

				int openSwitches = Data.BallCount - _ballCount;

				for (int i = 1; i < openSwitches; i++) {
					_ballSwitches[Data.BallCount - i].ScheduleSwitch(true, Data.SettleTime * i);
					_ballSwitches[Data.BallCount - i].ScheduleSwitch(false, Data.SettleTime * i + Data.SettleTime / 2);
				}

				_ballSwitches[_ballCount].ScheduleSwitch(true, Data.SettleTime * openSwitches);

				_ballCount++;
			}
		}

		#region Wiring

		void IApiInitializable.OnInit(BallManager ballManager)
		{
			BallManager = ballManager;

			// playfield elements
			_exitKicker = TableApi.Kicker(Data.ExitKicker);
			_jamTrigger = TableApi.Trigger(Data.JamSwitch);

			// setup entry handler
			_entryHittable = TableApi.Kicker(Data.EntryKicker);
			if (_entryHittable == null) {
				_entryHittable = TableApi.Trigger(Data.EntryTrigger);
			}

			if (_entryHittable != null) {
				_entryHittable.Hit += OnEntryHit;
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
					Logger.Warn($"Unknown switch ID {sw.Id}");
				}
			}

			// setup eject coil
			_ejectCoil = new TroughEjectCoil(this);

			// finally, emit the event for anyone else to chew on
			Init?.Invoke(this, EventArgs.Empty);
		}

		void IApi.OnDestroy()
		{
			Logger.Info("Destroying trough!");

			if (_entryHittable != null) {
				_entryHittable.Hit -= OnEntryHit;
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

		public void OnChange(bool enabled) => OnCoil(enabled, false);
	}
}
