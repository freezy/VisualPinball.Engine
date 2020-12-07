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
using System.Linq;
using NLog;
using UnityEngine;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game.Engine;

namespace VisualPinball.Unity
{
	[Serializable]
	public class WPCEmuGamelogicEngine : IGamelogicEngine, IGamelogicEngineWithSwitches, IGamelogicEngineWithCoils
	{
		private static readonly NLog.Logger Logger = LogManager.GetCurrentClassLogger();

		public string Name => "WPCEmu Game Engine";

		static readonly Dictionary<string, string> INPUT_ACTION_HINTS = new Dictionary<string, string>()
		{
			["R FLIPPER BUTTON"] = InputConstants.ActionRightFlipper,
			["L FLIPPER BUTTON"] = InputConstants.ActionLeftFlipper,
			["SLAM TILT"] = InputConstants.ActionSlamTilt,
			["LAUNCH BUTTON"] = InputConstants.ActionPlunger,
			["START BUTTON"] = InputConstants.ActionStartGame,
			["COIN DOOR CLOSED"] = InputConstants.ActionCoinDoorOpenClose,
			["COIN #1"] = InputConstants.ActionInsertCoin1,
			["COIN #2"] = InputConstants.ActionInsertCoin2,
			["COIN #3"] = InputConstants.ActionInsertCoin3,
			["COIN #4"] = InputConstants.ActionInsertCoin4,
			["ESCAPE"] = InputConstants.ActionCoinDoorCancel,
			["-"] = InputConstants.ActionCoinDoorDown,
			["+"] = InputConstants.ActionCoinDoorUp,
			["ENTER"] = InputConstants.ActionCoinDoorEnter
		};

		static readonly Dictionary<string, string> PLAYFIELD_ITEM_HINTS = new Dictionary<string, string>()
		{
			["L FLIPPER EOS"] = "^(LeftFlipper|LFlipper|FlipperLeft|FlipperL)$",
			["R FLIPPER EOS"] = "^(RightFlipper|RFlipper|FlipperRight|FlipperR)$"
		};

		private const string SwCreateBall = "s_create_ball";

		private static readonly Color Tint = new Color(1, 0.18f, 0);

		private Color[] _map = new Color[] {
			Color.Lerp(Color.black, Tint, 0),
			Color.Lerp(Color.black, Tint, 0.33f),
			Color.Lerp(Color.black, Tint, 0.66f),
			Color.Lerp(Color.black, Tint, 1f),
			Color.Lerp(Color.black, Tint, 1f)
		};

		private TableApi _tableApi;
		private BallManager _ballManager;

		private Texture2D _texture;
		private WPCEmuGameEngine _wpcEmuGameEngine;

		public event EventHandler<CoilEventArgs> OnCoilChanged;

		public string[] SupportedGames => WPCEmuGameEngine.SupportedGames;

		private byte[] lastSolenoids;
		private byte[] solenoidsSmoothed;
		private byte[] lastInputState;

		public Dictionary<string, GamelogicEngineSwitch> DefinedSwitches;
		public Dictionary<string, GamelogicEngineCoil> DefinedCoils;

		public GamelogicEngineSwitch[] AvailableSwitches
		{
			get
			{
				List<GamelogicEngineSwitch> switches = new List<GamelogicEngineSwitch>();

				var wpcGameEngineAuthoring = UnityEngine.Object.FindObjectOfType<WPCEmuGameEngineAuthoring>();
				if (wpcGameEngineAuthoring.Name != null)
				{
					foreach (var gleSwitch in WPCEmuGameEngine.SwitchesByGame(wpcGameEngineAuthoring.Name))
					{
						var newSwitch = new GamelogicEngineSwitch
						{
							Id = gleSwitch.Id,
							Description = gleSwitch.Description
						};

						if (INPUT_ACTION_HINTS.TryGetValue(gleSwitch.Description.ToUpper(), out var value))
						{
							newSwitch.InputActionHint = value;
							newSwitch.InputMapHint = InputConstants.MapCabinetSwitches;
						}
						else if (PLAYFIELD_ITEM_HINTS.TryGetValue(gleSwitch.Description.ToUpper(), out value))
						{
							newSwitch.PlayfieldItemHint = value;
						}

						switches.Add(newSwitch);
					}
				}

				switches.Add(new GamelogicEngineSwitch
				{
					Id = SwCreateBall,
					Description = "Create Debug Ball",
					InputActionHint = InputConstants.ActionCreateBall,
					InputMapHint = InputConstants.MapDebug
				});

				return switches.ToArray();
			}
		}

		public GamelogicEngineCoil[] AvailableCoils
		{
			get
			{
				List<GamelogicEngineCoil> coils = new List<GamelogicEngineCoil>();

				var wpcGameEngineAuthoring = UnityEngine.Object.FindObjectOfType<WPCEmuGameEngineAuthoring>();
				if (wpcGameEngineAuthoring.Name != null)
				{
					foreach (var gleCoil in WPCEmuGameEngine.CoilsByGame(wpcGameEngineAuthoring.Name))
					{
						coils.Add(new GamelogicEngineCoil
						{
							Id = gleCoil.Id,
							Description = gleCoil.Description
						});
					}
				}

				return coils.ToArray();
			}
		}

