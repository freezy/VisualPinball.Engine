﻿// Visual Pinball Engine
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
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NLog;
using UnityEngine;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Trough;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	/// <summary>
	/// A trough that implements all known trough behaviors that exist in the real world. <p/>
	/// </summary>
	///
	/// <remarks>
	/// A trough consists of two parts:
	///
	/// - The **drain** (often also called "out hole" where the ball lands after it exists the playfield. In
	///   modern troughs this part does not exist, since the balls go directly into the trough.
	/// - The **ball stack**, where balls are stored for games that hold more than one ball.
	/// </remarks>
	[Api]
	public class TroughApi : ItemApi<TroughComponent, TroughData>,
		IApi, IApiSwitchDevice, IApiCoilDevice, IApiWireDeviceDest
	{
		/// <summary>
		/// How many stack switches there are available.
		/// </summary>
		///
		/// <remarks>
		/// The drain switch is not considered a stack switch.
		/// </remarks>
		public int NumStackSwitches => MainComponent.SwitchCount;

		/// <summary>
		/// The virtual switch that is enabled when the ball lands in the drain.
		/// </summary>
		///
		/// <remarks>
		/// Is null for <see cref="TroughType.ModernOpto"/>, all of modern's switches are in <see cref="StackSwitch(int)"/>.
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
		/// Returns the stack switch at a given position for <see cref="TroughType.ModernOpto"/>,
		/// <see cref="TroughType.ModernMech"/> and <see cref="TroughType.TwoCoilsNSwitches"/> troughs.
		/// </summary>
		///
		/// <param name="pos">Position, where 0 is the switch of the ball being ejected next.</param>
		/// <returns>Switch in the ball stack</returns>
		public DeviceSwitch StackSwitch(int pos) => _stackSwitches[pos];

		/// <summary>
		/// The virtual switch that sits right above ball 1 and shortly enables and disables after eject.
		/// </summary>
		public DeviceSwitch JamSwitch;

		/// <summary>
		/// The virtual coil that shoots the ball from the drain into the trough.
		/// </summary>
		///
		/// <remarks>
		/// Is null for <see cref="TroughType.ModernOpto"/> and <see cref="TroughType.ModernMech"/>
		/// </remarks>
		public DeviceCoil EntryCoil;

		/// <summary>
		/// The virtual coil that ejects the ball.
		/// </summary>
		public DeviceCoil ExitCoil;

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
		/// A reference to the exit kicker's coil. It's separate from <see cref="_ejectKicker"/>, because a kicker
		/// can have multiple coils.
		/// </summary>
		private IApiCoil _ejectCoil;

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

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// Event emitted when the table is started.
		/// </summary>
		public event EventHandler Init;

		internal TroughApi(GameObject go, Player player, PhysicsEngine physicsEngine) : base(go, player, physicsEngine)
		{
		}

		void IApi.OnInit(BallManager ballManager)
		{
			base.OnInit(ballManager);

			// reference playfield elements
			_drainSwitch = TableApi.Switch(MainComponent.PlayfieldEntrySwitch, MainComponent.PlayfieldEntrySwitchItem);
			_ejectCoil = TableApi.Coil(MainComponent.PlayfieldExitKicker, MainComponent.PlayfieldExitKickerItem);
			_ejectKicker = TableApi.Kicker(MainComponent.PlayfieldExitKicker);

			// setup entry handler
			if (_drainSwitch != null) {
				_drainSwitch.Switch += OnEntry;
			}

			// setup switches
			if (MainComponent.Type != TroughType.ModernOpto && MainComponent.Type != TroughType.ModernMech) {
				EntrySwitch = CreateSwitch(TroughComponent.EntrySwitchId, false, SwitchDefault.NormallyOpen);
				_switchLookup[TroughComponent.EntrySwitchId] = EntrySwitch;
			}

			if (MainComponent.Type == TroughType.TwoCoilsOneSwitch) {
				_stackSwitches = new[] {
					CreateSwitch(TroughComponent.TroughSwitchId, false, SwitchDefault.NormallyOpen)
				};
				_switchLookup[TroughComponent.TroughSwitchId] = StackSwitch();

			} else {
				_stackSwitches = new DeviceSwitch[MainComponent.SwitchCount];

				// ball_switch_# switches created in TroughComponent
				var ballSwitchRegex = new Regex(@"^ball_switch_(\d+)$");
				foreach (var @switch in MainComponent.AvailableSwitches) {
					var match = ballSwitchRegex.Match(@switch.Id);
					if (match.Success) {
						int.TryParse(match.Groups[1].Value, out int id);
						if (id > 0) {
							_stackSwitches[id - 1] = CreateSwitch(@switch.Id, false, MainComponent.Type == TroughType.ModernOpto ? SwitchDefault.NormallyClosed : SwitchDefault.NormallyOpen);
							_switchLookup[@switch.Id] = _stackSwitches[id - 1];
						}
					}
				}

				// pull next ball on modern
				if (MainComponent.Type == TroughType.ModernOpto || MainComponent.Type == TroughType.ModernMech) {
					_stackSwitches[MainComponent.SwitchCount - 1].Switch += OnLastStackSwitch;
				}
			}

			if (MainComponent.JamSwitch) {
				JamSwitch = CreateSwitch(TroughComponent.JamSwitchId, false, MainComponent.Type == TroughType.ModernOpto ? SwitchDefault.NormallyClosed : SwitchDefault.NormallyOpen);
				_switchLookup[TroughComponent.JamSwitchId] = JamSwitch;
			}

			// setup coils
			EntryCoil = new DeviceCoil(Player, OnEntryCoilEnabled);
			ExitCoil = new DeviceCoil(Player, () => EjectBall());

			// fill up the ball stack
			var ballCount = MainComponent.Type == TroughType.ClassicSingleBall ? 1 : MainComponent.BallCount;
			for (var i = 0; i < ballCount; i++) {
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
			switch (MainComponent.Type) {
				case TroughType.ModernOpto:
				case TroughType.ModernMech:
				case TroughType.TwoCoilsNSwitches:
					if (_countedStackBalls < MainComponent.BallCount) {
						_stackSwitches[_countedStackBalls].SetSwitch(true);
						_countedStackBalls++;
					} else {
						UncountedStackBalls++;
					}
					break;

				case TroughType.TwoCoilsOneSwitch:
					if (_countedStackBalls < MainComponent.SwitchCount - 1) {
						_countedStackBalls++;

					} else if (_countedStackBalls == MainComponent.SwitchCount - 1) {
						_countedStackBalls++;
						StackSwitch().SetSwitch(true);

					} else {
						UncountedStackBalls++;
					}
					break;

				case TroughType.ClassicSingleBall:
					if (!EntrySwitch.IsSwitchEnabled) {
						_countedStackBalls++; // entry and stack is the same here
						EntrySwitch.SetSwitch(true);

					} else {
						UncountedStackBalls++;
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
			if (args.IsEnabled) {
				Logger.Info("Draining ball into trough.");
				if (_drainSwitch is KickerApi kickerApi) {
					kickerApi.DestroyBall();
				} else {
					BallManager.DestroyBall(args.BallId);
				}
				DrainBall();

			} else {
				Logger.Error("Draining ball into trough.");
			}
		}

		/// <summary>
		/// Simulates a drain, i.e. a ball entering the drain or trough directly,
		/// depending on the <see cref="TroughType"/>.
		/// </summary>
		private void DrainBall()
		{
			switch (MainComponent.Type) {
				case TroughType.ModernOpto:
				case TroughType.ModernMech:
					// ball rolls directly into the trough
					RollOverEntryBall(0);
					break;

				case TroughType.TwoCoilsNSwitches:
				case TroughType.TwoCoilsOneSwitch:
					if (EntrySwitch.IsSwitchEnabled) {   // if the drain slot is already occupied, queue it.
						UncountedDrainBalls++;

					} else {                      // otherwise just close the entry switch
						EntrySwitch.ScheduleSwitch(true, MainComponent.RollTime / 2);
					}

					break;

				case TroughType.ClassicSingleBall:
					AddBall();
					break;

				default:
					throw new ArgumentOutOfRangeException();
			}
#if UNITY_EDITOR
			RefreshUI();
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
			switch (MainComponent.Type) {
				case TroughType.ModernOpto:
				case TroughType.ModernMech:
					// modern troughs don't have an entry coil
					break;

				case TroughType.TwoCoilsNSwitches:
				case TroughType.TwoCoilsOneSwitch:
					// push the ball from the drain to the trough
					if (EntrySwitch.IsSwitchEnabled) {
						EntrySwitch.SetSwitch(false);
						RollOverEntryBall(0);
						DrainNextUncountedBall();
					}
					break;

				case TroughType.ClassicSingleBall:
					throw new InvalidOperationException("Single ball trough does not have an entry coil.");
					break;

				default:
					throw new ArgumentOutOfRangeException();
			}
#if UNITY_EDITOR
			RefreshUI();
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
		private void RollOverEntryBall(int t)
		{
			// if more balls than switches, just count and exit
			if (_countedStackBalls >= MainComponent.SwitchCount) {
				UncountedStackBalls++;
				return;
			}

			// pos 0 is the eject position, ball enters at the opposite end
			var pos = MainComponent.SwitchCount - 1;

			switch (MainComponent.Type) {
				case TroughType.ModernOpto:
				case TroughType.ModernMech:
				case TroughType.TwoCoilsNSwitches:
					// if entry position is occupied by another ball that just went in, queue.
					if (_stackSwitches[pos].IsSwitchEnabled) {
						UncountedStackBalls++;
						return;
					}
					_countedStackBalls++;

					// these are switches where the balls rolls over, so close and re-open them.
					for (var i = 0; i < MainComponent.SwitchCount - _countedStackBalls; i++) {
						_stackSwitches[pos].ScheduleSwitch(true, t);

						t += MainComponent.RollTimeDisabled;
						_stackSwitches[pos].ScheduleSwitch(false, t);
						t += MainComponent.RollTimeEnabled;
						pos--;
					}
					// switch nearest to the eject comes last, but doesn't re-open.
					_stackSwitches[pos].ScheduleSwitch(true, t);

					break;

				case TroughType.TwoCoilsOneSwitch:
					_countedStackBalls++;
					if (_countedStackBalls < MainComponent.SwitchCount) {
						StackSwitch().ScheduleSwitch(true, t);
						t += MainComponent.RollTimeDisabled;
						StackSwitch().ScheduleSwitch(false, t);

					} else if (_countedStackBalls == MainComponent.SwitchCount) {
						StackSwitch().SetSwitch(true);
					}

					break;

				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		/// <summary>
		/// If there are any balls in the ball stack, eject one to the playfield.
		/// </summary>
		///
		/// <remarks>
		/// This triggers any switches which the remaining balls would activate by rolling to the next position.
		/// </remarks>
		///
		/// <returns>True if a ball was ejected, false if there were no balls in the stack to eject.</returns>
		public bool EjectBall()
		{
			if (_countedStackBalls > 0) {

				// open the switch of the ejected ball immediately
				switch (MainComponent.Type) {
					case TroughType.ModernOpto:
					case TroughType.ModernMech:
					case TroughType.TwoCoilsNSwitches:
						if (!_stackSwitches[0].IsSwitchEnabled) {
							Logger.Warn("Ball not in eject position yet, ignoring.");
							return false;
						}
						break;

					case TroughType.TwoCoilsOneSwitch:
						// no switches at position 0 here.
						break;

					case TroughType.ClassicSingleBall:
						if (!EntrySwitch.IsSwitchEnabled) {
							Logger.Warn("No ball, ignoring.");
							return false;
						}
						break;

					default:
						throw new ArgumentOutOfRangeException();
				}


				if (_ejectKicker == null) {
					Logger.Warn("Trough: Cannot spawn ball without an exit kicker.");
					return false;
				}
				Logger.Info("Trough: Spawning new ball.");
				_ejectKicker.CreateBall();
				_ejectCoil.OnCoil(true);

				// open the switch of the ejected ball immediately
				switch (MainComponent.Type) {
					case TroughType.ModernOpto:
					case TroughType.ModernMech:
					case TroughType.TwoCoilsNSwitches:
						_stackSwitches[0].SetSwitch(false);
						break;

					case TroughType.ClassicSingleBall:
						EntrySwitch.SetSwitch(false);
						break;

					// no switches at position 0 for other types.
				}

				TriggerJamSwitch();
				RollOverStackBalls();
				RollOverNextUncountedStackBall();
#if UNITY_EDITOR
				RefreshUI();
#endif
				return true;
			}

			return false;
		}

		private void TriggerJamSwitch()
		{
			if (MainComponent.JamSwitch) {
				JamSwitch.ScheduleSwitch(true, MainComponent.KickTime / 2);
				JamSwitch.ScheduleSwitch(false, MainComponent.KickTime);
			}
		}

		/// <summary>
		/// Simulates all balls in the ball stack moving at once to the next position,
		/// due to an ejected ball.
		/// </summary>
		private void RollOverStackBalls()
		{
			var pos = _countedStackBalls - 1;
			switch (MainComponent.Type) {

				case TroughType.ModernOpto:
				case TroughType.ModernMech:
				case TroughType.TwoCoilsNSwitches:

					// don't re-close the switch nearest to the entry
					_stackSwitches[pos].ScheduleSwitch(false, MainComponent.RollTimeDisabled);

					// move remaining but last ball (which has been ejected) one position further,
					// all at the same time
					for (var i = 0; i < _countedStackBalls - 2; i++) {
						pos--;
						if (MainComponent.RollTimeEnabled != 0) {
							_stackSwitches[pos].ScheduleSwitch(false, MainComponent.RollTimeDisabled);
							_stackSwitches[pos].ScheduleSwitch(true, MainComponent.RollTime);
						}
					}

					// just close the switch for the last ball, since it has already been opened.
					if (pos-- > 0) {
						_stackSwitches[pos].ScheduleSwitch(true, MainComponent.RollTime);
					}
					break;

				case TroughType.TwoCoilsOneSwitch:
					// there is only one switch in the trough, so if it's closed, open it.
					if (StackSwitch().IsSwitchEnabled) {
						StackSwitch().ScheduleSwitch(false, MainComponent.RollTimeDisabled);
					}
					break;

				case TroughType.ClassicSingleBall:
					// no switches in this trough.
					break;

				default:
					throw new ArgumentOutOfRangeException();
			}
			_countedStackBalls--;
		}

		private void OnLastStackSwitch(object sender, SwitchEventArgs switchEventArgs)
		{
			if (!switchEventArgs.IsEnabled && UncountedStackBalls > 0) {
#if UNITY_EDITOR
				RefreshUI();
#endif
				UncountedStackBalls--;
				RollOverEntryBall(MainComponent.RollTime / 2);
#if UNITY_EDITOR
				RefreshUI();
#endif
			}
		}

		private void RollOverNextUncountedStackBall()
		{
			if (UncountedStackBalls == 0) {
				return;
			}
			_countedStackBalls++;
			switch (MainComponent.Type) {
				case TroughType.ModernOpto:
				case TroughType.ModernMech:
				case TroughType.TwoCoilsNSwitches:
					_stackSwitches[_countedStackBalls - 1].ScheduleSwitch(true, MainComponent.RollTime);
					break;

				case TroughType.TwoCoilsOneSwitch:
					StackSwitch().ScheduleSwitch(true, MainComponent.RollTime / 2);
					break;

				case TroughType.ClassicSingleBall:
					// no stack here
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
			UncountedStackBalls--;
		}

#if UNITY_EDITOR
		private void RefreshUI()
		{
			if (!Player.UpdateDuringGamplay) {
				return;
			}

			foreach (var editor in (UnityEditor.Editor[])Resources.FindObjectsOfTypeAll(Type.GetType("VisualPinball.Unity.Editor.TroughInspector, VisualPinball.Unity.Editor"))) {
				editor.Repaint();
			}
		}
#endif

		#region Wiring

		/// <summary>
		/// This is called when the player starts. It tells the trough
		/// "please give me switch XXX so I can hook it up to the gamelogic engine".
		/// </summary>
		/// <param name="deviceItem"></param>
		/// <returns></returns>
		IApiSwitch IApiSwitchDevice.Switch(string deviceItem)
		{
			if (deviceItem == null) {
				throw new ArgumentException("Must provide a non-null switch ID!");
			}
			return _switchLookup.ContainsKey(deviceItem) ? _switchLookup[deviceItem] : null;
		}

		IApiCoil IApiCoilDevice.Coil(string deviceItem) => Coil(deviceItem);
		IApiWireDest IApiWireDeviceDest.Wire(string deviceItem) => Coil(deviceItem);

		/// <summary>
		/// Returns a coil by ID. Same principle as <see cref="IApiSwitchDevice.Switch"/>
		/// </summary>
		/// <param name="deviceItem"></param>
		/// <returns></returns>
		private IApiCoil Coil(string deviceItem)
		{
			return deviceItem switch {
				TroughComponent.EntryCoilId => EntryCoil,
				TroughComponent.EjectCoilId => ExitCoil,
				_ => throw new ArgumentException($"Unknown trough coil \"{deviceItem}\". Valid names are: [ \"{TroughComponent.EntryCoilId}\", \"{TroughComponent.EjectCoilId}\" ].")
			};
		}

		void IApi.OnDestroy()
		{
			Logger.Info("Destroying trough!");

			if (_drainSwitch != null) {
				_drainSwitch.Switch -= OnEntry;
			}
			if (MainComponent.Type == TroughType.ModernOpto || MainComponent.Type == TroughType.ModernMech) {
				_stackSwitches[MainComponent.SwitchCount - 1].Switch -= OnLastStackSwitch;
			}
		}

		#endregion
	}
}
