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
using System.IO;
using System.Linq;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;

namespace VisualPinball.Engine.VPT.Mappings
{
	public class Mappings : Item<MappingsData>
	{
		public override string ItemType => "Mappings";

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

		public void PopulateSwitches(GamelogicEngineSwitch[] engineSwitches, IEnumerable<ISwitchable> tableSwitches)
		{
			var switches = tableSwitches
				.GroupBy(x => x.Name.ToLower())
				.ToDictionary(x => x.Key, x => x.First());

			foreach (var engineSwitch in GetSwitchIds(engineSwitches))
			{
				var switchMapping = Data.Switches.FirstOrDefault(mappingsSwitchData => mappingsSwitchData.Id == engineSwitch.Id);

				if (switchMapping == null) {
					var matchKey = int.TryParse(engineSwitch.Id, out var numericSwitchId)
						? $"sw{numericSwitchId}"
						: engineSwitch.Id;

					var matchedItem = switches.ContainsKey(matchKey)
						? switches[matchKey]
						: null;

					var description = GuessDescription(engineSwitch);
					Data.AddSwitch(new MappingsSwitchData {
						Id = engineSwitch.Id,
						Description = description,
						Source = description == string.Empty ? SwitchSource.Playfield : SwitchSource.InputSystem,
						PlayfieldItem = matchedItem == null ? string.Empty : matchedItem.Name,
						Type = matchedItem is Kicker.Kicker || matchedItem is Trigger.Trigger || description != string.Empty
							? SwitchType.OnOff
							: SwitchType.Pulse,
						InputActionMap = GuessInputMap(engineSwitch),
						InputAction = description != string.Empty ? GuessInputAction(engineSwitch) : null,
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

		private string GuessDescription(GamelogicEngineSwitch engineSwitch)
		{
			if (engineSwitch.Description != null)
			{
				return engineSwitch.Description;
			}
			else
			{
				if (engineSwitch.Id.Contains("left_flipper"))
				{
					return "Left Flipper";
				}
				if (engineSwitch.Id.Contains("right_flipper"))
				{
					return "Right Flipper";
				}
				if (engineSwitch.Id.Contains("create_ball"))
				{
					return "Create Ball";
				}
				if (engineSwitch.Id.Contains("plunger"))
				{
					return "Plunger";
				}
			}

			return string.Empty;
		}

		private string GuessInputMap(GamelogicEngineSwitch engineSwitch)
		{
			if (engineSwitch.Id.Contains("create_ball")) {
				return InputConstants.MapDebug;
			}
			return InputConstants.MapCabinetSwitches;
		}

		private string GuessInputAction(GamelogicEngineSwitch engineSwitch)
		{
			if (engineSwitch.InputActionHint != null)
			{
				return engineSwitch.InputActionHint;
			}
			else
			{
				if (engineSwitch.Id.Contains("left_flipper"))
				{
					return InputConstants.ActionLeftFlipper;
				}
				if (engineSwitch.Id.Contains("right_flipper"))
				{
					return InputConstants.ActionRightFlipper;
				}
				if (engineSwitch.Id.Contains("create_ball"))
				{
					return InputConstants.ActionCreateBall;
				}
				if (engineSwitch.Id.Contains("plunger"))
				{
					return InputConstants.ActionPlunger;
				}
			}

			return string.Empty;
		}


		#endregion

		#region Coil Population

		/// <summary>
		/// Auto-matches the coils provided by the gamelogic engine with the
		/// coils on the playfield.
		/// </summary>
		/// <param name="engineCoils">List of coils provided by the gamelogic engine</param>
		/// <param name="tableCoils">List of coils on the playfield</param>
		public void PopulateCoils(GamelogicEngineCoil[] engineCoils, ICollection<string> tableCoils)
		{
			foreach (var engineCoil in GetCoilIds(engineCoils)) {

				var coilMapping = Data.Coils.FirstOrDefault(mappingsCoilData => mappingsCoilData.Id == engineCoil.Id);
				if (coilMapping == null) {
					var itemName = string.Empty;
					var description = string.Empty;
					switch (engineCoil.Id) {
						case "c_left_flipper":
							itemName = FindCoil(tableCoils, "LeftFlipper", "FlipperLeft", "FlipperL", "LFlipper");
							description = "Left Flipper";
							break;

						case "c_right_flipper":
							itemName = FindCoil(tableCoils, "RightFlipper", "FlipperRight", "FlipperR", "RFlipper");
							description = "Right Flipper";
							break;

						case "c_auto_plunger":
							itemName = FindCoil(tableCoils, "Plunger");
							description = "Plunger";
							break;
					}

					Data.AddCoil(new MappingsCoilData {
						Id = engineCoil.Id,
						Description = description,
						Destination = CoilDestination.Playfield,
						PlayfieldItem = itemName,
						Type = CoilType.SingleWound
					});
				}
			}
		}

		/// <summary>
		/// Returns a sorted list of coil names from the gamelogic engine,
		/// appended with the additional names in the coil mapping. In short,
		/// the list of coil names to choose from.
		/// </summary>
		/// <param name="engineCoils">Coil names provided by the gamelogic engine</param>
		/// <returns>All coil names</returns>
		public IEnumerable<GamelogicEngineCoil> GetCoilIds(GamelogicEngineCoil[] engineCoils)
		{
			var ids = new List<GamelogicEngineCoil>();
			if (engineCoils != null) {
				ids.AddRange(engineCoils);
			}

			foreach (var mappingsCoilData in Data.Coils) {
				if (!ids.Exists(entry => entry.Id == mappingsCoilData.Id))
				{
					ids.Add(new GamelogicEngineCoil
					{
						Id = mappingsCoilData.Id
					});

				}
				if (!ids.Exists(entry => entry.Id == mappingsCoilData.HoldCoilId))
				{
					ids.Add(new GamelogicEngineCoil
					{
						Id = mappingsCoilData.HoldCoilId
					});
				}
			}

			ids.Sort((s1, s2) => s1.Id.CompareTo(s2.Id));
			return ids;
		}

		private static string FindCoil(ICollection<string> coils, params string[] names)
		{
			foreach (var itemName in names) {
				if (coils.Contains(itemName.ToLower())) {
					return itemName;
				}
			}
			return string.Empty;
		}

		#endregion
	}
}
