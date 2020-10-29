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

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NetVips;
using NLog;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Game.Engines;

namespace VisualPinball.Engine.VPT.Mappings
{
	public class Mappings : Item<MappingsData>
	{
		public override string ItemName { get; } = "Mapping";
		public override string ItemGroupName { get; } = "Mappings";

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public Mappings() : this(new MappingsData("Mappings"))
		{
		}

		public Mappings(MappingsData data) : base(data)
		{
		}

		public Mappings(BinaryReader reader, string itemName) : this(new MappingsData(reader, itemName))
		{
		}

		public bool IsEmpty()
		{
			return (Data.Coils == null || Data.Coils.Length == 0)
				&& (Data.Switches == null || Data.Switches.Length == 0);
		}

		#region Switch Population

		public void PopulateSwitches(GamelogicEngineSwitch[] engineSwitches, IEnumerable<ISwitchable> tableSwitches, IEnumerable<ISwitchableDevice> tableSwitchDevices)
		{
			var switches = tableSwitches
				.GroupBy(x => x.Name.ToLower())
				.ToDictionary(x => x.Key, x => x.First());

			var switchDevices = tableSwitchDevices
				.GroupBy(x => x.Name.ToLower())
				.ToDictionary(x => x.Key, x => x.First());

			foreach (var engineSwitch in GetSwitchIds(engineSwitches))
			{
				var switchMapping = Data.Switches.FirstOrDefault(mappingsSwitchData => mappingsSwitchData.Id == engineSwitch.Id);

				if (switchMapping == null) {

					var description = engineSwitch.Description ?? string.Empty;
					var source = GuessSwitchSource(engineSwitch);
					var playfieldItem = source == SwitchSource.Playfield ? GuessPlayfieldSwitch(switches, engineSwitch) : null;
					var device = source == SwitchSource.Device ? GuessDevice(switchDevices, engineSwitch) : null;
					var deviceItem = source == SwitchSource.Device && device != null ? GuessDeviceSwitch(engineSwitch, device) : default;
					var inputActionMap = source == SwitchSource.InputSystem
						? string.IsNullOrEmpty(engineSwitch.InputMapHint) ? InputConstants.MapCabinetSwitches : engineSwitch.InputMapHint
						: string.Empty;
					var inputAction = source == SwitchSource.InputSystem
						? string.IsNullOrEmpty(engineSwitch.InputActionHint) ? string.Empty : engineSwitch.InputActionHint
						: string.Empty;

					Data.AddSwitch(new MappingsSwitchData {
						Id = engineSwitch.Id,
						Description = description,
						Source = source,
						PlayfieldItem = playfieldItem != null ? playfieldItem.Name : string.Empty,
						InputActionMap = inputActionMap,
						InputAction = inputAction,
						Device = device != null ? device.Name : string.Empty,
						DeviceItem = deviceItem.Id
					});
				}
			}
		}

		/// <summary>
		/// Returns a sorted list of switch names from the gamelogic engine,
		/// appended with the additional names in the switch mapping. In short,
		/// the list of switch names to choose from.
		/// </summary>
		/// <param name="engineSwitches">Switch names provided by the gamelogic engine</param>
		/// <returns>All switch names</returns>
		public IEnumerable<GamelogicEngineSwitch> GetSwitchIds(GamelogicEngineSwitch[] engineSwitches)
		{
			var ids = new List<GamelogicEngineSwitch>();
			if (engineSwitches != null) {
				ids.AddRange(engineSwitches);
			}

			foreach (var mappingsSwitchData in Data.Switches) {
				if (!ids.Exists(entry => entry.Id == mappingsSwitchData.Id))
				{
					ids.Add(new GamelogicEngineSwitch
					{
						Id = mappingsSwitchData.Id
					});
				}
			}

			ids.Sort((s1, s2) => s1.Id.CompareTo(s2.Id));
			return ids;
		}

		private static int GuessSwitchSource(GamelogicEngineSwitch engineSwitch)
		{
			if (!string.IsNullOrEmpty(engineSwitch.DeviceHint)) {
				return SwitchSource.Device;
			}

			return !string.IsNullOrEmpty(engineSwitch.InputActionHint) ? SwitchSource.InputSystem : SwitchSource.Playfield;
		}

