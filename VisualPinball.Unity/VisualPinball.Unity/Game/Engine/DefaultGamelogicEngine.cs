// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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
using System.Diagnostics;
using NLog;
using UnityEngine;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game.Engines;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	/// <summary>
	/// The default gamelogic engine will be a showcase of how to implement a
	/// gamelogic engine. For now it just tries to find the flippers and hook
	/// them up to the switches.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu("Visual Pinball/Game Logic Engine/Default Game Logic")]
	public class DefaultGamelogicEngine : MonoBehaviour, IGamelogicEngine
	{
		public string Name { get; } = "Default Game Engine";

		public event EventHandler<CoilEventArgs> OnCoilChanged;
		public event EventHandler<LampEventArgs> OnLampChanged;
		public event EventHandler<LampsEventArgs> OnLampsChanged;
		public event EventHandler<LampColorEventArgs> OnLampColorChanged;
		public event EventHandler<AvailableDisplays> OnDisplaysAvailable;
		public event EventHandler<DisplayFrameData> OnDisplayFrame;

		private const bool DualWoundFlippers = false;

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

		public GamelogicEngineSwitch[] AvailableSwitches { get; } = {
			new GamelogicEngineSwitch(SwLeftFlipper) { Description = "Left Flipper (button)", InputActionHint = InputConstants.ActionLeftFlipper },
			new GamelogicEngineSwitch(SwRightFlipper) { Description = "Right Flipper (button)", InputActionHint = InputConstants.ActionRightFlipper },
			new GamelogicEngineSwitch(SwLeftFlipperEos) { Description = "Left Flipper (EOS)", DeviceHint = "^(LeftFlipper|LFlipper|FlipperLeft|FlipperL)$"},
			new GamelogicEngineSwitch(SwRightFlipperEos) { Description = "Right Flipper (EOS)", DeviceHint = "^(RightFlipper|RFlipper|FlipperRight|FlipperR)$"},
			new GamelogicEngineSwitch(SwTroughDrain) { Description = "Trough Drain", DeviceHint = "^Trough\\s*\\d?", DeviceItemHint = TroughAuthoring.EntrySwitchId },
			new GamelogicEngineSwitch(SwTrough1) { Description = "Trough 1 (eject)", DeviceHint = "^Trough\\s*\\d?", DeviceItemHint = "1"},
			new GamelogicEngineSwitch(SwTrough2) { Description = "Trough 2", DeviceHint = "^Trough\\s*\\d?", DeviceItemHint = "2"},
			new GamelogicEngineSwitch(SwTrough3) { Description = "Trough 3", DeviceHint = "^Trough\\s*\\d?", DeviceItemHint = "3"},
			new GamelogicEngineSwitch(SwTrough4) { Description = "Trough 4", DeviceHint = "^Trough\\s*\\d?", DeviceItemHint = "4"},
			new GamelogicEngineSwitch(SwTrough4) { Description = "Trough 4", DeviceHint = "^Trough\\s*\\d?", DeviceItemHint = "4"},
			new GamelogicEngineSwitch(SwCreateBall) { Description = "Create Debug Ball", InputActionHint = InputConstants.ActionCreateBall, InputMapHint = InputConstants.MapDebug },
			new GamelogicEngineSwitch(SwRedBumper) { Description = "Red Bumper", DeviceHint = "^Bumper1$" }
		};

		private const string CoilLeftFlipperMain = "c_flipper_left_main";
		private const string CoilLeftFlipperHold = "c_flipper_left_hold";
		private const string CoilRightFlipperMain = "c_flipper_right_main";
		private const string CoilRightFlipperHold = "c_flipper_right_hold";
		private const string CoilTroughEntry = "c_trough_entry";
		private const string CoilTroughEject = "c_trough_eject";

		public GamelogicEngineCoil[] AvailableCoils { get; } = {
			new GamelogicEngineCoil(CoilLeftFlipperMain) { Description = "Left Flipper (Main)", DeviceHint = "^(LeftFlipper|LFlipper|FlipperLeft|FlipperL)$", DeviceItemHint = FlipperAuthoring.MainCoilItem },
			new GamelogicEngineCoil(CoilLeftFlipperHold) { Description = "Left Flipper (Hold)", DeviceHint = "^(LeftFlipper|LFlipper|FlipperLeft|FlipperL)$", DeviceItemHint = FlipperAuthoring.HoldCoilItem },
			new GamelogicEngineCoil(CoilRightFlipperMain) { Description = "Right Flipper (Main)", DeviceHint = "^(RightFlipper|RFlipper|FlipperRight|FlipperR)$", DeviceItemHint = FlipperAuthoring.MainCoilItem },
			new GamelogicEngineCoil(CoilRightFlipperHold) { Description = "Right Flipper (Hold)", DeviceHint = "^(RightFlipper|RFlipper|FlipperRight|FlipperR)$", DeviceItemHint = FlipperAuthoring.HoldCoilItem },
			new GamelogicEngineCoil(CoilTroughEject) { Description = "Trough Eject", DeviceHint = "^Trough\\s*\\d?", DeviceItemHint = TroughAuthoring.EjectCoilId},
			new GamelogicEngineCoil(CoilTroughEntry) { Description = "Trough Entry", DeviceHint = "^Trough\\s*\\d?", DeviceItemHint = TroughAuthoring.EntryCoilId},
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

		public GamelogicEngineLamp[] AvailableLamps { get; } =
		{
			new GamelogicEngineLamp(GiSlingshotRightLower) { Description = "Right Slingshot (lower)", DeviceHint = "gi1$" },
			new GamelogicEngineLamp(GiSlingshotRightUpper) { Description = "Right Slingshot (upper)", DeviceHint = "gi2$" },
			new GamelogicEngineLamp(GiSlingshotLeftLower) { Description = "Left Slingshot (lower)", DeviceHint = "gi3$" },
			new GamelogicEngineLamp(GiSlingshotLeftUpper) { Description = "Left Slingshot (upper)", DeviceHint = "gi4$" },
			new GamelogicEngineLamp(GiDropTargetsRightLower) { Description = "Right Drop Targets (lower)", DeviceHint = "gi5$" },
			new GamelogicEngineLamp(GiDropTargetsRightUpper) { Description = "Right Drop Targets (upper)", DeviceHint = "gi8$" },
			new GamelogicEngineLamp(GiDropTargetsLeftLower) { Description = "Left Drop Targets (lower)", DeviceHint = "gi6$" },
			new GamelogicEngineLamp(GiDropTargetsLeftUpper) { Description = "Left Drop Targets (upper)", DeviceHint = "gi7$" },
			new GamelogicEngineLamp(GiTop1) { Description = "Top 1 (left)", DeviceHint = "gi13$" },
			new GamelogicEngineLamp(GiTop2) { Description = "Top 2", DeviceHint = "gi10$" },
			new GamelogicEngineLamp(GiTop3) { Description = "Top 3", DeviceHint = "gi9$" },
			new GamelogicEngineLamp(GiTop4) { Description = "Top 4", DeviceHint = "gi11$" },
			new GamelogicEngineLamp(GiTop5) { Description = "Top 5 (right)", DeviceHint = "gi12$" },
			new GamelogicEngineLamp(GiLowerRamp) { Description = "Ramp (lower)", DeviceHint = "gi14$" },
			new GamelogicEngineLamp(GiUpperRamp) { Description = "Ramp (upper)", DeviceHint = "gi15$" },
			new GamelogicEngineLamp(GiTopLeftPlastic) { Description = "Top Left Plastics", DeviceHint = "gi16$" },
			new GamelogicEngineLamp(LampRedBumper) { Description = "Red Bumper", DeviceHint = "^b1l2$" }
		};

		private Player _player;
		private BallManager _ballManager;
		private bool _frameSent = false;
		private PlayfieldAuthoring _playfieldComponent;

		private readonly Dictionary<string, Stopwatch> _switchTime = new Dictionary<string, Stopwatch>();

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public DefaultGamelogicEngine()
		{
			Logger.Info("New Gamelogic engine instantiated.");
		}

		public void OnInit(Player player, TableApi tableApi, BallManager ballManager)
		{
			_player = player;
			_ballManager = ballManager;

			OnDisplaysAvailable?.Invoke(this, new AvailableDisplays(new DisplayConfig(DisplayDmd, DmdWidth, DmdHeight)));

			// debug print stuff
			OnCoilChanged += DebugPrintCoil;

			// eject ball onto playfield
			OnCoilChanged?.Invoke(this, new CoilEventArgs(CoilTroughEject, true));
			_player.ScheduleAction(100, () => OnCoilChanged?.Invoke(this, new CoilEventArgs(CoilTroughEject, false)));

			_playfieldComponent = GetComponentInChildren<PlayfieldAuthoring>();
		}

		private void Update()
		{
			if (!_frameSent) {
				var frameTex = UnityEngine.Resources.Load<Texture2D>("Textures/vpe_dmd_32x128");
				var data = frameTex.GetRawTextureData<byte>().ToArray();

				// this texture happens to be stored as RGB24, so we can send the raw data directly.
				OnDisplayFrame?.Invoke(this, new DisplayFrameData(DisplayDmd, DisplayFrameFormat.Dmd24, data));

				_frameSent = true;
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
						_ballManager.CreateBall(new DebugBallCreator(_playfieldComponent.Width / 2f, _playfieldComponent.Height / 2f, _playfieldComponent.TableHeight));
					}
					break;
				}
			}
		}

		public void SetCoil(string n, bool value)
		{
			OnCoilChanged?.Invoke(this, new CoilEventArgs(n, value));
		}

		public void SetLamp(string n, int value)
		{
			OnLampChanged?.Invoke(this, new LampEventArgs(n, value));
		}

		public void SetLampColor(string n, Color color)
		{
			OnLampColorChanged?.Invoke(this, new LampColorEventArgs(n, color));
		}

		public void SetLamps(LampEventArgs[] values)
		{
			OnLampsChanged?.Invoke(this, new LampsEventArgs(values));
		}

		private void DebugPrintCoil(object sender, CoilEventArgs e)
		{
			Logger.Info("Coil {0} set to {1}.", e.Id, e.IsEnabled);
		}

		private void Flip(string id, bool isClosed)
		{
			switch (id) {

				case SwLeftFlipper:
					if (DualWoundFlippers) {
						if (isClosed) {
							OnCoilChanged?.Invoke(this, new CoilEventArgs(CoilLeftFlipperMain, true));

						} else {
							OnCoilChanged?.Invoke(this,
								_player.SwitchStatusesClosed[SwLeftFlipperEos]
									? new CoilEventArgs(CoilLeftFlipperHold, false)
									: new CoilEventArgs(CoilLeftFlipperMain, false)
							);
						}
					} else {
						OnCoilChanged?.Invoke(this, new CoilEventArgs(CoilLeftFlipperMain, isClosed));
					}

					break;

				case SwLeftFlipperEos:
					if (DualWoundFlippers) {
						if (isClosed) {
							OnCoilChanged?.Invoke(this, new CoilEventArgs(CoilLeftFlipperMain, false));
							OnCoilChanged?.Invoke(this, new CoilEventArgs(CoilLeftFlipperHold, true));
						}
					}
					break;

				case SwRightFlipper:
					if (DualWoundFlippers) {
						if (isClosed) {
							OnCoilChanged?.Invoke(this, new CoilEventArgs(CoilRightFlipperMain, true));
						} else {
							OnCoilChanged?.Invoke(this,
								_player.SwitchStatusesClosed[SwRightFlipperEos]
									? new CoilEventArgs(CoilRightFlipperHold, false)
									: new CoilEventArgs(CoilRightFlipperMain, false)
							);
						}
					} else {
						OnCoilChanged?.Invoke(this, new CoilEventArgs(CoilRightFlipperMain, isClosed));
					}
					break;

				case SwRightFlipperEos:
					if (DualWoundFlippers) {
						if (isClosed) {
							OnCoilChanged?.Invoke(this, new CoilEventArgs(CoilRightFlipperMain, false));
							OnCoilChanged?.Invoke(this, new CoilEventArgs(CoilRightFlipperHold, true));
						}
					}
					break;
			}
		}
	}
}
