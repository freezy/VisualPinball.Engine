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

namespace VisualPinball.Unity
{
	/// <summary>
	/// The default gamelogic engine will be a showcase of how to implement a
	/// gamelogic engine. For now it just tries to find the flippers and hook
	/// them up to the switches.
	/// </summary>
	[Serializable]
	public class DefaultGamelogicEngine : IGamelogicEngine, IGamelogicEngineWithSwitches, IGamelogicEngineWithCoils
	{
		public string Name => "Default Game Engine";

		public string[] AvailableSwitches { get; } = {SwLeftFlipper, SwRightFlipper, SwLeftFlipperEos, SwRightFlipperEos, SwPlunger, SwCreateBall};

		public string[] AvailableCoils { get; } = {CoilLeftFlipperMain, CoilLeftFlipperHold, CoilRightFlipperMain, CoilRightFlipperHold, CoilAutoPlunger};

		private const string SwLeftFlipper = "s_left_flipper";
		private const string SwLeftFlipperEos = "s_left_flipper_eos";
		private const string SwRightFlipper = "s_right_flipper";
		private const string SwRightFlipperEos = "s_right_flipper_eos";
		private const string SwPlunger = "s_plunger";
		private const string SwCreateBall = "s_create_ball";

		private const string CoilLeftFlipperMain = "c_flipper_left_main";
		private const string CoilLeftFlipperHold = "c_flipper_left_hold";
		private const string CoilRightFlipperMain = "c_flipper_right_main";
		private const string CoilRightFlipperHold = "c_flipper_right_hold";
		private const string CoilAutoPlunger = "c_auto_plunger";

		private TableApi _tableApi;
		private BallManager _ballManager;

		private Dictionary<string, bool> _switchStatus = new Dictionary<string, bool>();

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public void OnInit(TableApi tableApi, BallManager ballManager)
		{
			_tableApi = tableApi;
			_ballManager = ballManager;

			_switchStatus[SwLeftFlipper] = false;
			_switchStatus[SwLeftFlipperEos] = false;
			_switchStatus[SwRightFlipper] = false;
			_switchStatus[SwRightFlipperEos] = false;
			_switchStatus[SwPlunger] = false;
			_switchStatus[SwCreateBall] = false;

			// debug print stuff
			OnCoilChanged += DebugPrintCoil;
		}

		public void OnDestroy()
		{
			OnCoilChanged -= DebugPrintCoil;
		}

		public event EventHandler<CoilEventArgs> OnCoilChanged;

		public void Switch(string id, bool normallyClosed)
		{
			_switchStatus[id] = normallyClosed;
			Logger.Info("Switch {0} is {1}.", id, normallyClosed ? "closed" : "open");

			switch (id) {

				case SwLeftFlipper:
					if (normallyClosed) {
						OnCoilChanged?.Invoke(this, new CoilEventArgs(CoilLeftFlipperMain, true));

					} else {
						OnCoilChanged?.Invoke(this,
							_switchStatus[SwLeftFlipperEos]
								? new CoilEventArgs(CoilLeftFlipperHold, false)
								: new CoilEventArgs(CoilLeftFlipperMain, false)
						);
					}
					break;

				case SwLeftFlipperEos:
					OnCoilChanged?.Invoke(this, new CoilEventArgs(CoilLeftFlipperMain, false));
					OnCoilChanged?.Invoke(this, new CoilEventArgs(CoilLeftFlipperHold, true));
					break;

				case SwRightFlipper:
					if (normallyClosed) {
						OnCoilChanged?.Invoke(this, new CoilEventArgs(CoilRightFlipperMain, true));
					} else {
						OnCoilChanged?.Invoke(this,
							_switchStatus[SwRightFlipperEos]
								? new CoilEventArgs(CoilRightFlipperHold, false)
								: new CoilEventArgs(CoilRightFlipperMain, false)
						);
					}
					break;

				case SwRightFlipperEos:
					OnCoilChanged?.Invoke(this, new CoilEventArgs(CoilRightFlipperMain, false));
					OnCoilChanged?.Invoke(this, new CoilEventArgs(CoilRightFlipperHold, true));
					break;

				case SwPlunger:
					OnCoilChanged?.Invoke(this, new CoilEventArgs(CoilAutoPlunger, normallyClosed));
					break;

				case SwCreateBall: {
					if (normallyClosed) {
						_ballManager.CreateBall(new DebugBallCreator());
					}
					break;
				}
			}
		}

		private void DebugPrintCoil(object sender, CoilEventArgs e)
		{
			Logger.Info("Coil {0} set to {1}.", e.Id, e.IsEnabled);
		}
	}
}