		public string RomFilename
		{
			get
			{
				var wpcGameEngineAuthoring = UnityEngine.Object.FindObjectOfType<WPCEmuGameEngineAuthoring>();
				return WPCEmuGameEngine.RomFilenameByGame(wpcGameEngineAuthoring.Name);
			}
		}

		public void SetDefinedSwitches(Dictionary<string, GamelogicEngineSwitch> switches)
		{
			DefinedSwitches = switches;
		}

		public void SetDefinedCoils(Dictionary<string, GamelogicEngineCoil> coils)
		{
			DefinedCoils = coils;
		}

		public void OnInit(TableApi tableApi, BallManager ballManager)
		{
			_tableApi = tableApi;
			_ballManager = ballManager;

			var wpcGameEngineAuthoring = UnityEngine.Object.FindObjectOfType<WPCEmuGameEngineAuthoring>();
			_texture = wpcGameEngineAuthoring.Texture;

			_wpcEmuGameEngine = new WPCEmuGameEngine(wpcGameEngineAuthoring.Name, Application.streamingAssetsPath);
			_wpcEmuGameEngine.Start();
		}

		public void OnUpdate()
		{
			if (_wpcEmuGameEngine == null)
			{
				return;
			}

			UpdateDMD();
			UpdateSolenoids();
			LogInputState();
		}

		public void OnDestroy()
		{
			_wpcEmuGameEngine.Stop();
		}

		public void Switch(string id, bool closed)
		{
			DefinedSwitches.TryGetValue(id, out GamelogicEngineSwitch definedSwitch);

			Logger.Info("Switch(): #{0} ({1}), {2}", id, definedSwitch.Description, closed ? "CLOSED" : "OPEN");

			if (id == SwCreateBall)
			{
				if (closed)
				{
					_ballManager.CreateBall(new DebugBallCreator());
				}
			}
			else
			{
				_wpcEmuGameEngine.SetSwitch(id, closed);
			}
		}

		private void UpdateDMD()
		{
			var buffer = _wpcEmuGameEngine.GetDMD();

			for (var y = 0; y < 32; y++)
			{
				for (var x = 0; x < 128; x++)
				{
					var pixel = y * 128 + x;
					var value = buffer[pixel];

					_texture.SetPixel(x, y, _map[value]);
				}
			}

			_texture.Apply();
		}

		private void UpdateSolenoids()
		{
			var solenoids = _wpcEmuGameEngine.GetSolenoids();
			var log = "";
			var invoke = "";

			if (lastSolenoids != null)
			{
				for (var index = 0; index < solenoids.Length; index++)
				{
					if (solenoids[index] != lastSolenoids[index])
					{
						var id = (index + 1).ToString("D2");

						DefinedCoils.TryGetValue(id, out GamelogicEngineCoil definedCoil);

						if (log.Length > 0)
						{
							log += ", ";
						}

						log += $"#{id} ({definedCoil.Description}) = {lastSolenoids[index].ToString("X2")} -> {solenoids[index].ToString("X2")}";

						if (solenoids[index] > lastSolenoids[index])
						{
							if (solenoidsSmoothed[index] != 0xFF)
							{
								OnCoilChanged?.Invoke(this, new CoilEventArgs(id, true));
								solenoidsSmoothed[index] = 0xFF;

								invoke = $"Invoking Coil #{id} ({definedCoil.Description}) with TRUE";
							}
						}
						else if (solenoids[index] < lastSolenoids[index])
						{
							if (solenoidsSmoothed[index] != 0x00)
							{
								OnCoilChanged?.Invoke(this, new CoilEventArgs(id, false));
								solenoidsSmoothed[index] = 0x00;

								invoke = $"Invoking Coil #{id} ({definedCoil.Description}) with FALSE";
							}
						}
					}
				}
			}
			else
			{
				solenoidsSmoothed = Enumerable.Repeat((byte)0x00, solenoids.Length).ToArray();
			}

			lastSolenoids = solenoids.Take(solenoids.Length).ToArray();

			if (log.Length > 0)
			{
				Logger.Info("UpdateSolenoids(): {0}", log);

				if (invoke.Length > 0)
				{
					Logger.Info("UpdateSolenoids(): {0}", invoke);
				}
			}
		}

		private void LogInputState()
		{
			var inputState = _wpcEmuGameEngine.GetInputState();
			var log = "";

			if (lastInputState != null)
			{
				for (var index = 0; index < inputState.Length; index++)
				{
					if (inputState[index] != lastInputState[index])
					{
						if (log.Length > 0)
						{
							log += ", ";
						}

						log += $"#{index} = {lastInputState[index].ToString("X2")} -> {inputState[index].ToString("X2")}";
					}
				}
			}

			lastInputState = inputState.Take(inputState.Length).ToArray();

			if (log.Length > 0)
			{
				Logger.Info("LogInputState(): {0}", log);
			}
		}
	}
}
