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

// ReSharper disable ConditionIsAlwaysTrueOrFalse

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.InputSystem;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game.Engines;
using Debug = UnityEngine.Debug;
using NLog;
using Logger = NLog.Logger;

// uncomment to simulate dual-wound flippers
// #define DUAL_WOUND_FLIPPERS

namespace VisualPinball.Unity
{
	/// <summary>
	/// The default gamelogic engine will be a showcase of how to implement a
	/// gamelogic engine. For now it just tries to find the flippers and hook
	/// them up to the switches.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu("Visual Pinball/Gamelogic Engine/Default Game Logic")]
	public class DefaultGamelogicEngine : MonoBehaviour, IGamelogicEngine
	{
		public string Name => "Default Game Engine";

#pragma warning disable CS0067
		public event EventHandler<CoilEventArgs> OnCoilChanged;
		public event EventHandler<LampEventArgs> OnLampChanged;
		public event EventHandler<LampsEventArgs> OnLampsChanged;
		public event EventHandler<SwitchEventArgs2> OnSwitchChanged;
		public event EventHandler<RequestedDisplays> OnDisplaysRequested;
		public event EventHandler<string> OnDisplayClear;
		public event EventHandler<DisplayFrameData> OnDisplayUpdateFrame;
		public event EventHandler<EventArgs> OnStarted;
#pragma warning restore CS0067

		private const int DmdWidth = 128;
		private const int DmdHeight = 32;
		private const string DisplayDmd = "dmd0";

		private const string SwLeftFlipper = "s_left_flipper";
		private const string SwLeftFlipperEos = "s_left_flipper_eos";
		private const string SwRightFlipper = "s_right_flipper";
		private const string SwRightFlipperEos = "s_right_flipper_eos";
		private const string SwTroughDrain = "s_trough_drain";
		private const string SwTrough1 = "s_trough1";
		private const string SwTrough2 = "s_trough2";
		private const string SwTrough3 = "s_trough3";
		private const string SwTrough4 = "s_trough4";
		private const string SwCreateBall = "s_create_ball";
		private const string SwRedBumper = "s_red_bumper";

		private const string SwCannon = "s_cannon";
		private const string SwMotorStart = "s_motor_start";
		private const string SwMotorEnd = "s_motor_end";

		public GamelogicEngineSwitch[] RequestedSwitches => _availableSwitches.ToArray();
		private readonly List<GamelogicEngineSwitch> _availableSwitches = new() {
			new GamelogicEngineSwitch(SwLeftFlipper) { Description = "Left Flipper (Button)", InputActionHint = InputConstants.ActionLeftFlipper },
			new GamelogicEngineSwitch(SwRightFlipper) { Description = "Right Flipper (Button)", InputActionHint = InputConstants.ActionRightFlipper },
			new GamelogicEngineSwitch(SwTroughDrain) { Description = "Trough Drain", DeviceHint = "^Trough\\s*\\d?", DeviceItemHint = TroughComponent.EntrySwitchId },
			new GamelogicEngineSwitch(SwTrough1) { Description = "Trough 1 (Eject)", DeviceHint = "^Trough\\s*\\d?", DeviceItemHint = "1"},
			new GamelogicEngineSwitch(SwTrough2) { Description = "Trough 2", DeviceHint = "^Trough\\s*\\d?", DeviceItemHint = "2"},
			new GamelogicEngineSwitch(SwTrough3) { Description = "Trough 3", DeviceHint = "^Trough\\s*\\d?", DeviceItemHint = "3"},
			new GamelogicEngineSwitch(SwTrough4) { Description = "Trough 4", DeviceHint = "^Trough\\s*\\d?", DeviceItemHint = "4"},
			new GamelogicEngineSwitch(SwTrough4) { Description = "Trough 4", DeviceHint = "^Trough\\s*\\d?", DeviceItemHint = "4"},
			new GamelogicEngineSwitch(SwCreateBall) { Description = "Create Debug Ball", InputActionHint = InputConstants.ActionCreateBall, InputMapHint = InputConstants.MapDebug },
			new GamelogicEngineSwitch(SwRedBumper) { Description = "Red Bumper", DeviceHint = "^Bumper1$" },

			new GamelogicEngineSwitch(SwCannon) { Description = "Cannon" },
			new GamelogicEngineSwitch(SwMotorStart) { Description = "Motor Start" },
			new GamelogicEngineSwitch(SwMotorEnd) { Description = "Motor End" }
		};

		private const string CoilLeftFlipperMain = "c_flipper_left_main";
		private const string CoilLeftFlipperHold = "c_flipper_left_hold";
		private const string CoilRightFlipperMain = "c_flipper_right_main";
		private const string CoilRightFlipperHold = "c_flipper_right_hold";
		private const string CoilTroughEntry = "c_trough_entry";
		private const string CoilTroughEject = "c_trough_eject";
		private const string CoilMotorStart = "c_motor_start";