		private static ISwitchable GuessPlayfieldSwitch(Dictionary<string, ISwitchable> switches, GamelogicEngineSwitch engineSwitch)
		{
			// first, match by regex if hint provided
			if (!string.IsNullOrEmpty(engineSwitch.PlayfieldItemHint)) {
				foreach (var switchName in switches.Keys) {
					var regex = new Regex(engineSwitch.PlayfieldItemHint.ToLower());
					if (regex.Match(switchName).Success) {
						return switches[switchName];
					}
				}
			}

			// second, match by "swXX" or name
			var matchKey = int.TryParse(engineSwitch.Id, out var numericSwitchId)
				? $"sw{numericSwitchId}"
				: engineSwitch.Id;

			return switches.ContainsKey(matchKey) ? switches[matchKey] : null;
		}

		private static ISwitchableDevice GuessDevice(Dictionary<string, ISwitchableDevice> switchDevices, GamelogicEngineSwitch engineSwitch)
		{
			// match by regex if hint provided
			if (!string.IsNullOrEmpty(engineSwitch.DeviceHint)) {
				foreach (var deviceName in switchDevices.Keys) {
					var regex = new Regex(engineSwitch.DeviceHint.ToLower());
					if (regex.Match(deviceName).Success) {
						return switchDevices[deviceName];
					}
				}
			}
			return null;
		}

		private static GamelogicEngineSwitch GuessDeviceSwitch(GamelogicEngineSwitch engineSwitch, ISwitchableDevice device)
		{
			if (!string.IsNullOrEmpty(engineSwitch.DeviceItemHint)) {
				foreach (var deviceSwitch in device.AvailableSwitches) {
					var regex = new Regex(engineSwitch.DeviceItemHint.ToLower());
					if (regex.Match(deviceSwitch.Id).Success) {
						return deviceSwitch;
					}
				}
			}
			return default;
		}

		#endregion

		#region Coil Population

		/// <summary>
		/// Auto-matches the coils provided by the gamelogic engine with the
		/// coils on the playfield.
		/// </summary>
		/// <param name="engineCoils">List of coils provided by the gamelogic engine</param>
		/// <param name="tableCoils">List of coils on the playfield</param>
		public void PopulateCoils(GamelogicEngineCoil[] engineCoils, IEnumerable<ICoilable> tableCoils, IEnumerable<ICoilableDevice> tableCoilDevices)
		{
			var coils = tableCoils
				.GroupBy(x => x.Name.ToLower())
				.ToDictionary(x => x.Key, x => x.First());

			var coilDevices = tableCoilDevices
				.GroupBy(x => x.Name.ToLower())
				.ToDictionary(x => x.Key, x => x.First());

			var holdCoils = new List<GamelogicEngineCoil>();
			foreach (var engineCoil in GetCoils(engineCoils)) {

				var coilMapping = Data.Coils.FirstOrDefault(mappingsCoilData => mappingsCoilData.Id == engineCoil.Id);
				if (coilMapping == null) {

					// we'll handle those in a second loop when all the main coils are added
					if (!string.IsNullOrEmpty(engineCoil.MainCoilIdOfHoldCoil)) {
						holdCoils.Add(engineCoil);
						continue;
					}

					var destination = GuessCoilDestination(engineCoil);
					var description = string.IsNullOrEmpty(engineCoil.Description) ? string.Empty : engineCoil.Description;
					var playfieldItem = destination == CoilDestination.Playfield ? GuessPlayfieldCoil(coils, engineCoil) : null;
					var device = destination == CoilDestination.Device ? GuessDevice(coilDevices, engineCoil) : null;
					var deviceItem = destination == CoilDestination.Device && device != null ? GuessDeviceCoil(engineCoil, device) : default;

					Data.AddCoil(new MappingsCoilData {
						Id = engineCoil.Id,
						Description = description,
						Destination = destination,
						PlayfieldItem = playfieldItem != null ? playfieldItem.Name : string.Empty,
						Device = device != null ? device.Name : string.Empty,
						DeviceItem = deviceItem.Id,
						Type = CoilType.SingleWound
					});
				}
			}

			foreach (var holdCoil in holdCoils) {
				var mainCoil = Data.Coils.FirstOrDefault(c => c.Id == holdCoil.MainCoilIdOfHoldCoil);
				if (mainCoil != null) {
					mainCoil.Type = CoilType.DualWound;
					mainCoil.HoldCoilId = holdCoil.Id;

				} else {
					var playfieldItem = GuessPlayfieldCoil(coils, holdCoil);
					Data.AddCoil(new MappingsCoilData {
						Id = holdCoil.Id,
						Description = string.IsNullOrEmpty(holdCoil.Description) ? string.Empty : holdCoil.Description,
						Destination = CoilDestination.Playfield,
						PlayfieldItem = playfieldItem != null ? playfieldItem.Name : string.Empty,
						Type = CoilType.SingleWound
					});
				}
			}
		}

