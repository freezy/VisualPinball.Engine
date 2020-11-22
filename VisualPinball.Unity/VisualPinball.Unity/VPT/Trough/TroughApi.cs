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
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Trough;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	/// <summary>
	/// A trough implements all known trough behaviors that exist in the real world. <p/>
	///
	/// A trough consists of two parts:
	///
	/// - The **drain** where the ball lands after it exists the playfield. In [modern troughs](#) this part does not
	///   exist, since the balls go directly into the trough.
	/// - The **ball stack**, where balls are stored for games that hold more than one ball.
	/// </summary>
	[Api]
	public class TroughApi : ItemApi<Trough, TroughData>, IApi, IApiInitializable, IApiSwitchDevice, IApiCoilDevice, IApiWireDeviceDest
	{
		public int NumBallSwitches => Data.SwitchCount;

		/// <summary>
		/// The entry switch. <p/>
		///
		/// This is the switch that is closed when the ball lands in the drain.
		/// </summary>
		///
		/// <remarks>
		/// Is null for <see cref="TroughType.Modern"/>, all of modern's switches are in <see cref="_stackSwitches"/>.
		/// </remarks>
		public DeviceSwitch EntrySwitch { get; private set; }

		/// <summary>
		/// Returns the switch for <see cref="TroughType.TwoCoilsOneSwitch"/> multi ball troughs that only have one
		/// switch.
		/// </summary>
		///
		/// <returns>The stack switch</returns>
		public DeviceSwitch StackSwitch() => _stackSwitches[0];

		/// <summary>
		/// Returns the stack switch at a given position for <see cref="TroughType.Modern"/> and
		/// <see cref="TroughType.TwoCoilsNSwitches"/> troughs.
		/// </summary>
		///
		/// <param name="pos">Position, where 0 is the switch of the ball being ejected next.</param>
		/// <returns>Switch in the ball stack</returns>
		public DeviceSwitch StackSwitch(int pos) => _stackSwitches[pos];

		/// <summary>
		/// The stack of a trough can hold an unlimited number of balls. This counts the number of balls in the stack
		/// *additionally* to those counted by the stack's switches.
		/// </summary>
		///
		/// <remarks>
		/// Usually, games only have as many balls as stack switches and this value should always be zero.
		/// </remarks>
		public int UncountedStackBalls { get; private set; }

		/// <summary>
		/// The number of balls waiting to be drained because the drain slot is occupied. <p/>
		///
		/// Once the drain slot is freed, i.e. the ball is kicked over into the ball stack, the next undrained ball
		/// enters the drain and this number is decremented by one.
		/// </summary>
		///
		/// <remarks>
		/// Usually, the gamelogic engine immediately frees the drain slot and this value should always be zero.
		/// </remarks>
		public int UncountedDrainBalls { get; private set; }

		/// <summary>
		/// A reference to the drain switch on the playfield needed to destroy the ball and update the state of the
		/// <see cref="Trough"/>.
		/// </summary>
		private IApiSwitch _drainSwitch;

		/// <summary>
		/// A reference to the exit kicker on the playfield needed to create and kick new balls into the plunger lane.
		/// </summary>
		private KickerApi _ejectKicker;

		/// <summary>
		/// The stack switches. These are virtual switches that don't exist on the playfield, but changing their values
		/// on them sends the event to the gamelogic engine.
		/// </summary>
		///
		/// <remarks>
		/// Note that for entry-coil troughs, the entry switch isn't part of this array.
		/// </remarks>
		private DeviceSwitch[] _stackSwitches;

		/// <summary>
		/// Entry coil shoots the ball from the drain into the trough.
		/// </summary>
		///
		/// <remarks>
		/// Is null for <see cref="TroughType.Modern"/>
		/// </remarks>
		private DeviceCoil _entryCoil;

		/// <summary>
		/// Triggers the kicker that ejects the ball.
		/// </summary>
		private DeviceCoil _exitCoil;

		/// <summary>
		/// Number of virtual balls on switches in the ball stack.
		/// </summary>
		///
		/// <remarks>
		/// This does not include balls sitting in drain before being pushed into the trough.
		/// </remarks>
		private int _countedStackBalls;

		/// <summary>
		/// The player will ask for switches to hook up to the gamelogic engine,
		/// this allows fast lookup.
		/// </summary>
		private readonly Dictionary<string, IApiSwitch> _switchLookup = new Dictionary<string, IApiSwitch>();

		private bool _isSetup;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// Event emitted when the table is started.
		/// </summary>
		public event EventHandler Init;

		internal TroughApi(Trough item, Player player) : base(item, player)
		{
			Debug.Log("Trough API instantiated.");
		}

		void IApiInitializable.OnInit(BallManager ballManager)
		{
			base.OnInit(ballManager);

			// reference playfield elements
			_drainSwitch = TableApi.Switch(Data.PlayfieldEntrySwitch);
			_ejectKicker = TableApi.Kicker(Data.PlayfieldExitKicker);
			_isSetup = _drainSwitch != null && _ejectKicker != null;

			// setup entry handler
			if (_drainSwitch != null) {
				_drainSwitch.Switch += OnEntry;
			}

			// setup switches
			if (Data.Type != TroughType.Modern) {
				EntrySwitch = CreateSwitch(Trough.EntrySwitchId, false);
				_switchLookup[Trough.EntrySwitchId] = EntrySwitch;
			}

			if (Data.Type == TroughType.TwoCoilsOneSwitch) {
				_stackSwitches = new[] {
					CreateSwitch(Trough.TroughSwitchId, false)
				};
				_switchLookup[Trough.TroughSwitchId] = StackSwitch();

			} else {
				_stackSwitches = new DeviceSwitch[Data.SwitchCount];
				foreach (var sw in Item.AvailableSwitches) {
					if (int.TryParse(sw.Id, out var id)) {
						_stackSwitches[id - 1] = CreateSwitch(sw.Id, false);
						_switchLookup[sw.Id] = _stackSwitches[id - 1];

					} else {
						Logger.Warn($"Unknown switch ID {sw.Id}");
					}
				}
			}

			// setup coils
			_entryCoil = new DeviceCoil(OnEntryCoilEnabled);
			_exitCoil = new DeviceCoil(EjectBall);

			// fill up the ball stack
			for (var i = 0; i < Data.BallCount; i++) {
				AddBall();
			}

			// finally, emit the event for anyone else to chew on
			Init?.Invoke(this, EventArgs.Empty);
		}

		/// <summary>
		/// Create a ball in the ball stack without triggering extra events.
		/// </summary>
		private void AddBall()
		{
			switch (Data.Type) {
				case TroughType.Modern:
				case TroughType.TwoCoilsNSwitches:
					if (_countedStackBalls < Data.BallCount) {
						_stackSwitches[_countedStackBalls].SetSwitch(true);
						_countedStackBalls++;
					} else {
						UncountedStackBalls++;
					}
					break;

				case TroughType.TwoCoilsOneSwitch:
					if (_countedStackBalls < Data.SwitchCount - 1) {
						_countedStackBalls++;

					} else if (_countedStackBalls == Data.SwitchCount - 1) {
						_countedStackBalls++;
						StackSwitch().SetSwitch(true);

					} else {
						UncountedStackBalls++;
					}
					break;

				case TroughType.ClassicSingleBall:
					if (!EntrySwitch.IsClosed) {
						EntrySwitch.SetSwitch(true);
					}
					break;

				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		/// <summary>
		/// Destroys the ball and simulates a drain.
		/// </summary>
		private void OnEntry(object sender, SwitchEventArgs args)
		{
			Logger.Info("Draining ball into trough.");
			_drainSwitch.DestroyBall(args.BallEntity);
			DrainBall();
		}

		/// <summary>
		/// Simulates a drain, i.e. a ball entering the drain or trough directly,
		/// depending on the <see cref="TroughType"/>.
		/// </summary>
		private void DrainBall()
		{
			switch (Data.Type) {
				case TroughType.Modern:
					// ball rolls directly into the trough
					RollOverEntryBall();
					break;

				case TroughType.TwoCoilsNSwitches:
				case TroughType.TwoCoilsOneSwitch:
				case TroughType.ClassicSingleBall:

					if (EntrySwitch.IsClosed) {   // if the drain slot is already occupied, queue it.
						UncountedDrainBalls++;

					} else {                      // otherwise just close the entry switch
						EntrySwitch.ScheduleSwitch(true, Data.RollTime / 2);
					}

					break;

				default:
					throw new ArgumentOutOfRangeException();
			}
#if UNITY_EDITOR
			UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
#endif
		}

		/// <summary>
		/// Kicks the ball from the drain into the ball stack.
		/// </summary>
		///
		/// <remarks>
		/// If there are any uncounted drain balls, the next ball is drained afterwards.
		/// </remarks>
		private void OnEntryCoilEnabled()
		{
			switch (Data.Type) {
				case TroughType.Modern:
					// modern troughs don't have an entry coil
					break;

				case TroughType.TwoCoilsNSwitches:
				case TroughType.TwoCoilsOneSwitch:
					// push the ball from the drain to the trough
					if (EntrySwitch.IsClosed) {
						EntrySwitch.SetSwitch(false);
						RollOverEntryBall();
						DrainNextUncountedBall();
					}
					break;

				case TroughType.ClassicSingleBall:
					// balls get ejected immediately
					if (EntrySwitch.IsClosed) {
						EntrySwitch.SetSwitch(false);
						EjectBall();
						DrainNextUncountedBall();
					}
					break;

				default:
					throw new ArgumentOutOfRangeException();
			}
#if UNITY_EDITOR
			UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
#endif
		}

		private void DrainNextUncountedBall()
		{
			if (UncountedDrainBalls > 0) {
				DrainBall();
				UncountedDrainBalls--;
			}
		}

		/// <summary>
		/// Simulates rolling a new ball into the ball stack by enabling / disabling the switches it might hits.
		/// </summary>
		private void RollOverEntryBall()
		{
			// if more balls than switches, just count and exit
			if (_countedStackBalls >= Data.SwitchCount) {
				UncountedStackBalls++;
				return;
			}

			// start at time it takes for the ball to get into the ball stack
			var t = Data.KickTime;
			// pos 0 is the eject position, ball enter at the opposite end
			var pos = Data.SwitchCount - 1;
			var openSwitches = Data.SwitchCount - _countedStackBalls;

			switch (Data.Type) {
				case TroughType.Modern:
				case TroughType.TwoCoilsNSwitches:
					// these are switches where the balls rolls over, so close and re-open them.
					for (var i = 0; i < openSwitches - 1; i++) {
						_stackSwitches[pos].ScheduleSwitch(true, t);
						t += Data.RollTime / 2;
						_stackSwitches[pos].ScheduleSwitch(false, t);
						t += Data.RollTime;
						pos--;
					}
					// switch nearest to the eject comes last, but doesn't re-open.
					_stackSwitches[pos].ScheduleSwitch(true, t);
					_countedStackBalls++;
					break;

				case TroughType.TwoCoilsOneSwitch:
					if (_countedStackBalls < Data.SwitchCount - 1) {
						StackSwitch().ScheduleSwitch(true, t);
						t += Data.RollTime / 2;
						StackSwitch().ScheduleSwitch(false, t);

					} else if (_countedStackBalls == Data.SwitchCount - 1) {
						StackSwitch().SetSwitch(true);
					}
					_countedStackBalls++;
					break;

				case TroughType.ClassicSingleBall:
					// nothing going on here on stack side
					break;

				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		/// <summary>
		/// If there are any balls in the ball stack, add one to play and
		/// trigger any switches which the remaining balls would activate by rolling to the next position.
		/// </summary>
		private void EjectBall()
		{
			if (!_isSetup) {
				Logger.Warn($"Trough {Data.Name} not set up, ignoring.");
				return;
			}
			if (_countedStackBalls > 0) {
				Logger.Info("Spawning new ball.");

				_ejectKicker.CreateBall();
				_ejectKicker.Kick();

				// open the switch of the ejected ball immediately
				switch (Data.Type) {
					case TroughType.Modern:
					case TroughType.TwoCoilsNSwitches:
						_stackSwitches[0].SetSwitch(false);
						break;
					case TroughType.TwoCoilsOneSwitch:
					case TroughType.ClassicSingleBall:
						// no switches at position 0 here.
						break;

					default:
						throw new ArgumentOutOfRangeException();
				}
				RollOverStackBalls();
				RollOverNextUncountedBall();
			}
#if UNITY_EDITOR
			UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
#endif
		}

		/// <summary>
		/// Simulates all balls in the ball stack moving at once to the next position,
		/// due to an ejected ball.
		/// </summary>
		private void RollOverStackBalls()
		{
			var pos = _countedStackBalls - 1;
			switch (Data.Type) {
				case TroughType.Modern:
				case TroughType.TwoCoilsNSwitches:

					// don't re-close the switch nearest to the entry
					_stackSwitches[pos].ScheduleSwitch(false, Data.RollTime / 2);

					// move remaining but last ball (which has been ejected) one position further,
					// all at the same time
					for (var i = 0; i < _countedStackBalls - 2; i++) {
						pos--;
						_stackSwitches[pos].ScheduleSwitch(false, Data.RollTime / 2);
						_stackSwitches[pos].ScheduleSwitch(true, Data.RollTime);
					}

					// just close the switch for the last ball, since it has already been opened.
					if (pos-- > 0) {
						_stackSwitches[pos].ScheduleSwitch(true, Data.RollTime);
					}
					break;

				case TroughType.TwoCoilsOneSwitch:
					// there is only one switch in the trough, so if it's closed, open it.
					if (StackSwitch().IsClosed) {
						StackSwitch().ScheduleSwitch(false, Data.RollTime / 2);
					}
					break;

				case TroughType.ClassicSingleBall:
					// no switches in this trough at all.
					break;

				default:
					throw new ArgumentOutOfRangeException();
			}
			_countedStackBalls--;
		}

		private void RollOverNextUncountedBall()
		{
			if (UncountedStackBalls == 0) {
				return;
			}
			_countedStackBalls++;
			switch (Data.Type) {
				case TroughType.Modern:
				case TroughType.TwoCoilsNSwitches:
					_stackSwitches[_countedStackBalls - 1].ScheduleSwitch(true, Data.RollTime);
					break;

				case TroughType.TwoCoilsOneSwitch:
					StackSwitch().ScheduleSwitch(true, Data.RollTime);
					break;

				case TroughType.ClassicSingleBall:
					// no stack here
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
			UncountedStackBalls--;
		}

		#region Wiring

		/// <summary>
		/// This is called when the player starts. It tells the trough
		/// "please give me switch XXX so I can hook it up to the gamelogic engine".
		/// </summary>
		/// <param name="switchId"></param>
		/// <returns></returns>
		IApiSwitch IApiSwitchDevice.Switch(string switchId)
		{
			return _switchLookup.ContainsKey(switchId) ? _switchLookup[switchId] : null;
		}

		/// <summary>
		/// Returns a coil by ID. Same principle as <see cref="IApiSwitchDevice.Switch"/>
		/// </summary>
		/// <param name="coilId"></param>
		/// <returns></returns>
		IApiCoil IApiCoilDevice.Coil(string coilId)
		{
			switch (coilId) {
				case Trough.EntryCoilId:
					return _entryCoil;

				case Trough.EjectCoilId:
					return _exitCoil;

				default:
					return null;
			}
		}

		IApiWireDest IApiWireDeviceDest.Wire(string coilId) => (this as IApiCoilDevice).Coil(coilId);

		void IApi.OnDestroy()
		{
			Logger.Info("Destroying trough!");

			if (_drainSwitch != null) {
				_drainSwitch.Switch -= OnEntry;
			}
		}

		#endregion
	}
}