		public DisplayConfig[] RequiredDisplays => new[] { new DisplayConfig(DisplayDmd, DmdWidth, DmdHeight) };

		public GamelogicEngineCoil[] RequestedCoils => _availableCoils.ToArray();
		private readonly List<GamelogicEngineCoil> _availableCoils = new()
		{
			new GamelogicEngineCoil(CoilLeftFlipperMain) { Description = "Left Flipper (Main)", DeviceHint = "^(LeftFlipper|LFlipper|FlipperLeft|FlipperL)$", DeviceItemHint = FlipperComponent.MainCoilItem },
			new GamelogicEngineCoil(CoilLeftFlipperHold) { Description = "Left Flipper (Hold)", DeviceHint = "^(LeftFlipper|LFlipper|FlipperLeft|FlipperL)$", DeviceItemHint = FlipperComponent.HoldCoilItem },
			new GamelogicEngineCoil(CoilRightFlipperMain) { Description = "Right Flipper (Main)", DeviceHint = "^(RightFlipper|RFlipper|FlipperRight|FlipperR)$", DeviceItemHint = FlipperComponent.MainCoilItem },
			new GamelogicEngineCoil(CoilRightFlipperHold) { Description = "Right Flipper (Hold)", DeviceHint = "^(RightFlipper|RFlipper|FlipperRight|FlipperR)$", DeviceItemHint = FlipperComponent.HoldCoilItem },
			new GamelogicEngineCoil(CoilTroughEject) { Description = "Trough Eject", DeviceHint = "^Trough\\s*\\d?", DeviceItemHint = TroughComponent.EjectCoilId},
			new GamelogicEngineCoil(CoilTroughEntry) { Description = "Trough Entry", DeviceHint = "^Trough\\s*\\d?", DeviceItemHint = TroughComponent.EntryCoilId},
			new GamelogicEngineCoil(CoilMotorStart) { Description = "Cannon Motor" },
		};

		public GamelogicEngineWire[] AvailableWires { get; } = {
			new(SwLeftFlipper, CoilLeftFlipperMain, DestinationType.Coil, "Left Flipper"),
			new(SwRightFlipper, CoilRightFlipperMain, DestinationType.Coil, "Right Flipper"),
		};

		private const string GiSlingshotRightLower = "gi_1";
		private const string GiSlingshotRightUpper = "gi_2";
		private const string GiSlingshotLeftLower = "gi_3";
		private const string GiSlingshotLeftUpper = "gi_4";
		private const string GiDropTargetsRightLower = "gi_5";
		private const string GiDropTargetsLeftLower = "gi_6";
		private const string GiDropTargetsLeftUpper = "gi_7";
		private const string GiDropTargetsRightUpper = "gi_8";
		private const string GiTop3 = "gi_9";
		private const string GiTop2 = "gi_10";
		private const string GiTop4 = "gi_11";
		private const string GiTop5 = "gi_12";
		private const string GiTop1 = "gi_13";
		private const string GiLowerRamp = "gi_14";
		private const string GiUpperRamp = "gi_15";
		private const string GiTopLeftPlastic = "gi_16";

		private const string LampRedBumper = "l_bumper";

		public GamelogicEngineLamp[] RequestedLamps { get; } = {
			new(GiSlingshotRightLower) { Description = "Right Slingshot (lower)", DeviceHint = "gi1$" },
			new(GiSlingshotRightUpper) { Description = "Right Slingshot (upper)", DeviceHint = "gi2$" },
			new(GiSlingshotLeftLower) { Description = "Left Slingshot (lower)", DeviceHint = "gi3$" },
			new(GiSlingshotLeftUpper) { Description = "Left Slingshot (upper)", DeviceHint = "gi4$" },
			new(GiDropTargetsRightLower) { Description = "Right Drop Targets (lower)", DeviceHint = "gi5$" },
			new(GiDropTargetsRightUpper) { Description = "Right Drop Targets (upper)", DeviceHint = "gi8$" },
			new(GiDropTargetsLeftLower) { Description = "Left Drop Targets (lower)", DeviceHint = "gi6$" },
			new(GiDropTargetsLeftUpper) { Description = "Left Drop Targets (upper)", DeviceHint = "gi7$" },
			new(GiTop1) { Description = "Top 1 (left)", DeviceHint = "gi13$" },
			new(GiTop2) { Description = "Top 2", DeviceHint = "gi10$" },
			new(GiTop3) { Description = "Top 3", DeviceHint = "gi9$" },
			new(GiTop4) { Description = "Top 4", DeviceHint = "gi11$" },
			new(GiTop5) { Description = "Top 5 (right)", DeviceHint = "gi12$" },
			new(GiLowerRamp) { Description = "Ramp (lower)", DeviceHint = "gi14$" },
			new(GiUpperRamp) { Description = "Ramp (upper)", DeviceHint = "gi15$" },
			new(GiTopLeftPlastic) { Description = "Top Left Plastics", DeviceHint = "gi16$" },
			new(LampRedBumper) { Description = "Red Bumper", DeviceHint = "^b1l2$" }
		};