		private static int GuessCoilDestination(GamelogicEngineCoil engineCoil)
		{
			return !string.IsNullOrEmpty(engineCoil.DeviceHint) ? CoilDestination.Device : CoilDestination.Playfield;
		}

		private static ICoilableDevice GuessDevice(Dictionary<string, ICoilableDevice> coilDevices, GamelogicEngineCoil engineCoil)
		{
			// match by regex if hint provided
			if (!string.IsNullOrEmpty(engineCoil.DeviceHint)) {
				foreach (var deviceName in coilDevices.Keys) {
					var regex = new Regex(engineCoil.DeviceHint.ToLower());
					if (regex.Match(deviceName).Success) {
						return coilDevices[deviceName];
					}
				}
			}
			return null;
		}

		private static GamelogicEngineCoil GuessDeviceCoil(GamelogicEngineCoil engineCoil, ICoilableDevice device)
		{
			if (!string.IsNullOrEmpty(engineCoil.DeviceItemHint)) {
				foreach (var deviceCoil in device.AvailableCoils) {
					var regex = new Regex(engineCoil.DeviceItemHint.ToLower());
					if (regex.Match(deviceCoil.Id).Success) {
						return deviceCoil;
					}
				}
			}
			return default;
		}

		/// <summary>
		/// Returns a sorted list of coil names from the gamelogic engine,
		/// appended with the additional names in the coil mapping. In short,
		/// the list of coil names to choose from.
		/// </summary>
		/// <param name="engineCoils">Coil names provided by the gamelogic engine</param>
		/// <returns>All coil names</returns>
		public IEnumerable<GamelogicEngineCoil> GetCoils(GamelogicEngineCoil[] engineCoils)
		{
			var coils = new List<GamelogicEngineCoil>();

			// first, add coils from the gamelogic engine
			if (engineCoils != null) {
				coils.AddRange(engineCoils);
			}

			// then add coil ids that were added manually
			foreach (var mappingsCoilData in Data.Coils) {
				if (!coils.Exists(entry => entry.Id == mappingsCoilData.Id))
				{
					coils.Add(new GamelogicEngineCoil
					{
						Id = mappingsCoilData.Id
					});

				}
				if (!coils.Exists(entry => entry.Id == mappingsCoilData.HoldCoilId))
				{
					coils.Add(new GamelogicEngineCoil
					{
						Id = mappingsCoilData.HoldCoilId
					});
				}
			}

			coils.Sort((s1, s2) => s1.Id.CompareTo(s2.Id));
			return coils;
		}

		private static ICoilable GuessPlayfieldCoil(Dictionary<string, ICoilable> coils, GamelogicEngineCoil coil)
		{
			// first, match by regex if hint provided
			if (!string.IsNullOrEmpty(coil.PlayfieldItemHint)) {
				foreach (var coilName in coils.Keys) {
					var regex = new Regex(coil.PlayfieldItemHint.ToLower());
					if (regex.Match(coilName).Success) {
						return coils[coilName];
					}
				}
			}

			// second, match by id
			return coils.ContainsKey(coil.Id) ? coils[coil.Id] : null;
		}

		#endregion

	}
}
