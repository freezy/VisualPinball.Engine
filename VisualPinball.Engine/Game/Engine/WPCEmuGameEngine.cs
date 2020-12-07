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
using System.Threading;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using WPCEmu;
using WPCEmu.Boards.Elements;
using WPCEmu.Db;

namespace VisualPinball.Engine.Game.Engine
{
	[Serializable]
	public class WPCEmuGameEngine 
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		const int TICKS_PER_SECOND = 2000000;
		const int TICKS_PER_STEP = 16;
		const int INITIAL_FRAMERATE = 50;

		private IDb _gameDb;
		private bool _isRunning;
		private Emulator _wpcSystem;

		public static readonly GamelogicEngineSwitch[] CABINET_INPUT = new GamelogicEngineSwitch[] {
			new GamelogicEngineSwitch { Id = "C001", Description = "COIN #1" },
			new GamelogicEngineSwitch { Id = "C002", Description = "COIN #2" },
			new GamelogicEngineSwitch { Id = "C004", Description = "COIN #3" },
			new GamelogicEngineSwitch { Id = "C008", Description = "COIN #4" },
			new GamelogicEngineSwitch { Id = "C016", Description = "ESCAPE" },
			new GamelogicEngineSwitch { Id = "C032", Description = "-" },
			new GamelogicEngineSwitch { Id = "C064", Description = "+" },
			new GamelogicEngineSwitch { Id = "C128", Description = "ENTER" }
		};

		public static string[] SupportedGames
		{
			get
			{
				return Gamelist.getAllNames().Where(entry =>
					!entry.ToLower().StartsWith("upload") && !entry.ToLower().Contains("test fixture")).ToArray();
			}
		}

		public static string RomFilenameByGame(string name)
		{
			return Gamelist.getByName(name).rom?.u06;
		}

		public static GamelogicEngineSwitch[] SwitchesByGame(string name)
		{
			var switches = new List<GamelogicEngineSwitch>();

			switches.AddRange(CABINET_INPUT);

			switches.AddRange(Gamelist.getByName(name).switchMapping.Select(entry =>
				new GamelogicEngineSwitch
				{
					Id = entry.id,
					Description = entry.name
				}).ToArray());

			switches.AddRange(Gamelist.getByName(name).fliptronicsMappings?.Select(entry =>
				new GamelogicEngineSwitch
				{
					Id = entry.id,
					Description = entry.name
				}).ToArray());

			return switches.ToArray();
		}

		public static GamelogicEngineCoil[] CoilsByGame(string name)
		{
			var coils = new List<GamelogicEngineCoil>();

			coils.AddRange(Gamelist.getByName(name).solenoidMapping?.Select(entry =>
				new GamelogicEngineCoil
				{
					Id = entry.id,
					Description = entry.name
				}).ToArray());

			return coils.ToArray();
		}

		public WPCEmuGameEngine(string name, string path)
		{
			_gameDb = Gamelist.getByName(name);

			var u06 = File.ReadAllBytes(path + "/" + _gameDb.rom?.u06);

			_wpcSystem = Emulator.initVMwithRom(
				new RomBinary
				{
					u06 = u06
				},
				new RomMetaData
				{
					skipWpcRomCheck = _gameDb.skipWpcRomCheck,
					features = _gameDb.features
				});
		}

		public void Start()
		{
			_wpcSystem.reset();
			_wpcSystem.start();

			foreach (var id in _gameDb.initialise?.closedSwitches)
			{
				Logger.Info("Closing Switch: " + id);

				if (int.TryParse(id, out var value))
				{
					_wpcSystem.setSwitchInput((byte)value, true);
				}
			}

			_isRunning = true;

			int ticksPerCall = TICKS_PER_SECOND / INITIAL_FRAMERATE;
			int intervalTimingMs = 1000 / INITIAL_FRAMERATE;

			Logger.Info("TicksPerCall {0}, IntervalTimingMs {1}", ticksPerCall, intervalTimingMs);

			Task.Run(() =>
			{
				new Timer(_ => _wpcSystem.setCabinetInput(16), null, 1500, Timeout.Infinite);
			
				while (_isRunning)
				{
					_wpcSystem.executeCycle(ticksPerCall, TICKS_PER_STEP);
					var state = _wpcSystem.getUiState(false);

					Thread.Sleep(intervalTimingMs);
				}
			});
		}

		public void SetSwitch(string id, bool closed)
		{
			int value;

			if (id.StartsWith("C") && int.TryParse(id.Substring(1), out value))
			{
				_wpcSystem.setCabinetInput((byte)value);
			}
			else if (id.StartsWith("F") && int.TryParse(id.Substring(1), out _))
			{
				_wpcSystem.setFliptronicsInput(id, closed);
			}
			else if (int.TryParse(id, out value))
			{
				_wpcSystem.setSwitchInput((byte)value, closed);
			}
		}

		public byte[] GetDMD()
		{
			var state = _wpcSystem.getState();
			var display = (OutputDmdDisplay.State)state.asic?.display;

			return display.dmdShadedBuffer;
		}

		public byte[] GetSolenoids()
		{
			var state = _wpcSystem.getState();
			return state.asic?.wpc.solenoidState;
		}

		public byte[] GetInputState()
		{
			var state = _wpcSystem.getState();
			return state.asic?.wpc.inputState;
		}

		public void Stop()
		{
			_isRunning = false;
		}
	}
}