		private Player _player;
		private BallManager _ballManager;
		private bool _frameSent;
		private PlayfieldComponent _playfieldComponent;
		private const float FlipperLag = 0f;

		private readonly Dictionary<string, Stopwatch> _switchTime = new();

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		[NonSerialized]
		private bool _flippersEnabled = true;

		public DefaultGamelogicEngine()
		{
			Logger.Info("New Gamelogic engine instantiated.");

			#if DUAL_WOUND_FLIPPERS
				_availableCoils.AddRange(new [] {
					new GamelogicEngineCoil(CoilLeftFlipperHold) { Description = "Left Flipper (Hold)", DeviceHint = "^(LeftFlipper|LFlipper|FlipperLeft|FlipperL)$", DeviceItemHint = FlipperComponent.HoldCoilItem },
					new GamelogicEngineCoil(CoilRightFlipperHold) { Description = "Right Flipper (Hold)", DeviceHint = "^(RightFlipper|RFlipper|FlipperRight|FlipperR)$", DeviceItemHint = FlipperComponent.HoldCoilItem },
				});

				_availableSwitches.AddRange(new [] {
					new GamelogicEngineSwitch(SwLeftFlipperEos) { Description = "Left Flipper (EOS)", DeviceHint = "^(LeftFlipper|LFlipper|FlipperLeft|FlipperL)$"},
					new GamelogicEngineSwitch(SwRightFlipperEos) { Description = "Right Flipper (EOS)", DeviceHint = "^(RightFlipper|RFlipper|FlipperRight|FlipperR)$"},
				});
			#endif
		}

		public void OnInit(Player player, TableApi tableApi, BallManager ballManager)
		{
			_player = player;
			_ballManager = ballManager;

			OnDisplaysRequested?.Invoke(this, new RequestedDisplays(new DisplayConfig(DisplayDmd, DmdWidth, DmdHeight)));

			// debug print stuff
			OnCoilChanged += DebugPrintCoil;

			// eject ball onto playfield
			OnCoilChanged?.Invoke(this, new CoilEventArgs(CoilTroughEject, true));
			_player.ScheduleAction(100, () => OnCoilChanged?.Invoke(this, new CoilEventArgs(CoilTroughEject, false)));

			_playfieldComponent = GetComponentInChildren<PlayfieldComponent>();

			OnStarted?.Invoke(this, EventArgs.Empty);
		}

		private void Update()
		{
			if (!_frameSent) {
				var frameTex = UnityEngine.Resources.Load<Texture2D>("Textures/vpe_dmd_32x128");
				var data = frameTex.GetRawTextureData<byte>().ToArray();

				// this texture happens to be stored as RGB24, so we can send the raw data directly.
				OnDisplayUpdateFrame?.Invoke(this, new DisplayFrameData(DisplayDmd, DisplayFrameFormat.Dmd24, data));

				_frameSent = true;
			}

			if (Keyboard.current.fKey.wasPressedThisFrame) {
				Debug.Log("Flippers disabled");
				_flippersEnabled = false;
			}

			if (Keyboard.current.fKey.wasReleasedThisFrame) {
				Debug.Log("Flippers enabled");
				_flippersEnabled = true;
			}
		}

		public void OnDestroy()
		{
			OnCoilChanged -= DebugPrintCoil;
		}

