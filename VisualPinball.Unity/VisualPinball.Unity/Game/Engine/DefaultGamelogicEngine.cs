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

		public string[] AvailableSwitches { get; } = {SwLeftFlipper, SwRightFlipper, SwPlunger, SwCreateBall};

		public string[] AvailableCoils { get; } = {CoilLeftFlipper, CoilRightFlipper, CoilAutoPlunger};

		private const string SwLeftFlipper = "s_left_flipper";
		private const string SwRightFlipper = "s_right_flipper";
		private const string SwPlunger = "s_plunger";
		private const string SwCreateBall = "s_create_ball";

		private const string CoilLeftFlipper = "c_left_flipper";
		private const string CoilRightFlipper = "c_right_flipper";
		private const string CoilAutoPlunger = "c_auto_plunger";

		private TableApi _tableApi;
		private BallManager _ballManager;

		private FlipperApi _leftFlipper;
		private FlipperApi _rightFlipper;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public void OnInit(TableApi tableApi, BallManager ballManager)
		{
			_tableApi = tableApi;
			_ballManager = ballManager;

			// flippers
			_leftFlipper = _tableApi.Flipper("LeftFlipper")
			             ?? _tableApi.Flipper("FlipperLeft")
			             ?? _tableApi.Flipper("FlipperL")
			             ?? _tableApi.Flipper("LFlipper");
			_rightFlipper = _tableApi.Flipper("RightFlipper")
			             ?? _tableApi.Flipper("FlipperRight")
			             ?? _tableApi.Flipper("FlipperR")
			             ?? _tableApi.Flipper("RFlipper");

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
			switch (id) {

				case SwLeftFlipper:

					// todo remove when solenoids are done
					if (normallyClosed) {
						_leftFlipper?.RotateToEnd();
					} else {
						_leftFlipper?.RotateToStart();
					}
					OnCoilChanged?.Invoke(this, new CoilEventArgs(CoilLeftFlipper, normallyClosed));
					break;

				case SwRightFlipper:

					// todo remove when solenoids are done
					if (normallyClosed) {
						_rightFlipper?.RotateToEnd();
					} else {
						_rightFlipper?.RotateToStart();
					}

					OnCoilChanged?.Invoke(this, new CoilEventArgs(CoilRightFlipper, normallyClosed));
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
			//Logger.Info("Coil {0} set to {1}.", e.Name, e.IsEnabled);
		}
	}
}