		public void Switch(string id, bool isClosed)
		{
			if (!_switchTime.ContainsKey(id)) {
				_switchTime[id] = new Stopwatch();
			}

			if (isClosed) {
				_switchTime[id].Restart();
			} else {
				_switchTime[id].Stop();
			}
			Logger.Info("Switch {0} is {1}.", id, isClosed ? "closed" : "open after " + _switchTime[id].ElapsedMilliseconds + "ms");

			switch (id) {

				case SwLeftFlipper:
				case SwLeftFlipperEos:
				case SwRightFlipper:
				case SwRightFlipperEos:
					Flip(id, isClosed);
					break;

				case SwTrough4:
					if (isClosed) {
						OnCoilChanged?.Invoke(this, new CoilEventArgs(CoilTroughEject, true));
						_player.ScheduleAction(100, () => OnCoilChanged?.Invoke(this, new CoilEventArgs(CoilTroughEject, false)));
					}
					break;

				case SwRedBumper:
					OnLampChanged?.Invoke(this, new LampEventArgs(LampRedBumper, isClosed ? 1 : 0));
					break;

				case SwCreateBall: {
					if (isClosed) {
						_ballManager.CreateBall(new DebugBallCreator(630, _playfieldComponent.Height / 2f));
					}
					break;
				}

				case SwCannon: {
					SetCoil(CoilMotorStart, true);
					break;
				}

				case SwMotorStart: {
					if (isClosed) {
						SetCoil(CoilMotorStart, false);
					}
					break;
				}

				case SwMotorEnd: {

					break;
				}
			}

			OnSwitchChanged?.Invoke(this, new SwitchEventArgs2(id, isClosed));
		}

		public void SetCoil(string n, bool value)
		{
			OnCoilChanged?.Invoke(this, new CoilEventArgs(n, value));
		}

		public void SetLamp(string id, float value, bool isCoil = false, LampSource source = LampSource.Lamp)
		{
			OnLampChanged?.Invoke(this, new LampEventArgs(id, value, isCoil, source));
		}

		public void SetLamps(LampEventArgs[] values)
		{
			OnLampsChanged?.Invoke(this, new LampsEventArgs(values));
		}

		void IGamelogicEngine.DisplayChanged(DisplayFrameData displayFrameData)
		{
		}

		public LampState GetLamp(string id)
		{
			return _player.LampStatuses.ContainsKey(id) ? _player.LampStatuses[id] : LampState.Default;
		}

		public bool GetSwitch(string id)
		{
			return _player.SwitchStatuses.ContainsKey(id) && _player.SwitchStatuses[id].IsSwitchEnabled;
		}

		public bool GetCoil(string id)
		{
			return _player.CoilStatuses.ContainsKey(id) && _player.CoilStatuses[id];
		}

		private void DebugPrintCoil(object sender, CoilEventArgs e)
		{
			Logger.Info("Coil {0} set to {1}.", e.Id, e.IsEnabled);
		}

		private void Flip(string id, bool isClosed)
		{
			switch (id) {

				case SwLeftFlipper:
					#if DUAL_WOUND_FLIPPERS
						if (isClosed) {
							OnCoilChanged?.Invoke(this, new CoilEventArgs(CoilLeftFlipperMain, true));

						} else {
							OnCoilChanged?.Invoke(this,
								_player.SwitchStatusesClosed[SwLeftFlipperEos]
									? new CoilEventArgs(CoilLeftFlipperHold, false)
									: new CoilEventArgs(CoilLeftFlipperMain, false)
							);
						}
					#else
						if (_flippersEnabled) {
							Wait(FlipperLag, () => OnCoilChanged?.Invoke(this, new CoilEventArgs(CoilLeftFlipperMain, isClosed)));
						}
					#endif
					break;

				case SwLeftFlipperEos:
					#if DUAL_WOUND_FLIPPERS
						if (isClosed) {
							OnCoilChanged?.Invoke(this, new CoilEventArgs(CoilLeftFlipperMain, false));
							OnCoilChanged?.Invoke(this, new CoilEventArgs(CoilLeftFlipperHold, true));
						}
					#endif
					break;

				case SwRightFlipper:
					#if DUAL_WOUND_FLIPPERS
						if (isClosed) {
							OnCoilChanged?.Invoke(this, new CoilEventArgs(CoilRightFlipperMain, true));
						} else {
							OnCoilChanged?.Invoke(this,
								_player.SwitchStatusesClosed[SwRightFlipperEos]
									? new CoilEventArgs(CoilRightFlipperHold, false)
									: new CoilEventArgs(CoilRightFlipperMain, false)
							);
						}
					#else
						if (_flippersEnabled) {
							Wait(FlipperLag, () => OnCoilChanged?.Invoke(this, new CoilEventArgs(CoilRightFlipperMain, isClosed)));
						}
					#endif
					break;

				case SwRightFlipperEos:
					#if DUAL_WOUND_FLIPPERS
						if (isClosed) {
							OnCoilChanged?.Invoke(this, new CoilEventArgs(CoilRightFlipperMain, false));
							OnCoilChanged?.Invoke(this, new CoilEventArgs(CoilRightFlipperHold, true));
						}
					#endif
					break;
			}
		}

		private void Wait(float seconds, Action action) => StartCoroutine(_wait(seconds, action));

		private static IEnumerator _wait(float time, Action callback){
			yield return new WaitForSeconds(time);
			callback();
		}
	}
}
